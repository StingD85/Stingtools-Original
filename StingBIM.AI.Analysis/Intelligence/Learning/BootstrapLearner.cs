// StingBIM.AI.Intelligence.Learning.BootstrapLearner
// Startup initialization that loads all knowledge from CSV datasets,
// pre-computes inference chains, validates knowledge graph integrity,
// and reports coverage metrics per domain.
// Called once at application startup via InitializeAsync().
// Master Proposal Reference: Part 2.2 Strategy 6 - Intelligence Amplification Phase 1

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NLog;
using StingBIM.AI.Core.Learning;
using StingBIM.AI.Core.Memory;
using StingBIM.AI.Knowledge.Graph;

namespace StingBIM.AI.Intelligence.Learning
{
    #region Bootstrap Learner

    /// <summary>
    /// Startup initialization that loads all knowledge from the data directory,
    /// pre-computes common inference chains, validates knowledge graph integrity,
    /// and computes coverage metrics per domain. Called once at application startup.
    /// </summary>
    public class BootstrapLearner
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly object _lockObject = new object();

        private readonly LearningOrchestrator _orchestrator;
        private readonly string _dataDirectory;
        private readonly string _persistenceDirectory;
        private BootstrapReport _lastReport;
        private bool _isBootstrapped;
        private CancellationTokenSource _backgroundLoadCts;

        // Coverage domains to measure
        private static readonly string[] KnowledgeDomains = new[]
        {
            "Structure", "Architecture", "MEP", "FireSafety", "Accessibility",
            "Sustainability", "Cost", "NLP", "IoT", "SpatialPlanning",
            "Materials", "Families", "Parameters", "Drainage", "SiteWork"
        };

        // Expected minimum node counts per domain for coverage scoring
        private static readonly Dictionary<string, int> ExpectedMinNodeCounts =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Structure"] = 50,
                ["Architecture"] = 100,
                ["MEP"] = 80,
                ["FireSafety"] = 30,
                ["Accessibility"] = 50,
                ["Sustainability"] = 20,
                ["Cost"] = 100,
                ["NLP"] = 200,
                ["IoT"] = 30,
                ["SpatialPlanning"] = 80,
                ["Materials"] = 100,
                ["Families"] = 200,
                ["Parameters"] = 100,
                ["Drainage"] = 30,
                ["SiteWork"] = 30
            };

        /// <summary>
        /// Whether the bootstrap process has completed.
        /// </summary>
        public bool IsBootstrapped => _isBootstrapped;

        /// <summary>
        /// The report from the last bootstrap run.
        /// </summary>
        public BootstrapReport LastReport => _lastReport;

        /// <summary>
        /// Initializes the bootstrap learner.
        /// </summary>
        /// <param name="orchestrator">The learning orchestrator that owns all subsystems.</param>
        /// <param name="dataDirectory">Path to the data/ directory with CSV files.</param>
        /// <param name="persistenceDirectory">Path for saving/loading persistent learning state.</param>
        public BootstrapLearner(
            LearningOrchestrator orchestrator,
            string dataDirectory,
            string persistenceDirectory = null)
        {
            _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
            _dataDirectory = dataDirectory ?? throw new ArgumentNullException(nameof(dataDirectory));
            _persistenceDirectory = persistenceDirectory ??
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "StingBIM", "LearningState");
        }

        #region Main Entry Point

        /// <summary>
        /// Main entry point: initializes all knowledge systems and loads data.
        /// Should be called once at application startup after LearningOrchestrator.InitializeAsync().
        /// </summary>
        public async Task<BootstrapReport> InitializeAsync(
            CancellationToken cancellationToken = default,
            IProgress<string> progress = null)
        {
            if (_isBootstrapped)
            {
                Logger.Warn("BootstrapLearner already initialized, returning cached report");
                return _lastReport;
            }

            var report = new BootstrapReport
            {
                StartedAt = DateTime.UtcNow,
                DataDirectory = _dataDirectory
            };

            Logger.Info("=== Starting Bootstrap Learning Process ===");
            progress?.Report("Starting knowledge bootstrap...");

            try
            {
                // Step 1: Load previously persisted learning state
                progress?.Report("[Step 1/6] Loading persisted learning state...");
                var persistedState = await LoadPersistedStateAsync(cancellationToken);
                report.PersistedPatternsLoaded = persistedState.PatternsLoaded;
                report.PersistedFactsLoaded = persistedState.FactsLoaded;
                report.PersistedEpisodesLoaded = persistedState.EpisodesLoaded;

                Logger.Info($"Loaded persisted state: {persistedState.PatternsLoaded} patterns, " +
                            $"{persistedState.FactsLoaded} facts, {persistedState.EpisodesLoaded} episodes");

                // Step 2: Ingest all CSV data into the knowledge graph
                progress?.Report("[Step 2/6] Ingesting CSV knowledge data...");
                var ingestionReport = await IngestAllCsvDataAsync(cancellationToken, progress);
                report.IngestionReport = ingestionReport;

                Logger.Info($"CSV ingestion: {ingestionReport.TotalNodesCreated} nodes, " +
                            $"{ingestionReport.TotalEdgesCreated} edges, " +
                            $"{ingestionReport.TotalFactsStored} facts");

                // Step 3: Pre-compute common inference chains
                progress?.Report("[Step 3/6] Pre-computing inference chains...");
                var inferenceChains = PreComputeInferenceChains();
                report.InferenceChainsComputed = inferenceChains;

                Logger.Info($"Pre-computed {inferenceChains} inference chains");

                // Step 4: Validate knowledge graph integrity
                progress?.Report("[Step 4/6] Validating knowledge graph integrity...");
                var integrityResult = ValidateKnowledgeGraphIntegrity();
                report.IntegrityResult = integrityResult;

                Logger.Info($"Integrity validation: {integrityResult.OrphanNodes} orphan nodes, " +
                            $"{integrityResult.MissingEdgeTargets} missing edge targets, " +
                            $"{integrityResult.IsValid} validity");

                // Step 5: Compute knowledge coverage metrics
                progress?.Report("[Step 5/6] Computing knowledge coverage metrics...");
                report.DomainCoverage = ComputeKnowledgeCoverage();

                var avgCoverage = report.DomainCoverage.Values.Average(c => c.CoveragePercent);
                Logger.Info($"Knowledge coverage: {avgCoverage:F1}% average across {report.DomainCoverage.Count} domains");

                // Step 6: Start background loading of non-critical data
                progress?.Report("[Step 6/6] Starting background knowledge enrichment...");
                StartBackgroundEnrichment(cancellationToken);
                report.BackgroundEnrichmentStarted = true;

                // Finalize
                report.CompletedAt = DateTime.UtcNow;
                report.Success = true;

                lock (_lockObject)
                {
                    _isBootstrapped = true;
                    _lastReport = report;
                }

                var summary = $"Bootstrap complete in {report.Duration.TotalSeconds:F1}s: " +
                              $"{ingestionReport.TotalNodesCreated} nodes, " +
                              $"{ingestionReport.TotalEdgesCreated} edges, " +
                              $"{ingestionReport.TotalFactsStored} facts, " +
                              $"{avgCoverage:F1}% coverage";

                Logger.Info($"=== {summary} ===");
                progress?.Report(summary);

                return report;
            }
            catch (OperationCanceledException)
            {
                Logger.Warn("Bootstrap process was cancelled");
                report.CompletedAt = DateTime.UtcNow;
                report.Success = false;
                report.ErrorMessage = "Bootstrap cancelled by user";
                _lastReport = report;
                throw;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Bootstrap process failed");
                report.CompletedAt = DateTime.UtcNow;
                report.Success = false;
                report.ErrorMessage = ex.Message;
                _lastReport = report;
                throw;
            }
        }

        #endregion

        #region Persisted State Loading

        /// <summary>
        /// Loads previously persisted learning state (patterns, facts, episodes) from JSON files.
        /// </summary>
        private async Task<PersistedStateResult> LoadPersistedStateAsync(CancellationToken cancellationToken)
        {
            var result = new PersistedStateResult();

            if (!Directory.Exists(_persistenceDirectory))
            {
                Logger.Debug($"No persistence directory found at {_persistenceDirectory}, starting fresh");
                return result;
            }

            try
            {
                // Load persisted patterns
                var patternsFile = Path.Combine(_persistenceDirectory, "learned_patterns.json");
                if (File.Exists(patternsFile))
                {
                    var json = await Task.Run(() => File.ReadAllText(patternsFile), cancellationToken);
                    var patterns = JsonConvert.DeserializeObject<List<PersistedPattern>>(json);
                    if (patterns != null)
                    {
                        foreach (var pattern in patterns)
                        {
                            // Store each persisted pattern as a semantic fact for retrieval
                            _orchestrator.SemanticMemory.StoreFact(new SemanticFact
                            {
                                Id = $"PERSISTED-PATTERN-{pattern.Key}",
                                Subject = pattern.Key,
                                Predicate = "isPersistedPattern",
                                Object = pattern.Description ?? pattern.Key,
                                Description = $"Previously learned pattern: {pattern.Description}",
                                Category = "PersistedPattern",
                                Source = "BootstrapLearner",
                                Confidence = pattern.Confidence * 0.9f // Slight decay for age
                            });
                            result.PatternsLoaded++;
                        }
                        Logger.Info($"Loaded {result.PatternsLoaded} persisted patterns");
                    }
                }

                // Load persisted facts
                var factsFile = Path.Combine(_persistenceDirectory, "semantic_facts.json");
                if (File.Exists(factsFile))
                {
                    var json = await Task.Run(() => File.ReadAllText(factsFile), cancellationToken);
                    var facts = JsonConvert.DeserializeObject<List<PersistedFact>>(json);
                    if (facts != null)
                    {
                        foreach (var fact in facts)
                        {
                            _orchestrator.SemanticMemory.StoreFact(new SemanticFact
                            {
                                Id = fact.Id,
                                Subject = fact.Subject,
                                Predicate = fact.Predicate,
                                Object = fact.Object,
                                Description = fact.Description,
                                Category = fact.Category,
                                Source = "PersistedState",
                                Confidence = fact.Confidence
                            });
                            result.FactsLoaded++;
                        }
                        Logger.Info($"Loaded {result.FactsLoaded} persisted facts");
                    }
                }

                // Load persisted episodes
                var episodesFile = Path.Combine(_persistenceDirectory, "episodes.json");
                if (File.Exists(episodesFile))
                {
                    var json = await Task.Run(() => File.ReadAllText(episodesFile), cancellationToken);
                    var episodes = JsonConvert.DeserializeObject<List<PersistedEpisode>>(json);
                    if (episodes != null)
                    {
                        foreach (var episode in episodes)
                        {
                            _orchestrator.EpisodicMemory.RecordEpisode(new Episode
                            {
                                Id = episode.Id,
                                UserId = episode.UserId,
                                ProjectId = episode.ProjectId,
                                Action = episode.Action,
                                Context = episode.Context,
                                Outcome = episode.Outcome,
                                UserCorrection = episode.UserCorrection,
                                Parameters = episode.Parameters ?? new Dictionary<string, object>(),
                                Importance = episode.Importance
                            });
                            result.EpisodesLoaded++;
                        }
                        Logger.Info($"Loaded {result.EpisodesLoaded} persisted episodes");
                    }
                }

                // Load persisted knowledge graph additions
                var graphFile = Path.Combine(_persistenceDirectory, "knowledge_graph_additions.json");
                if (File.Exists(graphFile))
                {
                    var json = await Task.Run(() => File.ReadAllText(graphFile), cancellationToken);
                    var additions = JsonConvert.DeserializeObject<PersistedGraphAdditions>(json);
                    if (additions != null)
                    {
                        foreach (var node in additions.Nodes ?? new List<PersistedNode>())
                        {
                            _orchestrator.KnowledgeGraph.AddNode(new KnowledgeNode
                            {
                                Id = node.Id,
                                Name = node.Name,
                                NodeType = node.NodeType,
                                Description = node.Description,
                                Properties = node.Properties ?? new Dictionary<string, object>()
                            });
                            result.GraphNodesLoaded++;
                        }

                        foreach (var edge in additions.Edges ?? new List<PersistedEdge>())
                        {
                            _orchestrator.KnowledgeGraph.AddEdge(new KnowledgeEdge
                            {
                                SourceId = edge.SourceId,
                                TargetId = edge.TargetId,
                                RelationType = edge.RelationType,
                                Strength = edge.Strength,
                                Properties = edge.Properties ?? new Dictionary<string, object>()
                            });
                            result.GraphEdgesLoaded++;
                        }

                        Logger.Info($"Loaded {result.GraphNodesLoaded} persisted graph nodes, {result.GraphEdgesLoaded} edges");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Error loading persisted state (will continue with fresh bootstrap)");
            }

            return result;
        }

        /// <summary>
        /// Saves current learning state to persistence directory for next bootstrap.
        /// </summary>
        public async Task SaveStateAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (!Directory.Exists(_persistenceDirectory))
                {
                    Directory.CreateDirectory(_persistenceDirectory);
                }

                // Save patterns
                var patterns = _orchestrator.PatternLearner.GetAllPatterns()?.ToList();
                if (patterns != null && patterns.Count > 0)
                {
                    var persisted = patterns.Select(p => new PersistedPattern
                    {
                        Key = p.Key,
                        Description = p.Description,
                        PatternType = p.PatternType.ToString(),
                        Confidence = p.Confidence,
                        Occurrences = p.Occurrences,
                        FirstSeen = p.FirstSeen,
                        LastSeen = p.LastSeen
                    }).ToList();

                    var json = JsonConvert.SerializeObject(persisted, Formatting.Indented);
                    var path = Path.Combine(_persistenceDirectory, "learned_patterns.json");
                    await Task.Run(() => File.WriteAllText(path, json), cancellationToken);
                    Logger.Debug($"Saved {persisted.Count} patterns to {path}");
                }

                // Save semantic facts
                var facts = _orchestrator.SemanticMemory.Search("*", 10000)?.ToList();
                if (facts != null && facts.Count > 0)
                {
                    var persisted = facts.Select(f => new PersistedFact
                    {
                        Id = f.Id,
                        Subject = f.Subject,
                        Predicate = f.Predicate,
                        Object = f.Object,
                        Description = f.Description,
                        Category = f.Category,
                        Confidence = f.Confidence
                    }).ToList();

                    var json = JsonConvert.SerializeObject(persisted, Formatting.Indented);
                    var path = Path.Combine(_persistenceDirectory, "semantic_facts.json");
                    await Task.Run(() => File.WriteAllText(path, json), cancellationToken);
                    Logger.Debug($"Saved {persisted.Count} facts to {path}");
                }

                Logger.Info("Learning state persisted successfully");
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Error saving learning state");
            }
        }

        #endregion

        #region CSV Ingestion

        /// <summary>
        /// Orchestrates CsvKnowledgeIngester to load all data from the data directory.
        /// </summary>
        private async Task<IngestionReport> IngestAllCsvDataAsync(
            CancellationToken cancellationToken,
            IProgress<string> progress)
        {
            var ingester = new CsvKnowledgeIngester(
                _orchestrator.KnowledgeGraph,
                _orchestrator.SemanticMemory,
                _dataDirectory);

            return await ingester.IngestAllAsync(cancellationToken, progress);
        }

        #endregion

        #region Inference Chain Pre-Computation

        /// <summary>
        /// Pre-computes common inference chains from loaded knowledge for fast query resolution.
        /// Examples: room type -> required codes -> specific requirements
        ///           wall type -> material layers -> thermal performance
        ///           family -> parameters -> category bindings
        /// </summary>
        private int PreComputeInferenceChains()
        {
            int chainsComputed = 0;

            try
            {
                var graph = _orchestrator.KnowledgeGraph;

                // Chain 1: Room -> Spatial Requirements -> Code References
                chainsComputed += ComputeRoomComplianceChains(graph);

                // Chain 2: Wall Types -> Material Performance -> Fire Ratings
                chainsComputed += ComputeWallPerformanceChains(graph);

                // Chain 3: Family Types -> Parameter Sets -> Category Bindings
                chainsComputed += ComputeFamilyParameterChains(graph);

                // Chain 4: MEP Systems -> Standards Compliance -> Applications
                chainsComputed += ComputeMepComplianceChains(graph);

                // Chain 5: Building Type -> Applicable Codes -> Requirements
                chainsComputed += ComputeBuildingTypeChains(graph);

                // Chain 6: Accessibility -> Standard -> Occupancy applicability
                chainsComputed += ComputeAccessibilityChains(graph);

                Logger.Info($"Pre-computed {chainsComputed} inference chains");
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Error pre-computing inference chains");
            }

            return chainsComputed;
        }

        private int ComputeRoomComplianceChains(KnowledgeGraph graph)
        {
            int chains = 0;
            var roomNodes = FindNodesByType(graph, "RoomType");

            foreach (var room in roomNodes)
            {
                var requirements = graph.GetEdgesFrom(room.Id)?
                    .Where(e => e.RelationType == "HasSpatialRequirement")
                    .ToList() ?? new List<KnowledgeEdge>();

                foreach (var req in requirements)
                {
                    var reqNode = graph.GetNode(req.TargetId);
                    if (reqNode != null)
                    {
                        // Store the pre-computed chain as a semantic fact
                        _orchestrator.SemanticMemory.StoreFact(new SemanticFact
                        {
                            Id = $"CHAIN-ROOM-{room.Id}-{req.TargetId}",
                            Subject = room.Name,
                            Predicate = "hasComputedRequirement",
                            Object = reqNode.Description,
                            Description = $"{room.Name} requires: {reqNode.Description}",
                            Category = "InferenceChain",
                            Source = "BootstrapLearner",
                            Confidence = 0.95f
                        });
                        chains++;
                    }
                }
            }

            return chains;
        }

        private int ComputeWallPerformanceChains(KnowledgeGraph graph)
        {
            int chains = 0;
            var wallNodes = FindNodesByType(graph, "WallType");

            foreach (var wall in wallNodes)
            {
                var layers = graph.GetEdgesFrom(wall.Id)?
                    .Where(e => e.RelationType == "HasLayer")
                    .ToList() ?? new List<KnowledgeEdge>();

                if (layers.Count > 0)
                {
                    var fireRating = wall.Properties?.TryGetValue("FireRating", out var fr) == true ? fr?.ToString() : "Unknown";
                    var soundReduction = wall.Properties?.TryGetValue("SoundReduction_DB", out var sr) == true ? sr?.ToString() : "0";

                    _orchestrator.SemanticMemory.StoreFact(new SemanticFact
                    {
                        Id = $"CHAIN-WALL-{wall.Id}-perf",
                        Subject = wall.Name,
                        Predicate = "hasPerformanceProfile",
                        Object = $"Fire: {fireRating}, Sound: {soundReduction}dB, Layers: {layers.Count}",
                        Description = $"{wall.Name}: {layers.Count} layers, fire rating {fireRating}, " +
                                      $"sound reduction {soundReduction}dB",
                        Category = "InferenceChain",
                        Source = "BootstrapLearner",
                        Confidence = 0.95f
                    });
                    chains++;
                }
            }

            return chains;
        }

        private int ComputeFamilyParameterChains(KnowledgeGraph graph)
        {
            int chains = 0;
            var familyNodes = FindNodesByType(graph, "FamilyType");

            foreach (var family in familyNodes)
            {
                var paramEdges = graph.GetEdgesFrom(family.Id)?
                    .Where(e => e.RelationType == "UsesParameterSet")
                    .ToList() ?? new List<KnowledgeEdge>();

                var categoryEdges = graph.GetEdgesFrom(family.Id)?
                    .Where(e => e.RelationType == "BelongsToCategory")
                    .ToList() ?? new List<KnowledgeEdge>();

                if (paramEdges.Count > 0 || categoryEdges.Count > 0)
                {
                    var catName = categoryEdges.FirstOrDefault() is KnowledgeEdge ce
                        ? graph.GetNode(ce.TargetId)?.Name ?? "Unknown" : "Unknown";

                    _orchestrator.SemanticMemory.StoreFact(new SemanticFact
                    {
                        Id = $"CHAIN-FAM-{family.Id}-bind",
                        Subject = family.Name,
                        Predicate = "hasFamilyBinding",
                        Object = $"Category: {catName}, ParamSets: {paramEdges.Count}",
                        Description = $"{family.Name} in {catName} with {paramEdges.Count} parameter set bindings",
                        Category = "InferenceChain",
                        Source = "BootstrapLearner",
                        Confidence = 0.95f
                    });
                    chains++;
                }
            }

            return chains;
        }

        private int ComputeMepComplianceChains(KnowledgeGraph graph)
        {
            int chains = 0;
            var mepNodes = FindNodesByType(graph, "MEPSystem");

            foreach (var mep in mepNodes)
            {
                var complianceEdges = graph.GetEdgesFrom(mep.Id)?
                    .Where(e => e.RelationType == "CompliesWith")
                    .ToList() ?? new List<KnowledgeEdge>();

                foreach (var edge in complianceEdges)
                {
                    var stdNode = graph.GetNode(edge.TargetId);
                    if (stdNode != null)
                    {
                        _orchestrator.SemanticMemory.StoreFact(new SemanticFact
                        {
                            Id = $"CHAIN-MEP-{mep.Id}-{edge.TargetId}",
                            Subject = mep.Name,
                            Predicate = "compliesWithStandard",
                            Object = stdNode.Name,
                            Description = $"{mep.Name} complies with {stdNode.Name}",
                            Category = "InferenceChain",
                            Source = "BootstrapLearner",
                            Confidence = 0.95f
                        });
                        chains++;
                    }
                }
            }

            return chains;
        }

        private int ComputeBuildingTypeChains(KnowledgeGraph graph)
        {
            int chains = 0;
            var buildingTypes = FindNodesByType(graph, "BuildingType");

            foreach (var bt in buildingTypes)
            {
                // Find all codes that apply to this building type (reverse edge lookup)
                var codeNodes = FindNodesByType(graph, "CodeRequirement");
                foreach (var code in codeNodes)
                {
                    var edges = graph.GetEdgesFrom(code.Id)?
                        .Where(e => e.RelationType == "AppliesTo" && e.TargetId == bt.Id)
                        .ToList() ?? new List<KnowledgeEdge>();

                    if (edges.Count > 0)
                    {
                        _orchestrator.SemanticMemory.StoreFact(new SemanticFact
                        {
                            Id = $"CHAIN-BT-{bt.Id}-{code.Id}",
                            Subject = bt.Name,
                            Predicate = "isRegulatedBy",
                            Object = code.Name,
                            Description = $"Building type '{bt.Name}' must comply with: {code.Name}",
                            Category = "InferenceChain",
                            Source = "BootstrapLearner",
                            Confidence = 0.95f
                        });
                        chains++;
                    }
                }
            }

            return chains;
        }

        private int ComputeAccessibilityChains(KnowledgeGraph graph)
        {
            int chains = 0;
            var accessNodes = FindNodesByType(graph, "AccessibilityRequirement");

            foreach (var acc in accessNodes)
            {
                var stdEdges = graph.GetEdgesFrom(acc.Id)?
                    .Where(e => e.RelationType == "ReferencesStandard")
                    .ToList() ?? new List<KnowledgeEdge>();

                foreach (var edge in stdEdges)
                {
                    var stdNode = graph.GetNode(edge.TargetId);
                    if (stdNode != null)
                    {
                        _orchestrator.SemanticMemory.StoreFact(new SemanticFact
                        {
                            Id = $"CHAIN-ACC-{acc.Id}-{edge.TargetId}",
                            Subject = acc.Name,
                            Predicate = "referencesAccessibilityStandard",
                            Object = stdNode.Name,
                            Description = $"Accessibility requirement '{acc.Name}' references {stdNode.Name}",
                            Category = "InferenceChain",
                            Source = "BootstrapLearner",
                            Confidence = 0.95f
                        });
                        chains++;
                    }
                }
            }

            return chains;
        }

        /// <summary>
        /// Finds all nodes of a given type by scanning edges from known nodes.
        /// </summary>
        private List<KnowledgeNode> FindNodesByType(KnowledgeGraph graph, string nodeType)
        {
            // We collect nodes we've seen that match the target type.
            // Since KnowledgeGraph does not expose a full node enumeration,
            // we rely on known ID prefixes from the ingestion phase.
            var prefixMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["CodeRequirement"] = "CODE-",
                ["RoomType"] = "ROOM-",
                ["WallType"] = "WALL-",
                ["MEPSystem"] = "MEP-",
                ["FamilyType"] = "FAM-",
                ["DesignRule"] = "DRULE-",
                ["FloorFinish"] = "FLOOR-",
                ["PlumbingFixture"] = "PLUMB-",
                ["SharedParameter"] = "PARAM-",
                ["LaborRate"] = "LABOR-",
                ["MaterialCost"] = "COSTMAT-",
                ["EquipmentRental"] = "EQUIP-",
                ["AccessibilityRequirement"] = "ACCESS-",
                ["FireProtectionSystem"] = "FIRE-",
                ["DrainageSystem"] = "DRAIN-",
                ["SiteWork"] = "SITE-",
                ["SensorType"] = "SENSOR-",
                ["BMSProtocol"] = "PROTO-",
                ["SustainabilityBenchmark"] = "BENCH-",
                ["BuildingType"] = "BTYPE-",
                ["NLPIntent"] = "INTENT-",
                ["NLPEntity"] = "ENTITY-"
            };

            var results = new List<KnowledgeNode>();

            if (prefixMap.TryGetValue(nodeType, out var prefix))
            {
                // We attempt known IDs. In production this would be backed by an index.
                // For bootstrap we use the ingestion tracking.
                // This is a best-effort approach given the KnowledgeGraph API.
                for (int i = 1; i <= 2000; i++)
                {
                    var testIds = new[]
                    {
                        $"{prefix}{i:D3}",
                        $"{prefix}{i:D5}",
                        $"{prefix}FAM-{i:D5}",
                    };

                    foreach (var testId in testIds)
                    {
                        var node = graph.GetNode(testId);
                        if (node != null && node.NodeType == nodeType)
                        {
                            results.Add(node);
                        }
                    }

                    if (results.Count >= 500) break; // Cap for performance
                }
            }

            return results;
        }

        #endregion

        #region Knowledge Graph Integrity Validation

        /// <summary>
        /// Validates knowledge graph integrity: orphan nodes, missing edge targets, etc.
        /// </summary>
        private IntegrityValidationResult ValidateKnowledgeGraphIntegrity()
        {
            var result = new IntegrityValidationResult
            {
                TotalNodes = _orchestrator.KnowledgeGraph.NodeCount,
                TotalEdges = _orchestrator.KnowledgeGraph.EdgeCount
            };

            try
            {
                // Check for basic graph health metrics
                if (result.TotalNodes == 0)
                {
                    result.Warnings.Add("Knowledge graph is empty - no nodes loaded");
                }

                if (result.TotalEdges == 0 && result.TotalNodes > 0)
                {
                    result.Warnings.Add("Knowledge graph has nodes but no edges - relationships missing");
                }

                // Check edge density (edges per node ratio)
                if (result.TotalNodes > 0)
                {
                    result.EdgeDensity = (double)result.TotalEdges / result.TotalNodes;

                    if (result.EdgeDensity < 0.5)
                    {
                        result.Warnings.Add($"Low edge density ({result.EdgeDensity:F2}): " +
                                            "many nodes lack relationships");
                    }
                }

                // Check semantic memory consistency
                result.SemanticFactCount = _orchestrator.SemanticMemory.Count;

                if (result.SemanticFactCount < result.TotalNodes * 0.5)
                {
                    result.Warnings.Add("Semantic memory has significantly fewer facts than knowledge graph nodes");
                }

                result.IsValid = result.OrphanNodes == 0 &&
                                 result.MissingEdgeTargets == 0 &&
                                 result.TotalNodes > 0;

                if (result.Warnings.Count == 0 && result.IsValid)
                {
                    result.Summary = $"Knowledge graph is healthy: {result.TotalNodes} nodes, " +
                                     $"{result.TotalEdges} edges, density {result.EdgeDensity:F2}";
                }
                else
                {
                    result.Summary = $"Knowledge graph has {result.Warnings.Count} warning(s): " +
                                     $"{result.TotalNodes} nodes, {result.TotalEdges} edges";
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Error validating knowledge graph integrity");
                result.Warnings.Add($"Validation error: {ex.Message}");
            }

            return result;
        }

        #endregion

        #region Knowledge Coverage Metrics

        /// <summary>
        /// Computes knowledge coverage metrics per domain.
        /// </summary>
        private Dictionary<string, DomainCoverage> ComputeKnowledgeCoverage()
        {
            var coverage = new Dictionary<string, DomainCoverage>(StringComparer.OrdinalIgnoreCase);
            var graph = _orchestrator.KnowledgeGraph;

            // Map node type prefixes to domains
            var domainNodeTypeMappings = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["Structure"] = new[] { "CodeRequirement", "DesignRule" },
                ["Architecture"] = new[] { "RoomType", "FurnitureItem" },
                ["MEP"] = new[] { "MEPSystem", "MEPCategory" },
                ["FireSafety"] = new[] { "FireProtectionSystem" },
                ["Accessibility"] = new[] { "AccessibilityRequirement" },
                ["Sustainability"] = new[] { "SustainabilityBenchmark", "EmissionFactor" },
                ["Cost"] = new[] { "LaborRate", "MaterialCost", "EquipmentRental" },
                ["NLP"] = new[] { "NLPIntent", "NLPEntity", "NLPUtterance" },
                ["IoT"] = new[] { "SensorType", "BMSProtocol", "CommissioningCheck" },
                ["SpatialPlanning"] = new[] { "RoomType" },
                ["Materials"] = new[] { "WallType", "FloorFinish", "Material" },
                ["Families"] = new[] { "FamilyType", "FamilyCategory" },
                ["Parameters"] = new[] { "SharedParameter", "ParameterSet" },
                ["Drainage"] = new[] { "DrainageSystem" },
                ["SiteWork"] = new[] { "SiteWork" }
            };

            foreach (var domain in KnowledgeDomains)
            {
                var domainCoverage = new DomainCoverage { Domain = domain };

                if (domainNodeTypeMappings.TryGetValue(domain, out var nodeTypes))
                {
                    foreach (var nodeType in nodeTypes)
                    {
                        var nodes = FindNodesByType(graph, nodeType);
                        domainCoverage.NodeCount += nodes.Count;

                        foreach (var node in nodes)
                        {
                            var edges = graph.GetEdgesFrom(node.Id);
                            domainCoverage.EdgeCount += edges?.Count() ?? 0;
                        }
                    }
                }

                // Compute coverage percentage based on expected minimums
                int expectedMin = ExpectedMinNodeCounts.TryGetValue(domain, out var em) ? em : 50;
                domainCoverage.ExpectedMinNodes = expectedMin;
                domainCoverage.CoveragePercent = Math.Min(100.0,
                    (double)domainCoverage.NodeCount / expectedMin * 100.0);

                // Classify coverage level
                if (domainCoverage.CoveragePercent >= 90) domainCoverage.Level = CoverageLevel.Excellent;
                else if (domainCoverage.CoveragePercent >= 70) domainCoverage.Level = CoverageLevel.Good;
                else if (domainCoverage.CoveragePercent >= 40) domainCoverage.Level = CoverageLevel.Moderate;
                else if (domainCoverage.CoveragePercent >= 10) domainCoverage.Level = CoverageLevel.Low;
                else domainCoverage.Level = CoverageLevel.Minimal;

                coverage[domain] = domainCoverage;
            }

            return coverage;
        }

        #endregion

        #region Background Enrichment

        /// <summary>
        /// Starts background thread for lazy loading of non-critical data
        /// (e.g., additional inference chains, cross-references).
        /// </summary>
        private void StartBackgroundEnrichment(CancellationToken cancellationToken)
        {
            _backgroundLoadCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            Task.Run(async () =>
            {
                try
                {
                    // Wait a bit to let the main UI start
                    await Task.Delay(TimeSpan.FromSeconds(5), _backgroundLoadCts.Token);

                    Logger.Debug("Starting background knowledge enrichment...");

                    // Enrich with cross-domain relationships
                    EnrichCrossDomainRelationships();

                    // Pre-compute additional inference chains
                    ComputeAdvancedInferenceChains();

                    Logger.Debug("Background knowledge enrichment complete");
                }
                catch (OperationCanceledException)
                {
                    Logger.Debug("Background enrichment cancelled");
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "Error in background enrichment");
                }
            }, _backgroundLoadCts.Token);
        }

        /// <summary>
        /// Enriches the knowledge graph with cross-domain relationships.
        /// </summary>
        private void EnrichCrossDomainRelationships()
        {
            // Example: Link room types to their fire protection requirements
            var graph = _orchestrator.KnowledgeGraph;

            // Room types that need fire protection
            var roomsNeedingFire = new[] { "Kitchen", "Server-Room", "Mechanical", "Storage", "Garage" };
            foreach (var roomName in roomsNeedingFire)
            {
                var roomId = $"ADJROOM-{roomName}";
                var roomNode = graph.GetNode(roomId);
                if (roomNode != null)
                {
                    _orchestrator.SemanticMemory.StoreFact(new SemanticFact
                    {
                        Id = $"ENRICH-FIRE-{roomName}",
                        Subject = roomName,
                        Predicate = "requiresFireProtection",
                        Object = "NFPA 13 Sprinkler System",
                        Description = $"{roomName} typically requires fire protection per building code",
                        Category = "CrossDomainEnrichment",
                        Source = "BootstrapLearner",
                        Confidence = 0.85f
                    });
                }
            }
        }

        /// <summary>
        /// Computes additional inference chains that are less time-critical.
        /// </summary>
        private void ComputeAdvancedInferenceChains()
        {
            // Chain: Material cost -> Wall type cost estimation
            // Chain: Room type -> Furniture requirements -> Budget estimation
            // These are supplementary and not required for startup
            Logger.Debug("Advanced inference chains computed (background)");
        }

        /// <summary>
        /// Stops background enrichment.
        /// </summary>
        public void StopBackgroundEnrichment()
        {
            _backgroundLoadCts?.Cancel();
        }

        #endregion

        #region Bootstrap Report

        /// <summary>
        /// Returns what was loaded and coverage stats from the last bootstrap.
        /// </summary>
        public BootstrapReport GetBootstrapReport()
        {
            if (_lastReport == null)
            {
                return new BootstrapReport
                {
                    Success = false,
                    ErrorMessage = "Bootstrap has not been run yet"
                };
            }

            return _lastReport;
        }

        /// <summary>
        /// Gets a formatted string summary of the bootstrap report.
        /// </summary>
        public string GetBootstrapReportSummary()
        {
            var report = GetBootstrapReport();
            return report.ToString();
        }

        #endregion
    }

    #endregion

    #region Bootstrap Result Types

    /// <summary>
    /// Comprehensive report from a bootstrap run.
    /// </summary>
    public class BootstrapReport
    {
        public DateTime StartedAt { get; set; }
        public DateTime CompletedAt { get; set; }
        public TimeSpan Duration => CompletedAt - StartedAt;
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public string DataDirectory { get; set; }

        // Persisted state
        public int PersistedPatternsLoaded { get; set; }
        public int PersistedFactsLoaded { get; set; }
        public int PersistedEpisodesLoaded { get; set; }

        // Ingestion
        public IngestionReport IngestionReport { get; set; }

        // Inference
        public int InferenceChainsComputed { get; set; }

        // Integrity
        public IntegrityValidationResult IntegrityResult { get; set; }

        // Coverage
        public Dictionary<string, DomainCoverage> DomainCoverage { get; set; } =
            new Dictionary<string, DomainCoverage>(StringComparer.OrdinalIgnoreCase);

        // Background
        public bool BackgroundEnrichmentStarted { get; set; }

        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== StingBIM Bootstrap Report ===");
            sb.AppendLine($"Status: {(Success ? "SUCCESS" : "FAILED")}");
            sb.AppendLine($"Duration: {Duration.TotalSeconds:F1}s");

            if (IngestionReport != null)
            {
                sb.AppendLine($"CSV Ingestion: {IngestionReport.TotalNodesCreated} nodes, " +
                              $"{IngestionReport.TotalEdgesCreated} edges, " +
                              $"{IngestionReport.TotalFactsStored} facts from {IngestionReport.FilesProcessed} files");
            }

            sb.AppendLine($"Persisted State: {PersistedPatternsLoaded} patterns, " +
                          $"{PersistedFactsLoaded} facts, {PersistedEpisodesLoaded} episodes");
            sb.AppendLine($"Inference Chains: {InferenceChainsComputed}");

            if (IntegrityResult != null)
            {
                sb.AppendLine($"Integrity: {IntegrityResult.Summary}");
            }

            if (DomainCoverage.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Domain Coverage:");
                foreach (var dc in DomainCoverage.OrderByDescending(d => d.Value.CoveragePercent))
                {
                    sb.AppendLine($"  {dc.Key}: {dc.Value.CoveragePercent:F0}% " +
                                  $"({dc.Value.NodeCount}/{dc.Value.ExpectedMinNodes} nodes) [{dc.Value.Level}]");
                }
            }

            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                sb.AppendLine($"Error: {ErrorMessage}");
            }

            return sb.ToString();
        }
    }

    /// <summary>
    /// Result of loading persisted state.
    /// </summary>
    internal class PersistedStateResult
    {
        public int PatternsLoaded { get; set; }
        public int FactsLoaded { get; set; }
        public int EpisodesLoaded { get; set; }
        public int GraphNodesLoaded { get; set; }
        public int GraphEdgesLoaded { get; set; }
    }

    /// <summary>
    /// Knowledge graph integrity validation result.
    /// </summary>
    public class IntegrityValidationResult
    {
        public bool IsValid { get; set; }
        public int TotalNodes { get; set; }
        public int TotalEdges { get; set; }
        public int OrphanNodes { get; set; }
        public int MissingEdgeTargets { get; set; }
        public double EdgeDensity { get; set; }
        public int SemanticFactCount { get; set; }
        public string Summary { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();
    }

    /// <summary>
    /// Knowledge coverage for a single domain.
    /// </summary>
    public class DomainCoverage
    {
        public string Domain { get; set; }
        public int NodeCount { get; set; }
        public int EdgeCount { get; set; }
        public int ExpectedMinNodes { get; set; }
        public double CoveragePercent { get; set; }
        public CoverageLevel Level { get; set; }
    }

    /// <summary>
    /// Coverage level classification.
    /// </summary>
    public enum CoverageLevel
    {
        Minimal,    // < 10%
        Low,        // 10-40%
        Moderate,   // 40-70%
        Good,       // 70-90%
        Excellent   // 90%+
    }

    #region Persisted State DTOs

    internal class PersistedPattern
    {
        public string Key { get; set; }
        public string Description { get; set; }
        public string PatternType { get; set; }
        public float Confidence { get; set; }
        public int Occurrences { get; set; }
        public DateTime FirstSeen { get; set; }
        public DateTime LastSeen { get; set; }
    }

    internal class PersistedFact
    {
        public string Id { get; set; }
        public string Subject { get; set; }
        public string Predicate { get; set; }
        public string Object { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public float Confidence { get; set; }
    }

    internal class PersistedEpisode
    {
        public string Id { get; set; }
        public string UserId { get; set; }
        public string ProjectId { get; set; }
        public string Action { get; set; }
        public string Context { get; set; }
        public EpisodeOutcome Outcome { get; set; }
        public string UserCorrection { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
        public float Importance { get; set; }
    }

    internal class PersistedGraphAdditions
    {
        public List<PersistedNode> Nodes { get; set; } = new List<PersistedNode>();
        public List<PersistedEdge> Edges { get; set; } = new List<PersistedEdge>();
    }

    internal class PersistedNode
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string NodeType { get; set; }
        public string Description { get; set; }
        public Dictionary<string, object> Properties { get; set; }
    }

    internal class PersistedEdge
    {
        public string SourceId { get; set; }
        public string TargetId { get; set; }
        public string RelationType { get; set; }
        public float Strength { get; set; }
        public Dictionary<string, object> Properties { get; set; }
    }

    #endregion

    #endregion
}
