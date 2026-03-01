// StingBIM.AI.NLP.Consulting.BIMConsultantEngine
// Domain-specific BIM advisory engine with reasoning and knowledge integration.
// Provides expert consulting responses enriched by KnowledgeGraph inference,
// StandardsIntegration compliance lookups, MaterialIntelligence recommendations,
// and cross-domain reasoning across 12 specialist domains.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using StingBIM.AI.Knowledge.Graph;
using StingBIM.AI.Knowledge.Inference;
using StingBIM.AI.Knowledge.Standards;
using StingBIM.AI.Reasoning.Compliance;
using StingBIM.AI.Reasoning.Decision;
using StingBIM.AI.Reasoning.Materials;
using StingBIM.AI.Reasoning.Optimization;
using StingBIM.AI.Reasoning.Patterns;
using StingBIM.AI.Reasoning.Predictive;
using StingBIM.AI.Reasoning.Spatial;
using StingBIM.AI.NLP.Dialogue;
using StingBIM.AI.NLP.Pipeline;

namespace StingBIM.AI.NLP.Consulting
{
    /// <summary>
    /// BIM consulting engine that routes advisory queries to domain-specific handlers
    /// and enriches responses with reasoning from the Knowledge and Reasoning layers.
    /// </summary>
    public class BIMConsultantEngine
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly Dictionary<string, Func<ConsultingRequest, CancellationToken, Task<ConsultingResponse>>> _handlers;
        private readonly ConsultingReasoningPipeline _reasoningPipeline;

        // Cross-domain keyword map: secondary domain keywords that trigger cross-references
        private static readonly Dictionary<string, List<string>> CrossDomainKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            ["CONSULT_COST"] = new() { "cost", "budget", "price", "expensive", "cheap", "afford", "estimate" },
            ["CONSULT_FIRE_SAFETY"] = new() { "fire", "sprinkler", "egress", "smoke", "flame" },
            ["CONSULT_SUSTAINABILITY"] = new() { "green", "sustainable", "leed", "carbon", "embodied", "recycled" },
            ["CONSULT_ENERGY"] = new() { "thermal", "u-value", "insulation", "heat", "energy", "shgc" },
            ["CONSULT_ACOUSTICS"] = new() { "acoustic", "sound", "noise", "stc", "rw", "vibration" },
            ["CONSULT_ACCESSIBILITY"] = new() { "accessible", "ada", "wheelchair", "disability", "ramp" },
            ["CONSULT_COMPLIANCE"] = new() { "code", "comply", "compliant", "regulation", "requirement", "legal" },
            ["CONSULT_STRUCTURAL"] = new() { "structural", "load", "beam", "column", "foundation", "seismic" },
            ["CONSULT_MATERIALS"] = new() { "material", "concrete", "steel", "timber", "masonry", "glass" },
            ["MANAGE_OPTIMIZATION"] = new() { "optimize", "efficient", "improve", "layout", "better" },
            ["MANAGE_VALIDATION"] = new() { "validate", "check", "verify", "correct", "issue" },
            ["MANAGE_DESIGN_PATTERNS"] = new() { "pattern", "best practice", "guideline", "template" },
            ["CONSULT_BEP"] = new() { "bep", "execution plan", "iso 19650", "eir", "midp", "tidp", "cde" },
            ["CONSULT_DWG_TO_BIM"] = new() { "dwg", "cad", "autocad", "2d to 3d", "convert", "legacy" },
            ["CONSULT_IMAGE_TO_BIM"] = new() { "scan", "image", "pdf", "photo", "floor plan image", "digitize" },
            ["MANAGE_GENERATIVE_DESIGN"] = new() { "generative", "variant", "optimize layout", "ai design", "pareto" },
        };

        /// <summary>
        /// Creates a BIMConsultantEngine with optional reasoning and knowledge layer dependencies.
        /// When dependencies are null, the engine falls back to static domain knowledge.
        /// </summary>
        public BIMConsultantEngine(
            KnowledgeGraph knowledgeGraph = null,
            InferenceEngine inferenceEngine = null,
            StandardsIntegration standardsIntegration = null,
            ComplianceChecker complianceChecker = null,
            MaterialIntelligence materialIntelligence = null,
            SpatialReasoner spatialReasoner = null,
            DesignPatternRecognizer patternRecognizer = null,
            DecisionSupport decisionSupport = null,
            DesignOptimizer designOptimizer = null,
            PredictiveEngine predictiveEngine = null)
        {
            _reasoningPipeline = new ConsultingReasoningPipeline(
                knowledgeGraph, inferenceEngine, standardsIntegration,
                complianceChecker, materialIntelligence,
                spatialReasoner, patternRecognizer, decisionSupport,
                designOptimizer, predictiveEngine);

            _handlers = new Dictionary<string, Func<ConsultingRequest, CancellationToken, Task<ConsultingResponse>>>(
                StringComparer.OrdinalIgnoreCase)
            {
                // 12 specialist consulting domains
                ["CONSULT_STRUCTURAL"] = HandleStructuralAsync,
                ["CONSULT_MEP"] = HandleMEPAsync,
                ["CONSULT_COMPLIANCE"] = HandleComplianceAsync,
                ["CONSULT_MATERIALS"] = HandleMaterialsAsync,
                ["CONSULT_COST"] = HandleCostAsync,
                ["CONSULT_SUSTAINABILITY"] = HandleSustainabilityAsync,
                ["CONSULT_FIRE_SAFETY"] = HandleFireSafetyAsync,
                ["CONSULT_ACCESSIBILITY"] = HandleAccessibilityAsync,
                ["CONSULT_ENERGY"] = HandleEnergyAsync,
                ["CONSULT_ACOUSTICS"] = HandleAcousticsAsync,
                ["CONSULT_DAYLIGHTING"] = HandleDaylightingAsync,
                ["CONSULT_SITE_PLANNING"] = HandleSitePlanningAsync,
                // 6 BIM management / intelligence domains
                ["MANAGE_DESIGN_ANALYSIS"] = HandleDesignAnalysisAsync,
                ["MANAGE_OPTIMIZATION"] = HandleOptimizationAsync,
                ["MANAGE_DECISION_SUPPORT"] = HandleDecisionSupportAsync,
                ["MANAGE_VALIDATION"] = HandleValidationAsync,
                ["MANAGE_DESIGN_PATTERNS"] = HandleDesignPatternsAsync,
                ["MANAGE_PREDICTIVE"] = HandlePredictiveAsync,
                // New Phase 4 consulting domains
                ["CONSULT_BEP"] = HandleBEPAsync,
                ["CONSULT_DWG_TO_BIM"] = HandleDWGToBIMAsync,
                ["CONSULT_IMAGE_TO_BIM"] = HandleImageToBIMAsync,
                ["MANAGE_GENERATIVE_DESIGN"] = HandleGenerativeDesignAsync,
            };
        }

        /// <summary>
        /// Returns true if the intent is a consulting intent handled by this engine.
        /// </summary>
        public bool CanHandle(string intent)
        {
            return intent != null && _handlers.ContainsKey(intent);
        }

        /// <summary>
        /// Returns the domain name for a consulting intent, or null.
        /// </summary>
        public string GetDomainName(string intent)
        {
            return intent switch
            {
                "CONSULT_STRUCTURAL" => "Structural Engineering",
                "CONSULT_MEP" => "MEP Engineering",
                "CONSULT_COMPLIANCE" => "Code Compliance",
                "CONSULT_MATERIALS" => "Materials Selection",
                "CONSULT_COST" => "Cost Estimation",
                "CONSULT_SUSTAINABILITY" => "Sustainability",
                "CONSULT_FIRE_SAFETY" => "Fire Safety",
                "CONSULT_ACCESSIBILITY" => "Accessibility",
                "CONSULT_ENERGY" => "Energy Efficiency",
                "CONSULT_ACOUSTICS" => "Acoustics",
                "CONSULT_DAYLIGHTING" => "Daylighting",
                "CONSULT_SITE_PLANNING" => "Site Planning",
                "MANAGE_DESIGN_ANALYSIS" => "Design Analysis",
                "MANAGE_OPTIMIZATION" => "Design Optimization",
                "MANAGE_DECISION_SUPPORT" => "Decision Support",
                "MANAGE_VALIDATION" => "Design Validation",
                "MANAGE_DESIGN_PATTERNS" => "Design Patterns",
                "MANAGE_PREDICTIVE" => "Predictive Guidance",
                "CONSULT_BEP" => "BIM Execution Plan",
                "CONSULT_DWG_TO_BIM" => "DWG-to-BIM Conversion",
                "CONSULT_IMAGE_TO_BIM" => "Image/PDF-to-BIM Conversion",
                "MANAGE_GENERATIVE_DESIGN" => "AI Generative Design",
                _ => null
            };
        }

        /// <summary>
        /// Processes a consulting query: runs the domain handler, enriches with reasoning
        /// and knowledge, detects cross-domain references, and builds a confidence-scored response.
        /// </summary>
        public async Task<ConversationResponse> ConsultAsync(
            ConsultingRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var startTime = DateTime.Now;
            Logger.Info($"Consulting request: {request.Intent} - {request.UserQuery}");

            if (!_handlers.TryGetValue(request.Intent, out var handler))
            {
                return new ConversationResponse
                {
                    Message = "I don't have a specialist for that topic. Could you rephrase your question?",
                    ResponseType = ResponseType.Error
                };
            }

            try
            {
                // Step 1: Run domain-specific handler for base advisory
                var result = await handler(request, cancellationToken);
                result.ReasoningChain.Add(new ReasoningStep
                {
                    Source = "DomainHandler",
                    Description = $"Matched domain: {result.Domain}",
                    Confidence = 0.85f
                });

                // Step 2: Extract parametric values from entities to refine advice
                EnrichFromEntities(result, request);

                // Step 3: Enrich with knowledge graph and inference engine
                await _reasoningPipeline.EnrichWithKnowledgeAsync(result, request, cancellationToken);

                // Step 4: Enrich with standards integration
                await _reasoningPipeline.EnrichWithStandardsAsync(result, request, cancellationToken);

                // Step 5: Run domain-specific reasoning (compliance, materials, etc.)
                await _reasoningPipeline.EnrichWithDomainReasoningAsync(result, request, cancellationToken);

                // Step 6: Detect cross-domain implications
                DetectCrossDomainReferences(result, request);

                // Step 7: Compute aggregate confidence from reasoning chain
                result.ConfidenceLevel = ComputeConfidence(result);

                var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
                Logger.Info($"Consulting response ({request.Intent}, confidence={result.ConfidenceLevel:F2}) in {elapsed:F0}ms");

                return FormatResponse(result, elapsed);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error in consulting handler: {request.Intent}");
                return new ConversationResponse
                {
                    Message = "I encountered an issue preparing that advisory. Could you try rephrasing?",
                    ResponseType = ResponseType.Error
                };
            }
        }

        #region Entity Enrichment

        private void EnrichFromEntities(ConsultingResponse response, ConsultingRequest request)
        {
            if (request.Entities == null || request.Entities.Count == 0)
                return;

            var dimensionEntity = request.Entities.FirstOrDefault(e => e.Type == EntityType.DIMENSION);
            var materialEntity = request.Entities.FirstOrDefault(e => e.Type == EntityType.MATERIAL);
            var roomTypeEntity = request.Entities.FirstOrDefault(e => e.Type == EntityType.ROOM_TYPE);

            var sb = new StringBuilder();
            var hasContext = false;

            if (dimensionEntity != null)
            {
                sb.AppendLine($"Based on the specified dimension ({dimensionEntity.NormalizedValue}):");
                response.ExtractedParameters["DIMENSION"] = dimensionEntity.NormalizedValue;
                hasContext = true;
            }

            if (materialEntity != null)
            {
                sb.AppendLine($"For the specified material ({materialEntity.NormalizedValue}):");
                response.ExtractedParameters["MATERIAL"] = materialEntity.NormalizedValue;
                hasContext = true;
            }

            if (roomTypeEntity != null)
            {
                sb.AppendLine($"For room type: {roomTypeEntity.NormalizedValue}");
                response.ExtractedParameters["ROOM_TYPE"] = roomTypeEntity.NormalizedValue;
                hasContext = true;
            }

            if (hasContext)
            {
                response.ParametricContext = sb.ToString();
                response.ReasoningChain.Add(new ReasoningStep
                {
                    Source = "EntityExtraction",
                    Description = $"Extracted parameters: {string.Join(", ", response.ExtractedParameters.Keys)}",
                    Confidence = 0.9f
                });
            }
        }

        #endregion

        #region Cross-Domain Detection

        private void DetectCrossDomainReferences(ConsultingResponse response, ConsultingRequest request)
        {
            var query = request.UserQuery.ToLowerInvariant();
            var primaryDomain = request.Intent;

            foreach (var (domain, keywords) in CrossDomainKeywords)
            {
                if (string.Equals(domain, primaryDomain, StringComparison.OrdinalIgnoreCase))
                    continue;

                var matchedKeywords = keywords.Where(kw => query.Contains(kw)).ToList();
                if (matchedKeywords.Count > 0)
                {
                    var domainName = GetDomainName(domain) ?? domain;
                    response.CrossDomainReferences.Add(new CrossDomainReference
                    {
                        Domain = domainName,
                        Intent = domain,
                        MatchedKeywords = matchedKeywords,
                        Relevance = Math.Min(1.0f, matchedKeywords.Count * 0.4f)
                    });

                    response.ReasoningChain.Add(new ReasoningStep
                    {
                        Source = "CrossDomainDetector",
                        Description = $"Cross-domain link: {domainName} (keywords: {string.Join(", ", matchedKeywords)})",
                        Confidence = Math.Min(1.0f, matchedKeywords.Count * 0.4f)
                    });
                }
            }

            // Consider prior consulting domain for continuity
            if (request.ActiveDomain != null &&
                !string.Equals(request.ActiveDomain, primaryDomain, StringComparison.OrdinalIgnoreCase) &&
                CanHandle(request.ActiveDomain))
            {
                var priorDomainName = GetDomainName(request.ActiveDomain);
                if (priorDomainName != null &&
                    !response.CrossDomainReferences.Any(c => c.Intent == request.ActiveDomain))
                {
                    response.CrossDomainReferences.Add(new CrossDomainReference
                    {
                        Domain = priorDomainName,
                        Intent = request.ActiveDomain,
                        MatchedKeywords = new List<string> { "(prior topic)" },
                        Relevance = 0.3f
                    });
                }
            }
        }

        #endregion

        #region Confidence Computation

        private float ComputeConfidence(ConsultingResponse response)
        {
            if (response.ReasoningChain.Count == 0)
                return 0.5f;

            // Weighted average: later steps (reasoning enrichment) carry more weight
            float totalWeight = 0;
            float weightedSum = 0;

            for (int i = 0; i < response.ReasoningChain.Count; i++)
            {
                var step = response.ReasoningChain[i];
                float weight = 1.0f + (i * 0.5f); // increasing weight for deeper reasoning

                // Boost weight for certain sources
                weight *= step.Source switch
                {
                    "StandardsIntegration" => 1.5f,
                    "InferenceEngine" => 1.3f,
                    "ComplianceChecker" => 1.4f,
                    "MaterialIntelligence" => 1.3f,
                    "KnowledgeGraph" => 1.2f,
                    _ => 1.0f
                };

                weightedSum += step.Confidence * weight;
                totalWeight += weight;
            }

            return Math.Clamp(weightedSum / totalWeight, 0.0f, 1.0f);
        }

        #endregion

        #region Domain Handlers

        private Task<ConsultingResponse> HandleStructuralAsync(
            ConsultingRequest request, CancellationToken ct)
        {
            var response = new ConsultingResponse
            {
                Domain = "Structural Engineering",
                Standards = new List<string> { "ASCE 7", "ACI 318", "Eurocodes 2/3", "BS 6399" }
            };

            var sb = new StringBuilder();
            sb.AppendLine("Structural Advisory:");
            sb.AppendLine();

            var query = request.UserQuery.ToLowerInvariant();

            if (query.Contains("beam") || query.Contains("span"))
            {
                sb.AppendLine("Beam Sizing Guidance:");
                sb.AppendLine("  - Residential spans up to 6m: consider W200-W310 steel or 300x600mm RC");
                sb.AppendLine("  - Commercial spans 6-12m: W360-W530 steel or post-tensioned concrete");
                sb.AppendLine("  - Depth-to-span ratio: ~L/20 for simply supported, ~L/26 for continuous");
                sb.AppendLine();
                sb.AppendLine("Reference: ASCE 7 for load combinations, ACI 318 Ch.9 for concrete, AISC 360 for steel.");
                response.Suggestions = new List<string>
                {
                    "Check load combinations for this beam",
                    "Show deflection limits",
                    "Compare steel vs concrete options"
                };
                response.KnowledgeReferences.Add("Beam");
                response.KnowledgeReferences.Add("LoadPath");
            }
            else if (query.Contains("column") || query.Contains("load"))
            {
                sb.AppendLine("Column & Load Path Guidance:");
                sb.AppendLine("  - Ensure continuous load path from roof to foundation");
                sb.AppendLine("  - Slenderness ratio check: kL/r < 200 for steel, Pu/(0.85*f'c*Ag) for RC");
                sb.AppendLine("  - Typical residential: 200x200mm RC or W150 steel");
                sb.AppendLine("  - Commercial multi-story: 400x400mm+ RC or W250+ steel");
                sb.AppendLine();
                sb.AppendLine("Reference: ACI 318 Ch.10 for RC columns, AISC 360 Ch.E for steel compression.");
                response.Suggestions = new List<string>
                {
                    "Calculate column load for this level",
                    "Check seismic requirements",
                    "Review foundation sizing"
                };
                response.KnowledgeReferences.Add("Column");
                response.KnowledgeReferences.Add("Foundation");
            }
            else if (query.Contains("slab") || query.Contains("floor"))
            {
                sb.AppendLine("Slab Design Guidance:");
                sb.AppendLine("  - One-way slab: span/depth ~ L/28 (simply supported), L/33 (continuous)");
                sb.AppendLine("  - Two-way slab: span/depth ~ L/33 to L/40");
                sb.AppendLine("  - Minimum thickness for deflection: 125mm residential, 150mm commercial");
                sb.AppendLine("  - Post-tensioned slabs allow thinner sections for longer spans");
                sb.AppendLine();
                sb.AppendLine("Reference: ACI 318 Ch.8 for flexural design, Table 9.3.1.1 for minimum thickness.");
                response.Suggestions = new List<string>
                {
                    "Check punching shear at columns",
                    "Calculate reinforcement for this slab",
                    "Review fire rating requirements"
                };
                response.KnowledgeReferences.Add("Slab");
                response.KnowledgeReferences.Add("FloorSystem");
            }
            else
            {
                sb.AppendLine("I can advise on structural topics including:");
                sb.AppendLine("  - Beam and column sizing");
                sb.AppendLine("  - Slab design and thickness");
                sb.AppendLine("  - Load path analysis");
                sb.AppendLine("  - Seismic and wind design considerations");
                sb.AppendLine("  - Foundation recommendations");
                sb.AppendLine();
                sb.AppendLine("What structural aspect would you like guidance on?");
                response.Suggestions = new List<string>
                {
                    "Beam sizing for a 6m span",
                    "Column loads for 3-story building",
                    "Slab thickness recommendation"
                };
            }

            response.Message = sb.ToString();
            return Task.FromResult(response);
        }

        private Task<ConsultingResponse> HandleMEPAsync(
            ConsultingRequest request, CancellationToken ct)
        {
            var response = new ConsultingResponse
            {
                Domain = "MEP Engineering",
                Standards = new List<string> { "ASHRAE 90.1", "ASHRAE 62.1", "SMACNA", "IMC 2021" }
            };

            var sb = new StringBuilder();
            sb.AppendLine("MEP Advisory:");
            sb.AppendLine();

            var query = request.UserQuery.ToLowerInvariant();

            if (query.Contains("duct") || query.Contains("hvac") || query.Contains("air"))
            {
                sb.AppendLine("Duct Sizing Guidance:");
                sb.AppendLine("  - Max velocity: 5 m/s (residential), 7.5 m/s (commercial main), 10 m/s (industrial)");
                sb.AppendLine("  - Friction rate: 0.8-1.2 Pa/m for low-velocity systems");
                sb.AppendLine("  - Equal friction method recommended for branch sizing");
                sb.AppendLine("  - Aspect ratio should not exceed 4:1 for rectangular ducts");
                sb.AppendLine();
                sb.AppendLine("Reference: ASHRAE Fundamentals Ch.21, SMACNA Duct Design standards.");
                response.Suggestions = new List<string>
                {
                    "Calculate duct size for this zone",
                    "Check ventilation rates",
                    "Review insulation requirements"
                };
                response.KnowledgeReferences.Add("HVAC");
                response.KnowledgeReferences.Add("Ductwork");
            }
            else if (query.Contains("pipe") || query.Contains("plumbing") || query.Contains("water"))
            {
                sb.AppendLine("Pipe Sizing Guidance:");
                sb.AppendLine("  - Domestic water: velocity 0.6-2.4 m/s, pressure drop < 4 kPa/m");
                sb.AppendLine("  - Drainage: slope 1:40 for 100mm, 1:60 for 150mm pipes");
                sb.AppendLine("  - Hot water recirculation for runs > 15m");
                sb.AppendLine("  - Fixture unit method for supply sizing per IPC 2021");
                sb.AppendLine();
                sb.AppendLine("Reference: IPC 2021, ASHRAE Fundamentals Ch.22, local plumbing codes.");
                response.Suggestions = new List<string>
                {
                    "Size supply pipe for fixture count",
                    "Check drainage slope requirements",
                    "Review water storage sizing"
                };
                response.KnowledgeReferences.Add("Plumbing");
                response.KnowledgeReferences.Add("Piping");
            }
            else if (query.Contains("electrical") || query.Contains("power") || query.Contains("lighting"))
            {
                sb.AppendLine("Electrical Systems Guidance:");
                sb.AppendLine("  - Lighting power density: 9-12 W/m2 (office), 15 W/m2 (retail)");
                sb.AppendLine("  - Socket outlets: one per 4m2 min, dedicated circuits for equipment > 2kW");
                sb.AppendLine("  - Cable sizing: consider voltage drop < 4% (BS 7671) or 5% (NEC)");
                sb.AppendLine("  - Emergency lighting: min 1 lux on escape routes, 0.5 lux open areas");
                sb.AppendLine();
                sb.AppendLine("Reference: NEC 2023, BS 7671, IEEE standards.");
                response.Suggestions = new List<string>
                {
                    "Calculate electrical load for this floor",
                    "Check lighting levels",
                    "Review emergency power requirements"
                };
                response.KnowledgeReferences.Add("Electrical");
                response.KnowledgeReferences.Add("Lighting");
            }
            else
            {
                sb.AppendLine("I can advise on MEP topics including:");
                sb.AppendLine("  - HVAC duct and equipment sizing");
                sb.AppendLine("  - Plumbing pipe sizing and drainage");
                sb.AppendLine("  - Electrical load calculations and cable sizing");
                sb.AppendLine("  - Ventilation rates and air quality");
                sb.AppendLine("  - Fire suppression systems");
                sb.AppendLine();
                sb.AppendLine("What MEP aspect would you like guidance on?");
                response.Suggestions = new List<string>
                {
                    "HVAC duct sizing for office",
                    "Plumbing pipe sizing",
                    "Electrical load calculation"
                };
            }

            response.Message = sb.ToString();
            return Task.FromResult(response);
        }

        private Task<ConsultingResponse> HandleComplianceAsync(
            ConsultingRequest request, CancellationToken ct)
        {
            var response = new ConsultingResponse
            {
                Domain = "Code Compliance",
                Standards = new List<string> { "IBC 2021", "Local codes", "ISO 19650" }
            };

            var sb = new StringBuilder();
            sb.AppendLine("Compliance Advisory:");
            sb.AppendLine();

            var query = request.UserQuery.ToLowerInvariant();

            if (query.Contains("fire") || query.Contains("egress") || query.Contains("exit"))
            {
                sb.AppendLine("Fire & Egress Compliance:");
                sb.AppendLine("  - Exit access travel distance: 60m (sprinklered), 45m (unsprinklered) per IBC");
                sb.AppendLine("  - Minimum 2 exits when occupant load > 49");
                sb.AppendLine("  - Exit width: 7.6mm per occupant (stairs), 5mm per occupant (other)");
                sb.AppendLine("  - Fire separation: 1hr between dwelling units, 2hr for exit stairs");
                response.Suggestions = new List<string>
                {
                    "Check travel distances in this plan",
                    "Verify exit capacity",
                    "Review fire separation requirements"
                };
                response.KnowledgeReferences.Add("Egress");
                response.KnowledgeReferences.Add("FireSeparation");
            }
            else if (query.Contains("setback") || query.Contains("zoning") || query.Contains("height"))
            {
                sb.AppendLine("Zoning & Building Envelope:");
                sb.AppendLine("  - Check local zoning for: setbacks, height limits, FAR, lot coverage");
                sb.AppendLine("  - Building height measured from grade to mean roof height");
                sb.AppendLine("  - Type of construction determines allowable height and area per IBC Ch.5");
                sb.AppendLine("  - Sprinklers can increase allowable area by up to 300%");
                response.Suggestions = new List<string>
                {
                    "Check building height classification",
                    "Calculate floor area ratio",
                    "Review construction type limits"
                };
                response.KnowledgeReferences.Add("Zoning");
                response.KnowledgeReferences.Add("BuildingEnvelope");
            }
            else
            {
                sb.AppendLine("I can help check compliance with:");
                sb.AppendLine("  - IBC 2021 building code requirements");
                sb.AppendLine("  - Fire and life safety codes (NFPA)");
                sb.AppendLine("  - Zoning and building envelope restrictions");
                sb.AppendLine("  - Accessibility requirements (ADA/local)");
                sb.AppendLine("  - Regional standards (EAC, ECOWAS, KEBS, SANS, UNBS)");
                sb.AppendLine();
                sb.AppendLine("What compliance aspect should I review?");
                response.Suggestions = new List<string>
                {
                    "Check fire egress compliance",
                    "Review zoning setbacks",
                    "Verify accessibility requirements"
                };
            }

            response.Message = sb.ToString();
            return Task.FromResult(response);
        }

        private Task<ConsultingResponse> HandleMaterialsAsync(
            ConsultingRequest request, CancellationToken ct)
        {
            var response = new ConsultingResponse
            {
                Domain = "Materials Selection",
                Standards = new List<string> { "ASTM", "BS EN", "ISO material standards" }
            };

            var sb = new StringBuilder();
            sb.AppendLine("Materials Advisory:");
            sb.AppendLine();

            var query = request.UserQuery.ToLowerInvariant();

            if (query.Contains("concrete") || query.Contains("cement"))
            {
                sb.AppendLine("Concrete Selection:");
                sb.AppendLine("  - C20/25: foundations, non-structural elements");
                sb.AppendLine("  - C25/30: general structural, residential slabs and beams");
                sb.AppendLine("  - C30/37: commercial structural, exposed concrete");
                sb.AppendLine("  - C40/50+: high-rise, pre-stressed elements, aggressive environments");
                sb.AppendLine("  - Exposure classes: XC1-XC4 (carbonation), XS1-XS3 (chlorides)");
                sb.AppendLine();
                sb.AppendLine("Reference: EN 206, ACI 318 Ch.19, ASTM C150.");
                response.Suggestions = new List<string>
                {
                    "Recommend concrete grade for this element",
                    "Check exposure class requirements",
                    "Compare mix designs"
                };
                response.KnowledgeReferences.Add("Concrete");
            }
            else if (query.Contains("steel") || query.Contains("metal"))
            {
                sb.AppendLine("Steel Selection:");
                sb.AppendLine("  - S235/A36: general structural, light-duty framing");
                sb.AppendLine("  - S355/A572-50: primary structural members, heavy loads");
                sb.AppendLine("  - S460+: high-strength applications, long spans");
                sb.AppendLine("  - Weathering steel (Corten): exposed architectural, bridges");
                sb.AppendLine("  - Stainless steel: coastal, high-corrosion environments");
                sb.AppendLine();
                sb.AppendLine("Reference: ASTM A992 (W-shapes), EN 10025, AISC 360.");
                response.Suggestions = new List<string>
                {
                    "Compare steel grades for this application",
                    "Check corrosion protection options",
                    "Review connection requirements"
                };
                response.KnowledgeReferences.Add("Steel");
            }
            else
            {
                sb.AppendLine("I can advise on material selection including:");
                sb.AppendLine("  - Concrete grades and mix design");
                sb.AppendLine("  - Structural steel selection");
                sb.AppendLine("  - Masonry and blockwork");
                sb.AppendLine("  - Insulation materials and thermal performance");
                sb.AppendLine("  - Finishes and cladding");
                sb.AppendLine("  - Regional material availability (Africa markets)");
                sb.AppendLine();
                sb.AppendLine("The materials database contains 2,450+ entries. What material do you need?");
                response.Suggestions = new List<string>
                {
                    "Recommend concrete grade",
                    "Steel selection for beams",
                    "Insulation material options"
                };
            }

            response.Message = sb.ToString();
            return Task.FromResult(response);
        }

        private Task<ConsultingResponse> HandleCostAsync(ConsultingRequest request, CancellationToken ct)
        {
            var response = new ConsultingResponse { Domain = "Cost Estimation", Standards = new List<string> { "RICS NRM", "Local cost databases" } };
            var sb = new StringBuilder();
            sb.AppendLine("Cost Advisory:");
            sb.AppendLine();
            var query = request.UserQuery.ToLowerInvariant();
            if (query.Contains("africa") || query.Contains("uganda") || query.Contains("kenya") || query.Contains("tanzania") || query.Contains("rwanda"))
            {
                sb.AppendLine("African Market Cost Indicators:");
                sb.AppendLine("  - Residential (basic): $350-550/m2");
                sb.AppendLine("  - Residential (mid-range): $550-900/m2");
                sb.AppendLine("  - Commercial (office): $600-1,200/m2");
                sb.AppendLine("  - Industrial (warehouse): $300-500/m2");
                sb.AppendLine("  - Costs vary significantly by location and material supply chain");
                sb.AppendLine();
                sb.AppendLine("Note: Import duties on steel/glass can add 15-35% to material costs.");
                response.Suggestions = new List<string> { "Estimate cost for this building area", "Compare local vs imported materials", "Review cost breakdown by element" };
                response.KnowledgeReferences.Add("AfricaCosts");
            }
            else
            {
                sb.AppendLine("I can help estimate costs for:");
                sb.AppendLine("  - Elemental cost plans (structure, envelope, MEP, finishes)");
                sb.AppendLine("  - Material quantity takeoffs");
                sb.AppendLine("  - Regional cost benchmarks (East/West Africa, international)");
                sb.AppendLine("  - Value engineering alternatives");
                sb.AppendLine("  - Life cycle cost analysis");
                sb.AppendLine();
                sb.AppendLine("Provide the building type and region for specific estimates.");
                response.Suggestions = new List<string> { "Estimate cost for this project", "Cost comparison for Africa", "Value engineering options" };
            }
            response.Message = sb.ToString();
            return Task.FromResult(response);
        }

        private Task<ConsultingResponse> HandleSustainabilityAsync(ConsultingRequest request, CancellationToken ct)
        {
            var response = new ConsultingResponse { Domain = "Sustainability & Green Building", Standards = new List<string> { "LEED v4.1", "BREEAM", "Green Star Africa" } };
            var sb = new StringBuilder();
            sb.AppendLine("Sustainability Advisory:");
            sb.AppendLine();
            var query = request.UserQuery.ToLowerInvariant();
            if (query.Contains("leed") || query.Contains("certification") || query.Contains("rating"))
            {
                sb.AppendLine("LEED v4.1 Key Credit Areas:");
                sb.AppendLine("  - Energy & Atmosphere (33 pts): optimize energy performance, renewables");
                sb.AppendLine("  - Indoor Environmental Quality (16 pts): daylight, ventilation, acoustics");
                sb.AppendLine("  - Water Efficiency (11 pts): reduce water use 20-50%");
                sb.AppendLine("  - Materials & Resources (13 pts): recycled content, regional materials");
                sb.AppendLine("  - Certification: Certified 40-49, Silver 50-59, Gold 60-79, Platinum 80+");
                response.Suggestions = new List<string> { "Identify LEED opportunities for this project", "Check energy credit requirements", "Review water efficiency strategies" };
                response.KnowledgeReferences.Add("LEED");
                response.KnowledgeReferences.Add("GreenBuilding");
            }
            else
            {
                sb.AppendLine("I can advise on sustainability including:");
                sb.AppendLine("  - LEED/BREEAM/Green Star certification pathways");
                sb.AppendLine("  - Passive design strategies (orientation, shading, ventilation)");
                sb.AppendLine("  - Renewable energy integration");
                sb.AppendLine("  - Water harvesting and greywater recycling");
                sb.AppendLine("  - Tropical climate sustainability (Africa-specific)");
                sb.AppendLine();
                sb.AppendLine("What sustainability goals are you targeting?");
                response.Suggestions = new List<string> { "LEED certification guidance", "Passive cooling strategies", "Renewable energy options" };
            }
            response.Message = sb.ToString();
            return Task.FromResult(response);
        }

        private Task<ConsultingResponse> HandleFireSafetyAsync(ConsultingRequest request, CancellationToken ct)
        {
            var response = new ConsultingResponse { Domain = "Fire Safety", Standards = new List<string> { "NFPA 101", "NFPA 13", "IBC Ch.7-10", "BS 9999" } };
            var sb = new StringBuilder();
            sb.AppendLine("Fire Safety Advisory:");
            sb.AppendLine();
            var query = request.UserQuery.ToLowerInvariant();
            if (query.Contains("rating") || query.Contains("resistance") || query.Contains("separation"))
            {
                sb.AppendLine("Fire Resistance Ratings:");
                sb.AppendLine("  - Load-bearing walls: 1-4 hr depending on building type");
                sb.AppendLine("  - Floor assemblies: 1 hr (residential), 2 hr (high-rise)");
                sb.AppendLine("  - Exit stair enclosures: 1 hr (< 4 stories), 2 hr (4+ stories)");
                sb.AppendLine("  - Shaft enclosures: 1-2 hr");
                sb.AppendLine("  - Occupancy separations: per IBC Table 508.4");
                response.Suggestions = new List<string> { "Check fire rating for this assembly", "Review compartmentation requirements", "Verify sprinkler trade-offs" };
                response.KnowledgeReferences.Add("FireResistance");
            }
            else if (query.Contains("sprinkler") || query.Contains("suppression"))
            {
                sb.AppendLine("Fire Suppression Guidance:");
                sb.AppendLine("  - NFPA 13: full sprinkler coverage for most commercial/residential");
                sb.AppendLine("  - Light hazard: 4.1 mm/min over 139 m2 design area");
                sb.AppendLine("  - Ordinary hazard Group 1: 6.1 mm/min over 139 m2");
                sb.AppendLine("  - Sprinklers allow: reduced fire ratings, increased area, longer travel");
                response.Suggestions = new List<string> { "Design sprinkler layout for this area", "Check hazard classification", "Review water supply requirements" };
                response.KnowledgeReferences.Add("Sprinkler");
            }
            else
            {
                sb.AppendLine("I can advise on fire safety including:");
                sb.AppendLine("  - Fire resistance ratings for assemblies");
                sb.AppendLine("  - Sprinkler system design criteria");
                sb.AppendLine("  - Means of egress requirements");
                sb.AppendLine("  - Fire compartmentation and separation");
                sb.AppendLine("  - Smoke control and detection");
                sb.AppendLine();
                sb.AppendLine("What fire safety aspect do you need guidance on?");
                response.Suggestions = new List<string> { "Fire rating requirements", "Sprinkler design criteria", "Egress compliance check" };
            }
            response.Message = sb.ToString();
            return Task.FromResult(response);
        }

        private Task<ConsultingResponse> HandleAccessibilityAsync(ConsultingRequest request, CancellationToken ct)
        {
            var response = new ConsultingResponse { Domain = "Accessibility", Standards = new List<string> { "ADA Standards", "IBC Ch.11", "BS 8300", "ISO 21542" } };
            var sb = new StringBuilder();
            sb.AppendLine("Accessibility Advisory:");
            sb.AppendLine();
            var query = request.UserQuery.ToLowerInvariant();
            if (query.Contains("ramp") || query.Contains("slope") || query.Contains("wheelchair"))
            {
                sb.AppendLine("Ramp & Circulation Requirements:");
                sb.AppendLine("  - Maximum slope: 1:12 (8.33%), preferred 1:20 (5%)");
                sb.AppendLine("  - Maximum run between landings: 9m");
                sb.AppendLine("  - Minimum width: 900mm (1200mm preferred)");
                sb.AppendLine("  - Level landings: 1500mm x 1500mm at top, bottom, and turns");
                sb.AppendLine("  - Handrails: both sides, 865-965mm height, 300mm extensions");
                response.Suggestions = new List<string> { "Check ramp compliance for this entrance", "Review corridor widths", "Verify door clearances" };
                response.KnowledgeReferences.Add("Ramp");
                response.KnowledgeReferences.Add("AccessibleRoute");
            }
            else if (query.Contains("door") || query.Contains("entrance") || query.Contains("opening"))
            {
                sb.AppendLine("Accessible Door Requirements:");
                sb.AppendLine("  - Minimum clear width: 815mm (single leaf), 915mm preferred");
                sb.AppendLine("  - Maneuvering clearance: 1525mm pull side, 1220mm push side");
                sb.AppendLine("  - Threshold height: 13mm max (exterior), 6mm max (interior)");
                sb.AppendLine("  - Door hardware: lever handles, 865-1220mm above floor");
                sb.AppendLine("  - Closing speed: minimum 5 seconds from 90 degrees to 12 degrees");
                response.Suggestions = new List<string> { "Check door clearances in this plan", "Review hardware specifications", "Verify accessible route continuity" };
                response.KnowledgeReferences.Add("AccessibleDoor");
            }
            else
            {
                sb.AppendLine("I can advise on accessibility including:");
                sb.AppendLine("  - Ramps, slopes, and level changes");
                sb.AppendLine("  - Door widths and maneuvering clearances");
                sb.AppendLine("  - Accessible toilet/bathroom design");
                sb.AppendLine("  - Wayfinding and signage");
                sb.AppendLine("  - Lift/elevator requirements");
                sb.AppendLine();
                sb.AppendLine("What accessibility requirement do you need to check?");
                response.Suggestions = new List<string> { "Ramp design requirements", "Accessible door clearances", "Accessible toilet layout" };
            }
            response.Message = sb.ToString();
            return Task.FromResult(response);
        }

        private Task<ConsultingResponse> HandleEnergyAsync(ConsultingRequest request, CancellationToken ct)
        {
            var response = new ConsultingResponse { Domain = "Energy Efficiency", Standards = new List<string> { "ASHRAE 90.1", "CIBSE Guide A", "EPBD", "SANS 10400-XA" } };
            var sb = new StringBuilder();
            sb.AppendLine("Energy Efficiency Advisory:");
            sb.AppendLine();
            var query = request.UserQuery.ToLowerInvariant();
            if (query.Contains("insulation") || query.Contains("u-value") || query.Contains("thermal"))
            {
                sb.AppendLine("Thermal Performance Guidance:");
                sb.AppendLine("  - Walls: U-value 0.18-0.35 W/m2K (climate dependent)");
                sb.AppendLine("  - Roof: U-value 0.13-0.25 W/m2K");
                sb.AppendLine("  - Floor: U-value 0.15-0.25 W/m2K");
                sb.AppendLine("  - Windows: U-value 1.2-2.0 W/m2K (double/triple glazing)");
                sb.AppendLine("  - Tropical climates: prioritize solar heat gain control (SHGC < 0.25)");
                sb.AppendLine();
                sb.AppendLine("Reference: ASHRAE 90.1 Table 5.5, CIBSE Guide A Ch.3.");
                response.Suggestions = new List<string> { "Check wall U-value for this climate", "Compare glazing options", "Review insulation thickness needed" };
                response.KnowledgeReferences.Add("ThermalPerformance");
                response.KnowledgeReferences.Add("Insulation");
            }
            else
            {
                sb.AppendLine("I can advise on energy performance including:");
                sb.AppendLine("  - Building envelope thermal performance (U-values, SHGC)");
                sb.AppendLine("  - HVAC energy optimization");
                sb.AppendLine("  - Lighting power density targets");
                sb.AppendLine("  - Renewable energy sizing (PV, solar thermal)");
                sb.AppendLine("  - Tropical climate strategies (passive cooling, shading)");
                sb.AppendLine();
                sb.AppendLine("What energy aspect are you optimizing?");
                response.Suggestions = new List<string> { "Insulation and U-values", "HVAC energy optimization", "Solar panel sizing" };
            }
            response.Message = sb.ToString();
            return Task.FromResult(response);
        }

        private Task<ConsultingResponse> HandleAcousticsAsync(ConsultingRequest request, CancellationToken ct)
        {
            var response = new ConsultingResponse { Domain = "Acoustics", Standards = new List<string> { "ASTM E90", "BS 8233", "CIBSE Guide B4", "WHO guidelines" } };
            var sb = new StringBuilder();
            sb.AppendLine("Acoustics Advisory:");
            sb.AppendLine();
            var query = request.UserQuery.ToLowerInvariant();
            if (query.Contains("wall") || query.Contains("partition") || query.Contains("stc") || query.Contains("rw"))
            {
                sb.AppendLine("Wall Sound Insulation:");
                sb.AppendLine("  - Between dwellings: STC 50+ (Rw 53+ dB) minimum");
                sb.AppendLine("  - Office partitions: STC 40-45 (normal privacy)");
                sb.AppendLine("  - Conference rooms: STC 50-55 (confidential speech)");
                sb.AppendLine("  - Plant rooms: STC 55-60+");
                sb.AppendLine("  - Single stud + 2x plasterboard: ~STC 35-40");
                sb.AppendLine("  - Double stud + insulation: ~STC 55-60");
                response.Suggestions = new List<string> { "Recommend wall build-up for STC 50", "Check floor impact insulation", "Review background noise criteria" };
                response.KnowledgeReferences.Add("SoundInsulation");
                response.KnowledgeReferences.Add("WallAssembly");
            }
            else
            {
                sb.AppendLine("I can advise on acoustics including:");
                sb.AppendLine("  - Airborne sound insulation (STC/Rw ratings)");
                sb.AppendLine("  - Impact sound insulation (IIC/Ln,w ratings)");
                sb.AppendLine("  - Background noise criteria (NC/NR curves)");
                sb.AppendLine("  - Room acoustics (reverberation time, speech intelligibility)");
                sb.AppendLine("  - MEP noise and vibration control");
                sb.AppendLine();
                sb.AppendLine("What acoustic performance are you targeting?");
                response.Suggestions = new List<string> { "Wall sound insulation ratings", "Background noise criteria", "Reverberation time targets" };
            }
            response.Message = sb.ToString();
            return Task.FromResult(response);
        }

        private Task<ConsultingResponse> HandleDaylightingAsync(ConsultingRequest request, CancellationToken ct)
        {
            var response = new ConsultingResponse { Domain = "Daylighting", Standards = new List<string> { "CIBSE LG10", "LEED v4.1 EQ", "EN 17037", "BS 8206-2" } };
            var sb = new StringBuilder();
            sb.AppendLine("Daylighting Advisory:");
            sb.AppendLine();
            var query = request.UserQuery.ToLowerInvariant();
            if (query.Contains("window") || query.Contains("glazing") || query.Contains("ratio"))
            {
                sb.AppendLine("Window & Glazing for Daylight:");
                sb.AppendLine("  - Window-to-floor ratio: 15-25% for adequate daylight");
                sb.AppendLine("  - Window-to-wall ratio: 30-60% typical, balance with thermal");
                sb.AppendLine("  - Effective daylight zone: ~2x window head height into room");
                sb.AppendLine("  - Visible light transmittance (VLT): 50-70% for daylight glazing");
                sb.AppendLine("  - LEED EQ credit: 300 lux for 50%+ of regularly occupied area");
                response.Suggestions = new List<string> { "Check daylight factor for this room", "Optimize window placement", "Review glare control options" };
                response.KnowledgeReferences.Add("Daylight");
                response.KnowledgeReferences.Add("Glazing");
            }
            else
            {
                sb.AppendLine("I can advise on daylighting including:");
                sb.AppendLine("  - Daylight factor targets and calculations");
                sb.AppendLine("  - Window sizing and placement optimization");
                sb.AppendLine("  - Glare control and shading strategies");
                sb.AppendLine("  - Light shelf and reflector design");
                sb.AppendLine("  - Tropical daylighting (high solar altitude management)");
                sb.AppendLine();
                sb.AppendLine("What daylighting aspect are you designing for?");
                response.Suggestions = new List<string> { "Window sizing for daylight", "Daylight factor targets", "Glare control strategies" };
            }
            response.Message = sb.ToString();
            return Task.FromResult(response);
        }

        private Task<ConsultingResponse> HandleSitePlanningAsync(ConsultingRequest request, CancellationToken ct)
        {
            var response = new ConsultingResponse { Domain = "Site Planning", Standards = new List<string> { "Local zoning", "IBC Ch.33", "ASCE 7 Ch.26" } };
            var sb = new StringBuilder();
            sb.AppendLine("Site Planning Advisory:");
            sb.AppendLine();
            var query = request.UserQuery.ToLowerInvariant();
            if (query.Contains("parking") || query.Contains("vehicle") || query.Contains("car"))
            {
                sb.AppendLine("Parking Design Guidance:");
                sb.AppendLine("  - Standard stall: 2.5m x 5.0m (perpendicular)");
                sb.AppendLine("  - Accessible stall: 3.6m x 5.0m with 1.5m access aisle");
                sb.AppendLine("  - Drive aisle: 6.0m (two-way), 3.6m (one-way)");
                sb.AppendLine("  - Typical ratio: 1 per 30m2 GFA (office), 1 per 20m2 (retail)");
                sb.AppendLine("  - Accessible parking: 1 per 25 spaces up to 100, then 1 per 50");
                response.Suggestions = new List<string> { "Calculate parking requirement for this project", "Layout parking for this site", "Check accessible parking count" };
                response.KnowledgeReferences.Add("Parking");
            }
            else if (query.Contains("orientation") || query.Contains("solar") || query.Contains("wind"))
            {
                sb.AppendLine("Building Orientation Guidance:");
                sb.AppendLine("  - Long axis east-west to minimize solar gain on large facades");
                sb.AppendLine("  - Primary glazing on north/south (tropics: minimize east/west)");
                sb.AppendLine("  - Service areas buffer prevailing wind direction");
                sb.AppendLine("  - Consider natural ventilation pathways");
                sb.AppendLine("  - In Africa: optimize for passive cooling, minimize western exposure");
                response.Suggestions = new List<string> { "Analyze solar exposure for this site", "Review wind patterns", "Optimize building orientation" };
                response.KnowledgeReferences.Add("SolarOrientation");
            }
            else
            {
                sb.AppendLine("I can advise on site planning including:");
                sb.AppendLine("  - Building orientation and solar optimization");
                sb.AppendLine("  - Parking layout and requirements");
                sb.AppendLine("  - Setbacks and building envelope constraints");
                sb.AppendLine("  - Stormwater management");
                sb.AppendLine("  - Landscaping and site circulation");
                sb.AppendLine();
                sb.AppendLine("What site planning aspect do you need help with?");
                response.Suggestions = new List<string> { "Parking requirements", "Building orientation advice", "Setback requirements" };
            }
            response.Message = sb.ToString();
            return Task.FromResult(response);
        }

        #endregion

        #region BIM Management Handlers

        private async Task<ConsultingResponse> HandleDesignAnalysisAsync(
            ConsultingRequest request, CancellationToken ct)
        {
            var response = new ConsultingResponse
            {
                Domain = "Design Analysis",
                Standards = new List<string> { "ISO 19650", "Design review best practices" }
            };

            var sb = new StringBuilder();
            sb.AppendLine("Design Analysis:");
            sb.AppendLine();

            var query = request.UserQuery.ToLowerInvariant();

            if (query.Contains("spatial") || query.Contains("quality") || query.Contains("proportion"))
            {
                sb.AppendLine("Spatial Quality Analysis:");
                sb.AppendLine("  - Aspect ratio evaluation: ideal range 1:1 to 1:1.618 (golden ratio)");
                sb.AppendLine("  - Ceiling height assessment: 2.7m ideal for residential, 3.0m+ commercial");
                sb.AppendLine("  - Proportion scoring: combined spatial quality 0-1 scale");
                sb.AppendLine("  - Room dimension validation against building code minimums");
                sb.AppendLine();
                sb.AppendLine("The SpatialReasoner analyzes geometry, adjacencies, and circulation paths.");
                response.Suggestions = new List<string>
                {
                    "Check spatial quality for this room",
                    "Analyze circulation efficiency",
                    "Review adjacency relationships"
                };
                response.KnowledgeReferences.Add("SpatialQuality");
                response.KnowledgeReferences.Add("DesignAnalysis");
            }
            else if (query.Contains("circulation") || query.Contains("flow") || query.Contains("path"))
            {
                sb.AppendLine("Circulation Analysis:");
                sb.AppendLine("  - Path efficiency ratio: direct distance / actual path length");
                sb.AppendLine("  - Bottleneck detection: corridors < 1.5m flagged as constrained");
                sb.AppendLine("  - Reachability assessment: % of rooms accessible from entry");
                sb.AppendLine("  - Obstruction identification along movement paths");
                sb.AppendLine();
                sb.AppendLine("Efficient circulation targets: > 0.7 efficiency ratio, all rooms reachable.");
                response.Suggestions = new List<string>
                {
                    "Analyze circulation between rooms",
                    "Identify bottlenecks",
                    "Suggest corridor improvements"
                };
                response.KnowledgeReferences.Add("Circulation");
                response.KnowledgeReferences.Add("PathAnalysis");
            }
            else if (query.Contains("completeness") || query.Contains("missing") || query.Contains("incomplete"))
            {
                sb.AppendLine("Design Completeness Review:");
                sb.AppendLine("  - Room type checklist against project type requirements");
                sb.AppendLine("  - Missing element detection (e.g., bathroom without plumbing)");
                sb.AppendLine("  - Adjacency requirement verification");
                sb.AppendLine("  - Pattern matching: functional zoning, circulation, efficiency");
                sb.AppendLine();
                sb.AppendLine("Residential minimum: Living, Kitchen, Bedroom, Bathroom");
                sb.AppendLine("Commercial minimum: Reception, Office, WC, Services");
                response.Suggestions = new List<string>
                {
                    "Check what's missing in this design",
                    "Verify room type completeness",
                    "Review adjacency requirements"
                };
                response.KnowledgeReferences.Add("Completeness");
                response.KnowledgeReferences.Add("DesignReview");
            }
            else
            {
                sb.AppendLine("I can perform intelligent design analysis including:");
                sb.AppendLine("  - Spatial quality assessment (proportions, ceiling height, aspect ratio)");
                sb.AppendLine("  - Circulation path analysis (efficiency, bottlenecks, reachability)");
                sb.AppendLine("  - Design completeness review (missing rooms, elements, patterns)");
                sb.AppendLine("  - Adjacency and relationship verification");
                sb.AppendLine("  - Design pattern recognition (11 built-in patterns)");
                sb.AppendLine("  - Collision and overlap detection");
                sb.AppendLine();
                sb.AppendLine("What aspect of your design would you like analyzed?");
                response.Suggestions = new List<string>
                {
                    "Analyze spatial quality",
                    "Check circulation paths",
                    "Review design completeness"
                };
            }

            response.Message = sb.ToString();
            return response;
        }

        private async Task<ConsultingResponse> HandleOptimizationAsync(
            ConsultingRequest request, CancellationToken ct)
        {
            var response = new ConsultingResponse
            {
                Domain = "Design Optimization",
                Standards = new List<string> { "Multi-objective optimization", "Genetic algorithm" }
            };

            var sb = new StringBuilder();
            sb.AppendLine("Design Optimization:");
            sb.AppendLine();

            var query = request.UserQuery.ToLowerInvariant();

            if (query.Contains("layout") || query.Contains("arrangement") || query.Contains("floor plan"))
            {
                sb.AppendLine("Layout Optimization (Genetic Algorithm):");
                sb.AppendLine("  Objectives evaluated (weighted):");
                sb.AppendLine("  - Space efficiency (25%): net area / gross area ratio");
                sb.AppendLine("  - Adjacency satisfaction (30%): required adjacencies met");
                sb.AppendLine("  - Natural light access (20%): rooms with exterior wall access");
                sb.AppendLine("  - Circulation efficiency (15%): accessible rooms / total");
                sb.AppendLine("  - Compactness (10%): convex hull / bounding box ratio");
                sb.AppendLine();
                sb.AppendLine("  Constraints enforced:");
                sb.AppendLine("  - No room overlaps (hard constraint)");
                sb.AppendLine("  - All rooms within building boundary");
                sb.AppendLine("  - Minimum room sizes (9m² bedroom, 5m² kitchen, 12m² living)");
                sb.AppendLine();
                sb.AppendLine("The optimizer runs 100 generations with population of 50 candidates.");
                response.Suggestions = new List<string>
                {
                    "Optimize this floor layout",
                    "Suggest room placement improvements",
                    "Compare layout alternatives"
                };
                response.KnowledgeReferences.Add("LayoutOptimization");
                response.KnowledgeReferences.Add("GeneticAlgorithm");
            }
            else if (query.Contains("placement") || query.Contains("position") || query.Contains("where"))
            {
                sb.AppendLine("Room Placement Optimization:");
                sb.AppendLine("  - Grid-based candidate position generation");
                sb.AppendLine("  - Adjacency requirement scoring per position");
                sb.AppendLine("  - Collision avoidance with existing elements");
                sb.AppendLine("  - Top 3 alternative positions returned with scores");
                sb.AppendLine("  - Natural light access prioritized for habitable rooms");
                sb.AppendLine();
                sb.AppendLine("Provide room type and dimensions for placement recommendations.");
                response.Suggestions = new List<string>
                {
                    "Where to place the next room",
                    "Best position for bathroom",
                    "Optimize kitchen placement"
                };
                response.KnowledgeReferences.Add("RoomPlacement");
            }
            else if (query.Contains("improvement") || query.Contains("issue") || query.Contains("problem"))
            {
                sb.AppendLine("Design Improvement Identification:");
                sb.AppendLine("  Priority levels:");
                sb.AppendLine("  [HIGH] Space efficiency < 75% - reduce wasted circulation area");
                sb.AppendLine("  [MEDIUM] Adjacency violations - kitchen far from dining, etc.");
                sb.AppendLine("  [MEDIUM] Dark rooms - interior rooms needing natural light");
                sb.AppendLine("  [LOW] Circulation efficiency < 70% - add central hallway");
                sb.AppendLine("  [LOW] Room proportions > 2.5:1 aspect ratio - subdivide/adjust");
                sb.AppendLine();
                sb.AppendLine("Each improvement includes impact score and specific recommendations.");
                response.Suggestions = new List<string>
                {
                    "Find improvement opportunities",
                    "Check for layout issues",
                    "Prioritize design fixes"
                };
                response.KnowledgeReferences.Add("DesignImprovement");
            }
            else
            {
                sb.AppendLine("I can optimize your design using multi-objective algorithms:");
                sb.AppendLine("  - Full layout optimization (genetic algorithm, 5 objectives)");
                sb.AppendLine("  - Room placement optimization (best position from candidates)");
                sb.AppendLine("  - Improvement identification (5 priority categories)");
                sb.AppendLine("  - Space efficiency analysis and enhancement");
                sb.AppendLine("  - Adjacency and circulation optimization");
                sb.AppendLine();
                sb.AppendLine("What would you like to optimize?");
                response.Suggestions = new List<string>
                {
                    "Optimize layout",
                    "Best room placement",
                    "Find improvement opportunities"
                };
            }

            response.Message = sb.ToString();
            return response;
        }

        private async Task<ConsultingResponse> HandleDecisionSupportAsync(
            ConsultingRequest request, CancellationToken ct)
        {
            var response = new ConsultingResponse
            {
                Domain = "Decision Support",
                Standards = new List<string> { "Multi-criteria decision analysis", "ISO 31000 Risk" }
            };

            var sb = new StringBuilder();
            sb.AppendLine("Decision Support Analysis:");
            sb.AppendLine();

            var query = request.UserQuery.ToLowerInvariant();

            if (query.Contains("structural") || query.Contains("frame") || query.Contains("system"))
            {
                sb.AppendLine("Structural System Decision Template:");
                sb.AppendLine("  Alternatives: RC Frame, Steel Frame, Composite, Timber, Masonry");
                sb.AppendLine("  Criteria (18 across 6 categories):");
                sb.AppendLine("  - Cost: initial $/m², lifecycle 30yr, operating annual");
                sb.AppendLine("  - Performance: energy kWh/m²/yr, thermal comfort %, daylight %");
                sb.AppendLine("  - Sustainability: embodied carbon kgCO2e/m², operational carbon");
                sb.AppendLine("  - Schedule: construction months, material lead time weeks");
                sb.AppendLine("  - Quality: durability years, flexibility score 1-10");
                sb.AppendLine("  - Risk: construction risk, supply chain risk");
                sb.AppendLine();
                sb.AppendLine("Trade-off guidance: 5% initial cost increase per 10% energy improvement.");
                response.Suggestions = new List<string>
                {
                    "Compare RC vs steel frame",
                    "Risk assessment for steel",
                    "Sensitivity analysis on cost weighting"
                };
                response.KnowledgeReferences.Add("StructuralSystem");
                response.KnowledgeReferences.Add("DecisionAnalysis");
            }
            else if (query.Contains("facade") || query.Contains("cladding") || query.Contains("envelope"))
            {
                sb.AppendLine("Facade System Decision Template:");
                sb.AppendLine("  Alternatives: Brick Veneer, Curtain Wall, Precast Concrete,");
                sb.AppendLine("                Rainscreen, EIFS");
                sb.AppendLine("  Key trade-offs:");
                sb.AppendLine("  - Cost vs durability: 2% cost increase per year of added life");
                sb.AppendLine("  - Daylight vs energy: complex relationship requiring WWR optimization");
                sb.AppendLine("  - Flexibility vs cost: 8% premium for adaptable systems");
                sb.AppendLine();
                sb.AppendLine("Risk factors: material availability, construction complexity, regulatory path.");
                response.Suggestions = new List<string>
                {
                    "Compare facade systems",
                    "Analyze cost-durability trade-off",
                    "Assess supply chain risk"
                };
                response.KnowledgeReferences.Add("FacadeSystem");
                response.KnowledgeReferences.Add("EnvelopeDesign");
            }
            else if (query.Contains("hvac") || query.Contains("mechanical") || query.Contains("cooling"))
            {
                sb.AppendLine("HVAC System Decision Template:");
                sb.AppendLine("  Alternatives: VAV, VRF, Chilled Beam, DOAS+Radiant, Natural Ventilation");
                sb.AppendLine("  Key considerations:");
                sb.AppendLine("  - Energy: VAV baseline, VRF 30% less, Natural Vent 60% less");
                sb.AppendLine("  - Initial cost: Natural Vent lowest, Chilled Beam highest");
                sb.AppendLine("  - Flexibility: VRF high (zoning), Chilled Beam low");
                sb.AppendLine("  - Climate suitability: Natural Vent for mild/tropical only");
                sb.AppendLine();
                sb.AppendLine("Africa-specific: consider power reliability, passive strategies first.");
                response.Suggestions = new List<string>
                {
                    "Compare HVAC systems for tropical climate",
                    "Trade-off analysis: cost vs energy",
                    "Natural ventilation feasibility"
                };
                response.KnowledgeReferences.Add("HVACSystem");
                response.KnowledgeReferences.Add("MechanicalDesign");
            }
            else if (query.Contains("compare") || query.Contains("alternative") || query.Contains("option"))
            {
                sb.AppendLine("Multi-Criteria Comparison:");
                sb.AppendLine("  Analysis method: Weighted scoring with constraint penalties");
                sb.AppendLine("  Sensitivity: Test ranking stability across ±20% weight changes");
                sb.AppendLine("  Pareto frontier: Identify non-dominated solutions");
                sb.AppendLine("  Risk: 4 categories (supply chain, technical, regulatory, financial)");
                sb.AppendLine();
                sb.AppendLine("  Available decision templates:");
                sb.AppendLine("  - Structural system (RC, Steel, Composite, Timber, Masonry)");
                sb.AppendLine("  - Facade system (Brick, Curtain, Precast, Rainscreen, EIFS)");
                sb.AppendLine("  - HVAC system (VAV, VRF, Chilled Beam, DOAS+Radiant, Nat. Vent)");
                sb.AppendLine();
                sb.AppendLine("Specify what you'd like to compare for a detailed analysis.");
                response.Suggestions = new List<string>
                {
                    "Compare structural systems",
                    "Compare facade options",
                    "Compare HVAC systems"
                };
                response.KnowledgeReferences.Add("MultiCriteria");
            }
            else
            {
                sb.AppendLine("I provide structured decision support with:");
                sb.AppendLine("  - Multi-criteria evaluation (18 criteria, 6 categories)");
                sb.AppendLine("  - Sensitivity analysis (how weight changes affect ranking)");
                sb.AppendLine("  - Trade-off visualization (Pareto frontier analysis)");
                sb.AppendLine("  - Risk assessment (supply chain, technical, regulatory, financial)");
                sb.AppendLine("  - Decision reports with justified recommendations");
                sb.AppendLine();
                sb.AppendLine("Pre-configured templates: structural, facade, and HVAC systems.");
                sb.AppendLine("What decision would you like help with?");
                response.Suggestions = new List<string>
                {
                    "Compare structural systems",
                    "Evaluate facade options",
                    "HVAC system selection"
                };
            }

            response.Message = sb.ToString();
            return response;
        }

        private async Task<ConsultingResponse> HandleValidationAsync(
            ConsultingRequest request, CancellationToken ct)
        {
            var response = new ConsultingResponse
            {
                Domain = "Design Validation",
                Standards = new List<string> { "IBC 2021", "ADA", "NFPA", "ISO 19650" }
            };

            var sb = new StringBuilder();
            sb.AppendLine("Design Validation:");
            sb.AppendLine();

            var query = request.UserQuery.ToLowerInvariant();

            if (query.Contains("collision") || query.Contains("overlap") || query.Contains("clash"))
            {
                sb.AppendLine("Collision & Clash Detection:");
                sb.AppendLine("  - AABB (axis-aligned bounding box) intersection testing");
                sb.AppendLine("  - 50mm tolerance for adjacent elements (not true overlaps)");
                sb.AppendLine("  - Multi-element batch checking support");
                sb.AppendLine("  - Reports: colliding element pairs with overlap dimensions");
                sb.AppendLine();
                sb.AppendLine("Common clashes: MEP through structure, wall-door misalignment,");
                sb.AppendLine("furniture clearance violations, ceiling-duct conflicts.");
                response.Suggestions = new List<string>
                {
                    "Check for element collisions",
                    "Validate MEP clearances",
                    "Review furniture clearances"
                };
                response.KnowledgeReferences.Add("ClashDetection");
                response.KnowledgeReferences.Add("Coordination");
            }
            else if (query.Contains("code") || query.Contains("compliance") || query.Contains("regulation"))
            {
                sb.AppendLine("Multi-Code Compliance Validation:");
                sb.AppendLine("  Available profiles: IBC 2021, UKBR, EAC (East African Community)");
                sb.AppendLine("  Built-in rules (13):");
                sb.AppendLine("  - Room dimensions (min area, min dimension, max height)");
                sb.AppendLine("  - Egress (travel distance, exit width, door swing)");
                sb.AppendLine("  - Fire separation (rating, compartment size)");
                sb.AppendLine("  - Accessibility (corridor width, ramp slope, door clearance)");
                sb.AppendLine("  - Structural (load path, deflection, clearance)");
                sb.AppendLine();
                sb.AppendLine("Region auto-detection: East Africa, UK, International (IBC).");
                response.Suggestions = new List<string>
                {
                    "Run full compliance check",
                    "Check egress compliance",
                    "Validate accessibility requirements"
                };
                response.KnowledgeReferences.Add("ComplianceValidation");
                response.KnowledgeReferences.Add("BuildingCode");
            }
            else if (query.Contains("adjacency") || query.Contains("relationship") || query.Contains("neighbor"))
            {
                sb.AppendLine("Adjacency Validation:");
                sb.AppendLine("  - Required adjacencies: kitchen-dining, bedroom-bathroom");
                sb.AppendLine("  - Prohibited adjacencies: bedroom-plant room, kitchen-bathroom");
                sb.AppendLine("  - Proximity threshold: 0.3m for adjacent, 3m for near");
                sb.AppendLine("  - Direction analysis: relative positions of connected spaces");
                sb.AppendLine();
                sb.AppendLine("Standard adjacency requirements checked per room type and code.");
                response.Suggestions = new List<string>
                {
                    "Check adjacency requirements",
                    "Validate kitchen-dining proximity",
                    "Review prohibited adjacencies"
                };
                response.KnowledgeReferences.Add("Adjacency");
                response.KnowledgeReferences.Add("SpatialRelationship");
            }
            else
            {
                sb.AppendLine("I can validate your design against multiple criteria:");
                sb.AppendLine("  - Collision/clash detection (element overlap, clearance violations)");
                sb.AppendLine("  - Multi-code compliance (IBC, UKBR, EAC - 13 built-in rules)");
                sb.AppendLine("  - Adjacency verification (required/prohibited relationships)");
                sb.AppendLine("  - Spatial quality validation (proportions, dimensions, heights)");
                sb.AppendLine("  - Design completeness (missing rooms, elements, services)");
                sb.AppendLine("  - Pattern compliance (functional zoning, circulation, efficiency)");
                sb.AppendLine();
                sb.AppendLine("What would you like me to validate?");
                response.Suggestions = new List<string>
                {
                    "Check for clashes",
                    "Run compliance check",
                    "Validate adjacencies"
                };
            }

            response.Message = sb.ToString();
            return response;
        }

        private async Task<ConsultingResponse> HandleDesignPatternsAsync(
            ConsultingRequest request, CancellationToken ct)
        {
            var response = new ConsultingResponse
            {
                Domain = "Design Patterns & Best Practices",
                Standards = new List<string> { "Architectural best practices", "Building typology" }
            };

            var sb = new StringBuilder();
            sb.AppendLine("Design Patterns & Best Practices:");
            sb.AppendLine();

            var query = request.UserQuery.ToLowerInvariant();

            if (query.Contains("residential") || query.Contains("house") || query.Contains("home"))
            {
                sb.AppendLine("Residential Design Patterns (4 recognized):");
                sb.AppendLine();
                sb.AppendLine("  Functional Zoning (RES-FUNC-ZONE):");
                sb.AppendLine("    Public zone (living, dining, kitchen) near entry");
                sb.AppendLine("    Private zone (bedrooms, study) separated");
                sb.AppendLine("    Service zone (laundry, storage) accessible but discrete");
                sb.AppendLine("    Requires: min 4 rooms including Living, Bedroom, Kitchen, Bathroom");
                sb.AppendLine();
                sb.AppendLine("  Master Suite (RES-MASTER-SUITE):");
                sb.AppendLine("    Master bedroom with en-suite bathroom + walk-in closet");
                sb.AppendLine("    Adjacency requirement: direct access between rooms");
                sb.AppendLine();
                sb.AppendLine("  Kitchen Triangle (RES-KITCHEN-TRIANGLE):");
                sb.AppendLine("    Sink-stove-fridge triangle: 3.6-8m perimeter, max 2.7m per leg");
                sb.AppendLine();
                sb.AppendLine("  Open Plan (RES-OPEN-PLAN):");
                sb.AppendLine("    Combined living/dining/kitchen: min 30m² total");
                response.Suggestions = new List<string>
                {
                    "Check if my design matches residential patterns",
                    "Apply functional zoning to my layout",
                    "Suggest missing elements"
                };
                response.KnowledgeReferences.Add("ResidentialPatterns");
                response.KnowledgeReferences.Add("FunctionalZoning");
            }
            else if (query.Contains("circulation") || query.Contains("corridor") || query.Contains("hallway"))
            {
                sb.AppendLine("Circulation Patterns (2 recognized):");
                sb.AppendLine();
                sb.AppendLine("  Central Hall (CIRC-CENTRAL-HALL):");
                sb.AppendLine("    Corridor-based room access");
                sb.AppendLine("    Min width: 1.2m residential, 1.5m commercial");
                sb.AppendLine("    Efficient for privacy, linear organization");
                sb.AppendLine();
                sb.AppendLine("  Enfilade (CIRC-ENFILADE):");
                sb.AppendLine("    Direct room-to-room sequence (no corridors)");
                sb.AppendLine("    Min 3 connected rooms");
                sb.AppendLine("    Efficient for open-plan, gallery, exhibition spaces");
                sb.AppendLine();
                sb.AppendLine("  Efficiency Patterns:");
                sb.AppendLine("    Wet Stack (EFF-WET-STACK): vertical bathroom/kitchen alignment");
                sb.AppendLine("    Back-to-Back (EFF-BACK-TO-BACK): shared wet walls");
                response.Suggestions = new List<string>
                {
                    "Analyze my circulation pattern",
                    "Suggest corridor placement",
                    "Check wet stack alignment"
                };
                response.KnowledgeReferences.Add("CirculationPatterns");
                response.KnowledgeReferences.Add("EfficiencyPatterns");
            }
            else if (query.Contains("spatial") || query.Contains("double height") || query.Contains("light"))
            {
                sb.AppendLine("Spatial Quality Patterns (2 recognized):");
                sb.AppendLine();
                sb.AppendLine("  Double Height (SPATIAL-DOUBLE-HEIGHT):");
                sb.AppendLine("    Two-story volume: min 5m ceiling height");
                sb.AppendLine("    Best for: living room, foyer, gallery");
                sb.AppendLine("    Creates dramatic spatial experience, natural ventilation");
                sb.AppendLine();
                sb.AppendLine("  Borrowed Light (SPATIAL-BORROWED-LIGHT):");
                sb.AppendLine("    Interior rooms lit via glazed partitions");
                sb.AppendLine("    Maximizes daylight penetration without exterior walls");
                sb.AppendLine("    Useful for corridor walls, internal offices");
                response.Suggestions = new List<string>
                {
                    "Check spatial quality of rooms",
                    "Identify opportunities for borrowed light",
                    "Analyze double height feasibility"
                };
                response.KnowledgeReferences.Add("SpatialQuality");
                response.KnowledgeReferences.Add("NaturalLight");
            }
            else
            {
                sb.AppendLine("I recognize 11 design patterns across 4 categories:");
                sb.AppendLine("  Residential (4): Functional Zoning, Master Suite, Kitchen Triangle, Open Plan");
                sb.AppendLine("  Circulation (2): Central Hall, Enfilade");
                sb.AppendLine("  Spatial (2): Double Height, Borrowed Light");
                sb.AppendLine("  Efficiency (2): Wet Stack, Back-to-Back");
                sb.AppendLine();
                sb.AppendLine("For each pattern I can:");
                sb.AppendLine("  - Recognize if your design matches");
                sb.AppendLine("  - Suggest applicable patterns for your project type");
                sb.AppendLine("  - Identify missing elements to complete a pattern");
                sb.AppendLine("  - Learn from your feedback to improve suggestions");
                sb.AppendLine();
                sb.AppendLine("What type of project are you designing?");
                response.Suggestions = new List<string>
                {
                    "Residential design patterns",
                    "Circulation patterns",
                    "Spatial quality patterns"
                };
            }

            response.Message = sb.ToString();
            return response;
        }

        private async Task<ConsultingResponse> HandlePredictiveAsync(
            ConsultingRequest request, CancellationToken ct)
        {
            var response = new ConsultingResponse
            {
                Domain = "Predictive Design Guidance",
                Standards = new List<string> { "Workflow best practices", "Design sequence patterns" }
            };

            var sb = new StringBuilder();
            sb.AppendLine("Predictive Design Guidance:");
            sb.AppendLine();

            var query = request.UserQuery.ToLowerInvariant();

            if (query.Contains("next") || query.Contains("step") || query.Contains("then"))
            {
                sb.AppendLine("Next Steps Prediction:");
                sb.AppendLine("  The predictive engine learns from your workflow to anticipate next actions.");
                sb.AppendLine();
                sb.AppendLine("  Common design sequences recognized:");
                sb.AppendLine("  1. Room Creation: Wall x4 → Floor → Room tag → Door → Window x2");
                sb.AppendLine("  2. Door Placement: After wall creation → door addition");
                sb.AppendLine("  3. Bathroom Setup: Room → Set type → Add plumbing → Add fixtures");
                sb.AppendLine("  4. Window Sequence: After room → window placement (typically 2)");
                sb.AppendLine("  5. Duplicate & Offset: Select → Copy → Paste for repetitive elements");
                sb.AppendLine();
                sb.AppendLine("  Prediction confidence increases as you work and the engine learns your patterns.");
                response.Suggestions = new List<string>
                {
                    "What should I do next?",
                    "Suggest workflow for this room",
                    "Show common design sequences"
                };
                response.KnowledgeReferences.Add("WorkflowPrediction");
                response.KnowledgeReferences.Add("DesignSequence");
            }
            else if (query.Contains("workflow") || query.Contains("sequence") || query.Contains("process"))
            {
                sb.AppendLine("Design Workflow Guidance:");
                sb.AppendLine();
                sb.AppendLine("  Recommended residential workflow:");
                sb.AppendLine("  1. Set building grid and levels");
                sb.AppendLine("  2. Create exterior walls (boundary)");
                sb.AppendLine("  3. Place interior partitions (rooms)");
                sb.AppendLine("  4. Add doors and windows");
                sb.AppendLine("  5. Tag rooms and set types");
                sb.AppendLine("  6. Review adjacencies and circulation");
                sb.AppendLine("  7. Add MEP rough-in (plumbing, electrical)");
                sb.AppendLine("  8. Run compliance checks");
                sb.AppendLine("  9. Generate schedules and quantities");
                sb.AppendLine();
                sb.AppendLine("  The engine adapts suggestions based on your progress and design context.");
                response.Suggestions = new List<string>
                {
                    "Start residential workflow",
                    "What step am I on?",
                    "Skip to MEP coordination"
                };
                response.KnowledgeReferences.Add("DesignWorkflow");
            }
            else if (query.Contains("suggest") || query.Contains("recommend") || query.Contains("help"))
            {
                sb.AppendLine("Proactive Design Suggestions:");
                sb.AppendLine("  The system provides 4 types of suggestions:");
                sb.AppendLine();
                sb.AppendLine("  Pattern Completion (High priority):");
                sb.AppendLine("    Detects partially completed design patterns");
                sb.AppendLine("    Example: 3 walls placed → 'Complete the room with 4th wall and floor'");
                sb.AppendLine();
                sb.AppendLine("  Common Follow-Ups (Medium priority):");
                sb.AppendLine("    Based on typical sequences: wall → door, room → window");
                sb.AppendLine();
                sb.AppendLine("  Context-Based (Medium priority):");
                sb.AppendLine("    Based on selected elements: selected wall → add opening");
                sb.AppendLine();
                sb.AppendLine("  Getting Started (Low priority):");
                sb.AppendLine("    For new/empty projects: create walls, set up grid");
                response.Suggestions = new List<string>
                {
                    "Show suggestions for current state",
                    "What patterns am I completing?",
                    "Recommend next design action"
                };
                response.KnowledgeReferences.Add("ProactiveSuggestions");
            }
            else
            {
                sb.AppendLine("I provide intelligent predictive guidance including:");
                sb.AppendLine("  - Next action prediction (Markov chain + learned patterns)");
                sb.AppendLine("  - Design workflow recommendations (phase-by-phase)");
                sb.AppendLine("  - Proactive suggestions (pattern completion, follow-ups)");
                sb.AppendLine("  - Auto-completion for commands and parameters");
                sb.AppendLine("  - Parameter prediction (most common values per action type)");
                sb.AppendLine();
                sb.AppendLine("Default parameter predictions:");
                sb.AppendLine("  - Wall: 4m length, 2.7m height, 200mm thickness");
                sb.AppendLine("  - Room: 4x4m, 2.7m height");
                sb.AppendLine("  - Door: 0.9m wide, 2.1m high, single flush");
                sb.AppendLine("  - Window: 1.2m wide, 1.5m high, 0.9m sill");
                sb.AppendLine();
                sb.AppendLine("What guidance do you need?");
                response.Suggestions = new List<string>
                {
                    "Predict next steps",
                    "Show design workflow",
                    "Get proactive suggestions"
                };
            }

            response.Message = sb.ToString();
            return response;
        }

        #endregion

        #region Response Formatting

        private ConversationResponse FormatResponse(ConsultingResponse result, double processingMs)
        {
            var sb = new StringBuilder();

            // Parametric context header
            if (!string.IsNullOrEmpty(result.ParametricContext))
            {
                sb.AppendLine(result.ParametricContext);
            }

            // Main advisory message
            sb.Append(result.Message);

            // Standards footer
            if (result.Standards != null && result.Standards.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"Applicable standards: {string.Join(", ", result.Standards)}");
            }

            // Knowledge enrichment
            if (result.KnowledgeInsights.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Knowledge insights:");
                foreach (var insight in result.KnowledgeInsights)
                {
                    sb.AppendLine($"  - {insight}");
                }
            }

            // Inference results
            if (result.InferredFacts.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Inferred recommendations:");
                foreach (var fact in result.InferredFacts)
                {
                    sb.AppendLine($"  - {fact}");
                }
            }

            // Standards requirements from integration
            if (result.StandardsRequirements.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Specific requirements:");
                foreach (var req in result.StandardsRequirements)
                {
                    sb.AppendLine($"  - {req}");
                }
            }

            // Cross-domain references
            if (result.CrossDomainReferences.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Related considerations:");
                foreach (var xref in result.CrossDomainReferences)
                {
                    sb.AppendLine($"  - {xref.Domain}: ask me about {string.Join(", ", xref.MatchedKeywords)}");
                }
            }

            // Confidence indicator
            var confidenceLabel = result.ConfidenceLevel switch
            {
                >= 0.85f => "High",
                >= 0.65f => "Good",
                >= 0.45f => "Moderate",
                _ => "Low"
            };
            sb.AppendLine();
            sb.AppendLine($"Advisory confidence: {confidenceLabel} ({result.ConfidenceLevel:P0})");

            return new ConversationResponse
            {
                Message = sb.ToString(),
                ResponseType = ResponseType.Information,
                Suggestions = result.Suggestions,
                ProcessingTimeMs = processingMs
            };
        }

        #endregion
    }

    #region Reasoning Pipeline

    /// <summary>
    /// Coordinates enrichment of consulting responses using Knowledge Graph,
    /// Inference Engine, Standards Integration, and domain-specific reasoners.
    /// All dependencies are optional - gracefully degrades to static advice when absent.
    /// </summary>
    internal class ConsultingReasoningPipeline
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly KnowledgeGraph _knowledgeGraph;
        private readonly InferenceEngine _inferenceEngine;
        private readonly StandardsIntegration _standardsIntegration;
        private readonly ComplianceChecker _complianceChecker;
        private readonly MaterialIntelligence _materialIntelligence;
        private readonly SpatialReasoner _spatialReasoner;
        private readonly DesignPatternRecognizer _patternRecognizer;
        private readonly DecisionSupport _decisionSupport;
        private readonly DesignOptimizer _designOptimizer;
        private readonly PredictiveEngine _predictiveEngine;

        public ConsultingReasoningPipeline(
            KnowledgeGraph knowledgeGraph,
            InferenceEngine inferenceEngine,
            StandardsIntegration standardsIntegration,
            ComplianceChecker complianceChecker,
            MaterialIntelligence materialIntelligence,
            SpatialReasoner spatialReasoner = null,
            DesignPatternRecognizer patternRecognizer = null,
            DecisionSupport decisionSupport = null,
            DesignOptimizer designOptimizer = null,
            PredictiveEngine predictiveEngine = null)
        {
            _knowledgeGraph = knowledgeGraph;
            _inferenceEngine = inferenceEngine;
            _standardsIntegration = standardsIntegration;
            _complianceChecker = complianceChecker;
            _materialIntelligence = materialIntelligence;
            _spatialReasoner = spatialReasoner;
            _patternRecognizer = patternRecognizer;
            _decisionSupport = decisionSupport;
            _designOptimizer = designOptimizer;
            _predictiveEngine = predictiveEngine;
        }

        /// <summary>
        /// Enriches the response with knowledge graph lookups and inference engine results.
        /// </summary>
        public async Task EnrichWithKnowledgeAsync(
            ConsultingResponse response,
            ConsultingRequest request,
            CancellationToken ct)
        {
            if (_knowledgeGraph == null || response.KnowledgeReferences.Count == 0)
                return;

            try
            {
                foreach (var concept in response.KnowledgeReferences.ToList())
                {
                    // Search for matching nodes in knowledge graph
                    var nodes = _knowledgeGraph.SearchNodes(concept, maxResults: 3);
                    foreach (var node in nodes)
                    {
                        // Get related concepts
                        var related = _knowledgeGraph.GetRelatedNodes(node.Id);
                        var relatedNames = related.Take(4).Select(r => r.Name).ToList();

                        if (relatedNames.Count > 0)
                        {
                            response.KnowledgeInsights.Add(
                                $"{node.Name}: related to {string.Join(", ", relatedNames)}");
                        }

                        // Get properties as additional context
                        if (node.Properties != null && node.Properties.Count > 0)
                        {
                            var props = node.Properties
                                .Take(3)
                                .Select(p => $"{p.Key}={p.Value}");
                            response.KnowledgeInsights.Add(
                                $"{node.Name} properties: {string.Join(", ", props)}");
                        }
                    }
                }

                // Run inference if engine is available
                if (_inferenceEngine != null && response.KnowledgeReferences.Count > 0)
                {
                    var primaryConcept = response.KnowledgeReferences.First();
                    var query = new InferenceQuery
                    {
                        Type = QueryType.Recommend,
                        Subject = primaryConcept
                    };

                    var answer = _inferenceEngine.AnswerQuery(query);
                    if (answer != null && answer.Answers != null)
                    {
                        foreach (var result in answer.Answers.Take(3))
                        {
                            response.InferredFacts.Add(result.Description);
                        }

                        response.ReasoningChain.Add(new ReasoningStep
                        {
                            Source = "InferenceEngine",
                            Description = $"Forward chaining on '{primaryConcept}': {answer.Answers.Count} inferences",
                            Confidence = answer.Confidence
                        });
                    }
                }

                response.ReasoningChain.Add(new ReasoningStep
                {
                    Source = "KnowledgeGraph",
                    Description = $"Queried {response.KnowledgeReferences.Count} concepts, found {response.KnowledgeInsights.Count} insights",
                    Confidence = response.KnowledgeInsights.Count > 0 ? 0.8f : 0.5f
                });
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Knowledge enrichment failed, continuing with static advice");
            }
        }

        /// <summary>
        /// Enriches the response with standards requirements from StandardsIntegration.
        /// </summary>
        public async Task EnrichWithStandardsAsync(
            ConsultingResponse response,
            ConsultingRequest request,
            CancellationToken ct)
        {
            if (_standardsIntegration == null)
                return;

            try
            {
                // Map domain to standards topic
                var topic = MapDomainToStandardsTopic(request.Intent);
                if (topic == null) return;

                // Get requirements for this topic
                var requirements = _standardsIntegration.GetRequirements(topic);
                if (requirements != null)
                {
                    foreach (var req in requirements.Requirements.Take(3))
                    {
                        response.StandardsRequirements.Add(
                            $"[{req.StandardCode} {req.Section}] {req.Description}");
                    }
                }

                // Get regional guidance if context provides region info
                var region = InferRegion(request);
                if (region != null)
                {
                    var regional = _standardsIntegration.GetRegionalGuidance(region, topic);
                    if (regional != null && regional.Adaptations != null)
                    {
                        foreach (var adaptation in regional.Adaptations.Take(2))
                        {
                            response.StandardsRequirements.Add($"[Regional - {region}] {adaptation}");
                        }
                    }
                }

                if (response.StandardsRequirements.Count > 0)
                {
                    response.ReasoningChain.Add(new ReasoningStep
                    {
                        Source = "StandardsIntegration",
                        Description = $"Found {response.StandardsRequirements.Count} applicable requirements for '{topic}'",
                        Confidence = 0.9f
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Standards enrichment failed, continuing with static advice");
            }
        }

        /// <summary>
        /// Enriches with domain-specific reasoning from ComplianceChecker, MaterialIntelligence,
        /// SpatialReasoner, DesignPatternRecognizer, DecisionSupport, and DesignOptimizer.
        /// </summary>
        public async Task EnrichWithDomainReasoningAsync(
            ConsultingResponse response,
            ConsultingRequest request,
            CancellationToken ct)
        {
            try
            {
                // Compliance reasoning for compliance/fire/accessibility intents
                if (_complianceChecker != null && IsComplianceRelated(request.Intent))
                {
                    await EnrichWithComplianceAsync(response, request, ct);
                }

                // Material reasoning for material intents
                if (_materialIntelligence != null &&
                    string.Equals(request.Intent, "CONSULT_MATERIALS", StringComparison.OrdinalIgnoreCase))
                {
                    EnrichWithMaterialIntelligence(response, request);
                }

                // Spatial reasoning enrichment for structural, site planning, and management intents
                if (_spatialReasoner != null && IsSpatialRelated(request.Intent))
                {
                    EnrichWithSpatialReasoning(response, request);
                }

                // Pattern recognition enrichment
                if (_patternRecognizer != null && IsPatternRelated(request.Intent))
                {
                    EnrichWithPatternRecognition(response, request);
                }

                // Design optimization enrichment
                if (_designOptimizer != null && IsOptimizationRelated(request.Intent))
                {
                    EnrichWithOptimization(response, request);
                }

                // Decision support enrichment
                if (_decisionSupport != null && IsDecisionRelated(request.Intent))
                {
                    EnrichWithDecisionSupport(response, request);
                }

                // Predictive suggestions enrichment
                if (_predictiveEngine != null)
                {
                    await EnrichWithPredictiveAsync(response, request, ct);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Domain reasoning enrichment failed, continuing with static advice");
            }
        }

        private void EnrichWithSpatialReasoning(ConsultingResponse response, ConsultingRequest request)
        {
            try
            {
                var entities = _spatialReasoner.GetEntities().ToList();
                if (entities.Count == 0) return;

                // Report spatial entity count and types for context
                var typeGroups = entities.GroupBy(e => e.EntityType)
                    .Select(g => $"{g.Key}: {g.Count()}");
                response.InferredFacts.Add($"Spatial model: {string.Join(", ", typeGroups)}");

                response.ReasoningChain.Add(new ReasoningStep
                {
                    Source = "SpatialReasoner",
                    Description = $"Spatial model has {entities.Count} entities for geometric analysis",
                    Confidence = 0.8f
                });
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Spatial reasoning enrichment failed");
            }
        }

        private void EnrichWithPatternRecognition(ConsultingResponse response, ConsultingRequest request)
        {
            try
            {
                var context = new DesignContext
                {
                    ProjectType = InferProjectType(request)
                };

                var suggestions = _patternRecognizer.SuggestPatterns(context, context.ProjectType);
                if (suggestions != null && suggestions.Count > 0)
                {
                    foreach (var suggestion in suggestions.Take(3))
                    {
                        response.InferredFacts.Add(
                            $"Applicable pattern: {suggestion.Pattern?.Name ?? "Unknown"} " +
                            $"(relevance: {suggestion.Relevance:P0})");
                    }

                    response.ReasoningChain.Add(new ReasoningStep
                    {
                        Source = "DesignPatternRecognizer",
                        Description = $"Suggested {suggestions.Count} design patterns for {context.ProjectType}",
                        Confidence = suggestions.Average(s => s.Relevance)
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Pattern recognition enrichment failed");
            }
        }

        private void EnrichWithOptimization(ConsultingResponse response, ConsultingRequest request)
        {
            try
            {
                response.InferredFacts.Add(
                    "Optimization available: 5 objectives (space efficiency, adjacency, " +
                    "natural light, circulation, compactness)");

                response.ReasoningChain.Add(new ReasoningStep
                {
                    Source = "DesignOptimizer",
                    Description = "Multi-objective optimization engine ready for layout analysis",
                    Confidence = 0.75f
                });
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Optimization enrichment failed");
            }
        }

        private void EnrichWithDecisionSupport(ConsultingResponse response, ConsultingRequest request)
        {
            try
            {
                var templates = _decisionSupport.GetAllTemplates();
                if (templates != null && templates.Count > 0)
                {
                    var templateNames = templates.Select(t => t.Name ?? t.Id).ToList();
                    response.InferredFacts.Add(
                        $"Decision templates available: {string.Join(", ", templateNames)}");

                    response.ReasoningChain.Add(new ReasoningStep
                    {
                        Source = "DecisionSupport",
                        Description = $"{templates.Count} decision analysis templates available",
                        Confidence = 0.8f
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Decision support enrichment failed");
            }
        }

        private async Task EnrichWithPredictiveAsync(
            ConsultingResponse response, ConsultingRequest request, CancellationToken ct)
        {
            try
            {
                var predictionContext = new PredictionContext();
                var suggestions = _predictiveEngine.GetProactiveSuggestions(predictionContext);
                var suggestionList = suggestions?.Take(3).ToList();
                if (suggestionList != null && suggestionList.Count > 0)
                {
                    response.InferredFacts.Add(
                        $"Predicted next steps: {string.Join("; ", suggestionList.Select(s => s.Title))}");

                    response.ReasoningChain.Add(new ReasoningStep
                    {
                        Source = "PredictiveEngine",
                        Description = $"Generated {suggestionList.Count} proactive design suggestions",
                        Confidence = suggestionList.Average(s => s.Confidence)
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Predictive enrichment failed");
            }
        }

        private async Task EnrichWithComplianceAsync(
            ConsultingResponse response,
            ConsultingRequest request,
            CancellationToken ct)
        {
            // Set code profile based on region context
            var region = InferRegion(request);
            if (region != null)
            {
                var profile = region switch
                {
                    "EAC" or "Uganda" or "Kenya" or "Tanzania" or "Rwanda" => "EAC",
                    "UK" => "UKBR",
                    _ => "IBC2021"
                };

                var profiles = _complianceChecker.GetAvailableProfiles();
                if (profiles.Any(p => string.Equals(p, profile, StringComparison.OrdinalIgnoreCase)))
                {
                    _complianceChecker.SetCodeProfile(profile);
                    response.InferredFacts.Add($"Using {profile} compliance profile for {region} region");
                }
            }

            response.ReasoningChain.Add(new ReasoningStep
            {
                Source = "ComplianceChecker",
                Description = $"Compliance checker activated for {request.Intent}",
                Confidence = 0.85f
            });
        }

        private void EnrichWithMaterialIntelligence(
            ConsultingResponse response,
            ConsultingRequest request)
        {
            var region = InferRegion(request) ?? "UK";
            var elementType = request.Entities
                ?.FirstOrDefault(e => e.Type == EntityType.ELEMENT_TYPE)?.NormalizedValue;

            var context = new MaterialSelectionContext
            {
                Region = region,
                ElementType = elementType ?? "General"
            };

            var recommendation = _materialIntelligence.GetRecommendation(context);
            if (recommendation?.PrimaryRecommendation != null)
            {
                response.InferredFacts.Add(
                    $"Recommended material: {recommendation.PrimaryRecommendation.MaterialId} " +
                    $"(score: {recommendation.PrimaryRecommendation.Score:F1}/10)");

                if (recommendation.Rationale != null)
                {
                    response.InferredFacts.Add($"Rationale: {recommendation.Rationale}");
                }

                if (recommendation.RegionalNotes != null)
                {
                    foreach (var note in recommendation.RegionalNotes.Take(2))
                    {
                        response.InferredFacts.Add($"Regional: {note}");
                    }
                }
            }

            response.ReasoningChain.Add(new ReasoningStep
            {
                Source = "MaterialIntelligence",
                Description = $"Material recommendation for region={region}, element={elementType ?? "General"}",
                Confidence = recommendation?.PrimaryRecommendation != null ? 0.85f : 0.5f
            });
        }

        private string MapDomainToStandardsTopic(string intent)
        {
            return intent switch
            {
                "CONSULT_STRUCTURAL" => "structural",
                "CONSULT_MEP" => "mechanical",
                "CONSULT_COMPLIANCE" => "building_code",
                "CONSULT_MATERIALS" => "materials",
                "CONSULT_FIRE_SAFETY" => "fire_safety",
                "CONSULT_ACCESSIBILITY" => "accessibility",
                "CONSULT_ENERGY" => "energy",
                "CONSULT_ACOUSTICS" => "acoustics",
                "CONSULT_DAYLIGHTING" => "lighting",
                "CONSULT_SUSTAINABILITY" => "sustainability",
                "CONSULT_SITE_PLANNING" => "site",
                "MANAGE_VALIDATION" => "building_code",
                "MANAGE_DESIGN_ANALYSIS" => "building_code",
                _ => null
            };
        }

        private bool IsComplianceRelated(string intent)
        {
            return intent is "CONSULT_COMPLIANCE" or "CONSULT_FIRE_SAFETY" or "CONSULT_ACCESSIBILITY"
                or "MANAGE_VALIDATION";
        }

        private bool IsSpatialRelated(string intent)
        {
            return intent is "CONSULT_STRUCTURAL" or "CONSULT_SITE_PLANNING"
                or "MANAGE_DESIGN_ANALYSIS" or "MANAGE_OPTIMIZATION" or "MANAGE_VALIDATION";
        }

        private bool IsPatternRelated(string intent)
        {
            return intent is "MANAGE_DESIGN_ANALYSIS" or "MANAGE_DESIGN_PATTERNS"
                or "MANAGE_OPTIMIZATION" or "MANAGE_VALIDATION";
        }

        private bool IsOptimizationRelated(string intent)
        {
            return intent is "MANAGE_OPTIMIZATION" or "MANAGE_DESIGN_ANALYSIS";
        }

        private bool IsDecisionRelated(string intent)
        {
            return intent is "MANAGE_DECISION_SUPPORT" or "CONSULT_STRUCTURAL"
                or "CONSULT_MATERIALS" or "CONSULT_MEP";
        }

        private string InferProjectType(ConsultingRequest request)
        {
            var query = request.UserQuery?.ToLowerInvariant() ?? "";
            if (query.Contains("residential") || query.Contains("house") || query.Contains("home")
                || query.Contains("apartment") || query.Contains("dwelling"))
                return "residential";
            if (query.Contains("commercial") || query.Contains("office") || query.Contains("retail"))
                return "commercial";
            if (query.Contains("industrial") || query.Contains("warehouse") || query.Contains("factory"))
                return "industrial";
            if (query.Contains("healthcare") || query.Contains("hospital") || query.Contains("clinic"))
                return "healthcare";
            if (query.Contains("education") || query.Contains("school") || query.Contains("university"))
                return "educational";
            return "residential"; // default
        }

        private string InferRegion(ConsultingRequest request)
        {
            var query = request.UserQuery?.ToLowerInvariant() ?? "";

            if (query.Contains("uganda") || query.Contains("kampala")) return "Uganda";
            if (query.Contains("kenya") || query.Contains("nairobi")) return "Kenya";
            if (query.Contains("tanzania") || query.Contains("dar es salaam")) return "Tanzania";
            if (query.Contains("rwanda") || query.Contains("kigali")) return "Rwanda";
            if (query.Contains("south africa") || query.Contains("johannesburg") || query.Contains("cape town")) return "South Africa";
            if (query.Contains("east africa") || query.Contains("eac")) return "EAC";
            if (query.Contains("uk") || query.Contains("british") || query.Contains("london")) return "UK";
            if (query.Contains("africa")) return "EAC";

            return null;
        }

        #region Phase 4 Handlers - BEP, DWG-to-BIM, Image-to-BIM, Generative Design

        private async Task<ConsultingResponse> HandleBEPAsync(ConsultingRequest request, CancellationToken ct)
        {
            var query = request.UserQuery?.ToLowerInvariant() ?? "";
            var bepGenerator = new BIMExecutionPlanGenerator();

            // Determine BEP type
            var bepType = query.Contains("post") ? "Post-Appointment BEP" : "Pre-Appointment BEP";

            var bepRequest = new BEPRequest
            {
                ProjectDescription = request.UserQuery,
                BEPType = bepType,
                Region = InferRegion(request)
            };

            var bep = await bepGenerator.GenerateBEPAsync(bepRequest, ct);
            var formattedBEP = bepGenerator.FormatBEPAsText(bep);

            var response = new ConsultingResponse
            {
                Domain = "BIM Execution Plan",
                Message = formattedBEP,
                ConfidenceLevel = 0.90f,
                Standards = new List<string> { "ISO 19650-1:2018", "ISO 19650-2:2018" },
                Suggestions = new List<string>
                {
                    "Review and customize the BEP for your specific project requirements",
                    "Ensure all stakeholders review and sign off on the BEP",
                    "Update the BEP at each project stage gate",
                    "Distribute to all appointed parties via the CDE"
                }
            };

            return response;
        }

        private async Task<ConsultingResponse> HandleDWGToBIMAsync(ConsultingRequest request, CancellationToken ct)
        {
            await Task.CompletedTask;
            var query = request.UserQuery?.ToLowerInvariant() ?? "";

            var sb = new StringBuilder();
            sb.AppendLine("DWG-TO-BIM CONVERSION GUIDANCE");
            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            sb.AppendLine();
            sb.AppendLine("StingBIM's DWG-to-BIM conversion engine can transform 2D AutoCAD");
            sb.AppendLine("drawings into 3D BIM element creation plans. Here's how it works:");
            sb.AppendLine();
            sb.AppendLine("SUPPORTED CONVERSIONS:");
            sb.AppendLine("───────────────────────────────────────────────────────────────");
            sb.AppendLine("  Architectural: Walls, Doors, Windows, Stairs, Elevators, Rooms");
            sb.AppendLine("  Structural:    Columns, Beams, Foundations, Structural Walls");
            sb.AppendLine("  Mechanical:    Ductwork, Diffusers, AHUs, Piping");
            sb.AppendLine("  Electrical:    Lighting, Receptacles, Panels, Conduit, Cable Tray");
            sb.AppendLine("  Plumbing:      Pipes, Fixtures (WC, Sinks, Showers)");
            sb.AppendLine("  Fire:          Sprinkler pipes, Heads, Alarm devices");
            sb.AppendLine("  Civil:         Topography, Roads, Property Lines");
            sb.AppendLine();
            sb.AppendLine("BEST PRACTICES FOR DWG INPUT:");
            sb.AppendLine("───────────────────────────────────────────────────────────────");
            sb.AppendLine("  1. Use AIA CAD Layer Guidelines (A-WALL, A-DOOR, S-COLS, etc.)");
            sb.AppendLine("  2. Standard block names for doors/windows (DOOR-900, WINDOW-1200x1500)");
            sb.AppendLine("  3. Consistent units (mm recommended)");
            sb.AppendLine("  4. Layer visibility - freeze layers not needed for conversion");
            sb.AppendLine("  5. Clean geometry - purge unused blocks and layers");
            sb.AppendLine();
            sb.AppendLine("CONVERSION PROCESS:");
            sb.AppendLine("───────────────────────────────────────────────────────────────");
            sb.AppendLine("  1. Layer classification (AIA standard + pattern matching)");
            sb.AppendLine("  2. Block identification (doors, windows, fixtures → families)");
            sb.AppendLine("  3. Wall analysis (geometry + thickness → wall types)");
            sb.AppendLine("  4. Grid and level extraction");
            sb.AppendLine("  5. Hosted element mapping (doors/windows to host walls)");
            sb.AppendLine("  6. Structural element detection (columns, beams)");
            sb.AppendLine("  7. MEP system routing (ducts, pipes, conduit)");
            sb.AppendLine("  8. Room detection from enclosed areas + text labels");
            sb.AppendLine("  9. Validation and confidence scoring");
            sb.AppendLine("  10. BIM creation plan output");
            sb.AppendLine();
            sb.AppendLine("To start a conversion, provide your DWG file and specify:");
            sb.AppendLine("  - Building type (residential, commercial, healthcare, etc.)");
            sb.AppendLine("  - Default floor-to-floor height");
            sb.AppendLine("  - Number of levels");
            sb.AppendLine("  - Any non-standard layer naming conventions used");

            return new ConsultingResponse
            {
                Domain = "DWG-to-BIM Conversion",
                Message = sb.ToString(),
                ConfidenceLevel = 0.88f,
                Standards = new List<string> { "AIA CAD Layer Guidelines", "NCS v6", "ISO 13567" },
                Suggestions = new List<string>
                {
                    "Ensure DWG layers follow AIA naming for best auto-classification",
                    "Use standard door/window blocks with size in the name",
                    "Review the conversion report and adjust wall types as needed",
                    "Post-conversion: verify shared coordinates and levels"
                }
            };
        }

        private async Task<ConsultingResponse> HandleImageToBIMAsync(ConsultingRequest request, CancellationToken ct)
        {
            await Task.CompletedTask;
            var query = request.UserQuery?.ToLowerInvariant() ?? "";

            var sb = new StringBuilder();
            sb.AppendLine("IMAGE/PDF-TO-BIM CONVERSION GUIDANCE");
            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            sb.AppendLine();
            sb.AppendLine("StingBIM's Image/PDF-to-BIM engine converts scanned floor plans,");
            sb.AppendLine("photographs, and PDF drawings into 3D BIM element creation plans");
            sb.AppendLine("using computer vision and spatial reasoning.");
            sb.AppendLine();
            sb.AppendLine("SUPPORTED INPUT FORMATS:");
            sb.AppendLine("───────────────────────────────────────────────────────────────");
            sb.AppendLine("  Images: PNG, JPG, TIFF (300+ DPI recommended)");
            sb.AppendLine("  PDFs:   Vectorized preferred, scanned supported");
            sb.AppendLine();
            sb.AppendLine("WHAT THE ENGINE DETECTS:");
            sb.AppendLine("───────────────────────────────────────────────────────────────");
            sb.AppendLine("  Walls:     Thick lines classified as exterior/interior/partition");
            sb.AppendLine("  Doors:     Arc symbols (single, double, sliding)");
            sb.AppendLine("  Windows:   Triple-line symbols within walls");
            sb.AppendLine("  Columns:   Filled circles/rectangles at grid intersections");
            sb.AppendLine("  Stairs:    Evenly-spaced parallel lines with arrows");
            sb.AppendLine("  Plumbing:  Toilet, sink, shower, bathtub symbols");
            sb.AppendLine("  Rooms:     Text labels within enclosed wall boundaries");
            sb.AppendLine("  Dimensions: OCR extraction of dimension text");
            sb.AppendLine();
            sb.AppendLine("TIPS FOR BEST RESULTS:");
            sb.AppendLine("───────────────────────────────────────────────────────────────");
            sb.AppendLine("  1. Use 300+ DPI scans (higher resolution = better accuracy)");
            sb.AppendLine("  2. Ensure even lighting and no shadows on the drawing");
            sb.AppendLine("  3. Include a scale bar or specify the drawing scale (e.g., 1:100)");
            sb.AppendLine("  4. Clear, legible room labels and dimensions");
            sb.AppendLine("  5. Standard architectural symbols for doors/windows");
            sb.AppendLine("  6. Black and white or high contrast preferred");
            sb.AppendLine();
            sb.AppendLine("CONVERSION PIPELINE:");
            sb.AppendLine("───────────────────────────────────────────────────────────────");
            sb.AppendLine("  1. Image preprocessing (deskew, contrast, noise removal)");
            sb.AppendLine("  2. Line detection (walls, grids)");
            sb.AppendLine("  3. Symbol recognition (doors, windows, fixtures)");
            sb.AppendLine("  4. OCR text extraction (room names, dimensions)");
            sb.AppendLine("  5. Room boundary detection (flood fill on enclosed areas)");
            sb.AppendLine("  6. Spatial reasoning (room classification, adjacency)");
            sb.AppendLine("  7. BIM element generation with types and materials");
            sb.AppendLine("  8. Quality assessment with confidence scores");
            sb.AppendLine();
            sb.AppendLine("Provide your floor plan image/PDF and specify the drawing scale.");

            return new ConsultingResponse
            {
                Domain = "Image/PDF-to-BIM Conversion",
                Message = sb.ToString(),
                ConfidenceLevel = 0.82f,
                Standards = new List<string> { "ISO 128 (Technical Drawings)", "AIA Drawing Conventions" },
                Suggestions = new List<string>
                {
                    "For best results, provide 300+ DPI scans with clear line work",
                    "Always specify the drawing scale (1:50, 1:100, 1:200)",
                    "DWG files produce higher accuracy than scanned images",
                    "Review detected elements and adjust types/sizes in the BIM model"
                }
            };
        }

        private async Task<ConsultingResponse> HandleGenerativeDesignAsync(ConsultingRequest request, CancellationToken ct)
        {
            await Task.CompletedTask;
            var query = request.UserQuery?.ToLowerInvariant() ?? "";

            var sb = new StringBuilder();
            sb.AppendLine("AI GENERATIVE DESIGN ENGINE");
            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            sb.AppendLine();
            sb.AppendLine("StingBIM's Enhanced Generative Design Engine creates optimized");
            sb.AppendLine("design variants using multi-objective evolutionary optimization");
            sb.AppendLine("enhanced with reasoning, knowledge, and intelligence layers.");
            sb.AppendLine();
            sb.AppendLine("CAPABILITIES:");
            sb.AppendLine("───────────────────────────────────────────────────────────────");
            sb.AppendLine("  • Multi-objective optimization (cost, energy, daylight, views, efficiency)");
            sb.AppendLine("  • Evolutionary algorithms (tournament, roulette, elite selection)");
            sb.AppendLine("  • KnowledgeGraph-backed typology pattern retrieval");
            sb.AppendLine("  • Causal reasoning for constraint derivation");
            sb.AppendLine("  • Spatial reasoning for adjacency and circulation quality");
            sb.AppendLine("  • Physics simulation scoring (daylight, acoustic, thermal)");
            sb.AppendLine("  • Standards compliance checking (IBC, ASHRAE, ADA, NFPA)");
            sb.AppendLine("  • Multi-agent consensus (Arch, Struct, MEP, Cost, Safety, Sustain)");
            sb.AppendLine("  • Design intent inference from context");
            sb.AppendLine("  • Pareto front analysis for trade-off visualization");
            sb.AppendLine("  • Sensitivity analysis on key design parameters");
            sb.AppendLine();
            sb.AppendLine("SUPPORTED BUILDING TYPES:");
            sb.AppendLine("───────────────────────────────────────────────────────────────");
            sb.AppendLine("  Office, Residential, Healthcare, Educational, Hospitality,");
            sb.AppendLine("  Industrial, Retail, Mixed-Use, Infrastructure");
            sb.AppendLine();
            sb.AppendLine("DESIGN PATTERNS:");
            sb.AppendLine("───────────────────────────────────────────────────────────────");
            sb.AppendLine("  Linear, Central Core, Cluster, Courtyard, Atrium, Open Plan,");
            sb.AppendLine("  Racetrack (Healthcare), Finger Plan (Education), Hotel Slab");
            sb.AppendLine();
            sb.AppendLine("HOW TO USE:");
            sb.AppendLine("───────────────────────────────────────────────────────────────");
            sb.AppendLine("  Provide: building type, total area (m²), number of floors,");
            sb.AppendLine("  room program, site constraints, climate zone, and any");
            sb.AppendLine("  specific design priorities (sustainability, views, cost, etc.)");
            sb.AppendLine();
            sb.AppendLine("  Example: 'Generate design variants for a 5-story, 8000m²");
            sb.AppendLine("  office building in Nairobi with emphasis on natural ventilation");
            sb.AppendLine("  and daylight, maximum budget $5M'");

            return new ConsultingResponse
            {
                Domain = "AI Generative Design",
                Message = sb.ToString(),
                ConfidenceLevel = 0.88f,
                Standards = new List<string> { "ISO 19650", "IBC 2021", "ASHRAE 90.1" },
                Suggestions = new List<string>
                {
                    "Provide a detailed room program for better variant generation",
                    "Specify climate zone and region for climate-responsive design",
                    "Set design priorities to weight the multi-objective optimization",
                    "Review the top 3-5 variants and use Pareto analysis for trade-offs"
                }
            };
        }

        #endregion
    }

    #endregion

    #region Supporting Types

    /// <summary>
    /// Request to the BIM consulting engine with session continuity support.
    /// </summary>
    public class ConsultingRequest
    {
        public string Intent { get; set; }
        public string UserQuery { get; set; }
        public List<ExtractedEntity> Entities { get; set; } = new List<ExtractedEntity>();
        public ConversationContext Context { get; set; }

        /// <summary>
        /// The consulting domain active in the current session (for follow-up continuity).
        /// </summary>
        public string ActiveDomain { get; set; }

        /// <summary>
        /// Prior consulting topics in this session for cross-domain awareness.
        /// </summary>
        public List<string> SessionTopicHistory { get; set; } = new List<string>();
    }

    /// <summary>
    /// Internal response from a domain handler, enriched by the reasoning pipeline.
    /// </summary>
    public class ConsultingResponse
    {
        public string Domain { get; set; }
        public string Message { get; set; }
        public List<string> Standards { get; set; } = new List<string>();
        public List<string> Suggestions { get; set; }

        // Reasoning enrichments
        public float ConfidenceLevel { get; set; } = 0.5f;
        public List<ReasoningStep> ReasoningChain { get; set; } = new List<ReasoningStep>();
        public List<CrossDomainReference> CrossDomainReferences { get; set; } = new List<CrossDomainReference>();
        public List<string> KnowledgeReferences { get; set; } = new List<string>();
        public List<string> KnowledgeInsights { get; set; } = new List<string>();
        public List<string> InferredFacts { get; set; } = new List<string>();
        public List<string> StandardsRequirements { get; set; } = new List<string>();

        // Entity-driven parametric context
        public string ParametricContext { get; set; }
        public Dictionary<string, string> ExtractedParameters { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// A single step in the reasoning chain that produced the advisory response.
    /// </summary>
    public class ReasoningStep
    {
        public string Source { get; set; }
        public string Description { get; set; }
        public float Confidence { get; set; }
    }

    /// <summary>
    /// A detected cross-domain reference suggesting related consulting topics.
    /// </summary>
    public class CrossDomainReference
    {
        public string Domain { get; set; }
        public string Intent { get; set; }
        public List<string> MatchedKeywords { get; set; } = new List<string>();
        public float Relevance { get; set; }
    }

    #endregion
}
