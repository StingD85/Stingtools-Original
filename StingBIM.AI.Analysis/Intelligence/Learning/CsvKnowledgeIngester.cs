// StingBIM.AI.Intelligence.Learning.CsvKnowledgeIngester
// Massively expands the knowledge graph from all 113 CSV datasets in the data directory.
// Reads each CSV type, creates KnowledgeNode per row and KnowledgeEdge for relationships,
// and stores as SemanticFacts in SemanticMemory for text search.
// Target: 15,000+ nodes, 40,000+ edges from the full data corpus.
// Master Proposal Reference: Part 2.2 Strategy 6 - Intelligence Amplification Phase 1

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NLog;
using StingBIM.AI.Core.Memory;
using StingBIM.AI.Knowledge.Graph;

namespace StingBIM.AI.Intelligence.Learning
{
    #region CSV Knowledge Ingester

    /// <summary>
    /// Massively expands the knowledge graph by ingesting all CSV datasets from the data directory.
    /// Each CSV type has a dedicated parser that creates KnowledgeNodes per row and KnowledgeEdges
    /// for relationships between entities. Also stores facts in SemanticMemory for text search.
    /// </summary>
    public class CsvKnowledgeIngester
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly object _lockObject = new object();

        private readonly KnowledgeGraph _knowledgeGraph;
        private readonly SemanticMemory _semanticMemory;
        private readonly string _dataDirectory;
        private readonly ConcurrentDictionary<string, CsvIngestionResult> _ingestionResults;

        // Track node/edge IDs to avoid duplicates
        private readonly ConcurrentDictionary<string, bool> _existingNodeIds;
        private readonly ConcurrentDictionary<string, bool> _existingEdgeKeys;

        // CSV parser registry: maps filename pattern to parser method
        private readonly Dictionary<string, Func<string, CancellationToken, IProgress<string>, Task<CsvIngestionResult>>> _parsers;

        // Running totals
        private int _totalNodesCreated;
        private int _totalEdgesCreated;
        private int _totalFactsStored;
        private int _totalFilesProcessed;
        private int _totalRowsProcessed;

        /// <summary>
        /// Initializes the ingester with a knowledge graph, semantic memory, and data directory path.
        /// </summary>
        public CsvKnowledgeIngester(
            KnowledgeGraph knowledgeGraph,
            SemanticMemory semanticMemory,
            string dataDirectory)
        {
            _knowledgeGraph = knowledgeGraph ?? throw new ArgumentNullException(nameof(knowledgeGraph));
            _semanticMemory = semanticMemory ?? throw new ArgumentNullException(nameof(semanticMemory));
            _dataDirectory = dataDirectory ?? throw new ArgumentNullException(nameof(dataDirectory));

            _ingestionResults = new ConcurrentDictionary<string, CsvIngestionResult>(StringComparer.OrdinalIgnoreCase);
            _existingNodeIds = new ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            _existingEdgeKeys = new ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

            _parsers = new Dictionary<string, Func<string, CancellationToken, IProgress<string>, Task<CsvIngestionResult>>>(StringComparer.OrdinalIgnoreCase)
            {
                ["BUILDING_CODES"] = IngestBuildingCodesAsync,
                ["ROOM_DEFINITIONS"] = IngestRoomDefinitionsAsync,
                ["WALL_TYPES"] = IngestWallTypesAsync,
                ["MEP_SYSTEMS"] = IngestMepSystemsAsync,
                ["FAMILY_CATALOG"] = IngestFamilyCatalogAsync,
                ["FAMILY_PARAMETERS"] = IngestFamilyParametersAsync,
                ["FAMILY_PLACEMENT_RULES"] = IngestFamilyPlacementRulesAsync,
                ["FAMILY_RELATIONSHIPS"] = IngestFamilyRelationshipsAsync,
                ["DESIGN_RULES"] = IngestDesignRulesAsync,
                ["SPATIAL_ADJACENCY"] = IngestSpatialAdjacencyAsync,
                ["FLOOR_FINISHES"] = IngestFloorFinishesAsync,
                ["PLUMBING_FIXTURES"] = IngestPlumbingFixturesAsync,
                ["MASTER_PARAMETERS"] = IngestMasterParametersAsync,
                ["COST_LABOR"] = IngestCostLaborAsync,
                ["COST_MATERIALS"] = IngestCostMaterialsAsync,
                ["COST_EQUIPMENT"] = IngestCostEquipmentAsync,
                ["ACCESSIBILITY_REQUIREMENTS"] = IngestAccessibilityAsync,
                ["FIRE_PROTECTION"] = IngestFireProtectionAsync,
                ["DRAINAGE_SYSTEMS"] = IngestDrainageSystemsAsync,
                ["SITE_WORK"] = IngestSiteWorkAsync,
                ["AI_TRAINING_INTENTS"] = IngestTrainingIntentsAsync,
                ["AI_TRAINING_ENTITIES"] = IngestTrainingEntitiesAsync,
                ["AI_TRAINING_UTTERANCES"] = IngestTrainingUtterancesAsync,
                ["AI_TRAINING_CONVERSATIONS"] = IngestTrainingConversationsAsync,
                ["SENSOR_TYPES"] = IngestSensorTypesAsync,
                ["BMS_PROTOCOLS"] = IngestBmsProtocolsAsync,
                ["SUSTAINABILITY_BENCHMARKS"] = IngestSustainabilityBenchmarksAsync,
                ["ENERGY_EMISSION"] = IngestEnergyEmissionFactorsAsync,
                ["COMMISSIONING"] = IngestCommissioningChecklistsAsync,
                ["CODE_REQUIREMENTS"] = IngestCodeRequirementsAsync,
                ["CONSTRUCTION_ASSEMBLIES"] = IngestConstructionAssembliesAsync,
                ["CONSTRUCTION_DETAILS"] = IngestConstructionDetailsAsync,
                ["ENTITY_DICTIONARY"] = IngestEntityDictionaryAsync,
                ["FURNITURE"] = IngestFurnitureAsync,
                ["CASE_STUDIES"] = IngestCaseStudiesAsync,
                ["BUILDING_COMPONENTS"] = IngestBuildingComponentsAsync,
                ["BLE_MATERIALS"] = IngestBleMaterialsAsync,
                ["MEP_MATERIALS"] = IngestMepMaterialsAsync,
            };
        }

        #region Main Ingestion Entry Point

        /// <summary>
        /// Ingests all CSV files found in the data directory and subdirectories.
        /// Returns comprehensive statistics of nodes created, edges created, and facts stored.
        /// </summary>
        public async Task<IngestionReport> IngestAllAsync(
            CancellationToken cancellationToken = default,
            IProgress<string> progress = null)
        {
            Logger.Info($"Starting CSV knowledge ingestion from: {_dataDirectory}");
            progress?.Report($"Scanning for CSV files in {_dataDirectory}...");

            if (!Directory.Exists(_dataDirectory))
            {
                throw new DirectoryNotFoundException($"Data directory not found: {_dataDirectory}");
            }

            // Find all CSV files recursively
            var csvFiles = Directory.GetFiles(_dataDirectory, "*.csv", SearchOption.AllDirectories)
                .OrderBy(f => f)
                .ToList();

            Logger.Info($"Found {csvFiles.Count} CSV files to ingest");
            progress?.Report($"Found {csvFiles.Count} CSV files. Beginning ingestion...");

            var overallStart = DateTime.UtcNow;
            int fileIndex = 0;

            foreach (var csvFile in csvFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                fileIndex++;
                var fileName = Path.GetFileNameWithoutExtension(csvFile);
                var percentComplete = (int)((double)fileIndex / csvFiles.Count * 100);
                progress?.Report($"[{percentComplete}%] Ingesting {fileName} ({fileIndex}/{csvFiles.Count})...");

                try
                {
                    // Find matching parser by file name pattern
                    var parser = FindParser(fileName);
                    if (parser != null)
                    {
                        var result = await parser(csvFile, cancellationToken, progress);
                        if (result != null)
                        {
                            _ingestionResults[csvFile] = result;
                            _totalFilesProcessed++;

                            Logger.Debug($"Ingested {fileName}: {result.NodesCreated} nodes, " +
                                         $"{result.EdgesCreated} edges, {result.FactsStored} facts");
                        }
                    }
                    else
                    {
                        // Generic fallback parser for unrecognized CSVs
                        var result = await IngestGenericCsvAsync(csvFile, cancellationToken, progress);
                        if (result != null)
                        {
                            _ingestionResults[csvFile] = result;
                            _totalFilesProcessed++;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, $"Error ingesting {csvFile}");
                    _ingestionResults[csvFile] = new CsvIngestionResult
                    {
                        FileName = fileName,
                        Success = false,
                        ErrorMessage = ex.Message
                    };
                }
            }

            var report = BuildReport(overallStart);
            Logger.Info($"CSV ingestion complete: {report.TotalNodesCreated} nodes, " +
                        $"{report.TotalEdgesCreated} edges, {report.TotalFactsStored} facts " +
                        $"from {report.FilesProcessed} files ({report.Duration.TotalSeconds:F1}s)");

            progress?.Report($"Ingestion complete: {report.TotalNodesCreated:N0} nodes, " +
                             $"{report.TotalEdgesCreated:N0} edges, {report.TotalFactsStored:N0} facts");

            return report;
        }

        /// <summary>
        /// Finds the matching parser for a file name.
        /// </summary>
        private Func<string, CancellationToken, IProgress<string>, Task<CsvIngestionResult>> FindParser(string fileName)
        {
            var upper = fileName.ToUpperInvariant();

            foreach (var kvp in _parsers)
            {
                if (upper.Contains(kvp.Key))
                {
                    return kvp.Value;
                }
            }

            return null;
        }

        #endregion

        #region CSV Reading Helpers

        /// <summary>
        /// Reads a CSV file and returns rows as dictionaries keyed by column header.
        /// Handles quoted fields and embedded commas.
        /// </summary>
        private List<Dictionary<string, string>> ReadCsv(string filePath)
        {
            var rows = new List<Dictionary<string, string>>();

            using (var reader = new StreamReader(filePath, Encoding.UTF8))
            {
                var headerLine = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(headerLine)) return rows;

                var headers = ParseCsvLine(headerLine);

                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var fields = ParseCsvLine(line);
                    var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                    for (int i = 0; i < headers.Length && i < fields.Length; i++)
                    {
                        row[headers[i].Trim()] = fields[i].Trim();
                    }

                    rows.Add(row);
                }
            }

            return rows;
        }

        /// <summary>
        /// Parses a single CSV line, handling quoted fields.
        /// </summary>
        private string[] ParseCsvLine(string line)
        {
            var fields = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++; // skip escaped quote
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    fields.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            fields.Add(current.ToString());
            return fields.ToArray();
        }

        /// <summary>
        /// Adds a node to the knowledge graph if it doesn't already exist.
        /// Thread-safe.
        /// </summary>
        private bool AddNodeSafe(KnowledgeNode node)
        {
            if (_existingNodeIds.TryAdd(node.Id, true))
            {
                _knowledgeGraph.AddNode(node);
                Interlocked.Increment(ref _totalNodesCreated);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Adds an edge to the knowledge graph if it doesn't already exist.
        /// Thread-safe.
        /// </summary>
        private bool AddEdgeSafe(KnowledgeEdge edge)
        {
            var key = $"{edge.SourceId}|{edge.RelationType}|{edge.TargetId}";
            if (_existingEdgeKeys.TryAdd(key, true))
            {
                _knowledgeGraph.AddEdge(edge);
                Interlocked.Increment(ref _totalEdgesCreated);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Stores a fact in semantic memory. Thread-safe.
        /// </summary>
        private void StoreFactSafe(SemanticFact fact)
        {
            _semanticMemory.StoreFact(fact);
            Interlocked.Increment(ref _totalFactsStored);
        }

        /// <summary>
        /// Safely parses a double value from string.
        /// </summary>
        private double SafeParseDouble(string value, double defaultValue = 0.0)
        {
            if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
                return result;
            return defaultValue;
        }

        /// <summary>
        /// Safely parses an int value from string.
        /// </summary>
        private int SafeParseInt(string value, int defaultValue = 0)
        {
            if (int.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
                return result;
            return defaultValue;
        }

        /// <summary>
        /// Gets a column value or a default.
        /// </summary>
        private string GetValue(Dictionary<string, string> row, string key, string defaultValue = "")
        {
            return row.TryGetValue(key, out var val) && !string.IsNullOrWhiteSpace(val) ? val : defaultValue;
        }

        #endregion

        #region Building Codes Parser

        private Task<CsvIngestionResult> IngestBuildingCodesAsync(
            string filePath, CancellationToken ct, IProgress<string> progress)
        {
            var result = new CsvIngestionResult { FileName = Path.GetFileNameWithoutExtension(filePath) };
            var rows = ReadCsv(filePath);

            foreach (var row in rows)
            {
                ct.ThrowIfCancellationRequested();
                var ruleId = GetValue(row, "RuleID", GetValue(row, "ID"));
                if (string.IsNullOrEmpty(ruleId)) continue;

                var nodeId = $"CODE-{ruleId}";
                var ruleName = GetValue(row, "RuleName");
                var description = GetValue(row, "Description");
                var codeRef = GetValue(row, "Code_Reference");
                var priority = GetValue(row, "Priority");
                var appliesTo = GetValue(row, "Applies_To");
                var buildingType = GetValue(row, "Building_Type");
                var category = GetValue(row, "Category");

                var node = new KnowledgeNode
                {
                    Id = nodeId,
                    Name = ruleName,
                    NodeType = "CodeRequirement",
                    Description = description,
                    Properties = new Dictionary<string, object>
                    {
                        ["RuleID"] = ruleId,
                        ["CodeReference"] = codeRef,
                        ["Priority"] = priority,
                        ["AppliesTo"] = appliesTo,
                        ["BuildingType"] = buildingType,
                        ["Category"] = category,
                        ["Requirement"] = GetValue(row, "Requirement")
                    }
                };

                if (AddNodeSafe(node)) result.NodesCreated++;

                // Create compliance edges to building types
                if (!string.IsNullOrEmpty(buildingType))
                {
                    foreach (var bt in buildingType.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var btNodeId = $"BTYPE-{bt.Trim().Replace(" ", "_")}";
                        AddNodeSafe(new KnowledgeNode
                        {
                            Id = btNodeId,
                            Name = bt.Trim(),
                            NodeType = "BuildingType",
                            Description = $"Building type: {bt.Trim()}"
                        });

                        if (AddEdgeSafe(new KnowledgeEdge
                        {
                            SourceId = nodeId,
                            TargetId = btNodeId,
                            RelationType = "AppliesTo",
                            Strength = priority == "Critical" ? 1.0f : 0.7f,
                            Properties = new Dictionary<string, object> { ["CodeReference"] = codeRef }
                        })) result.EdgesCreated++;
                    }
                }

                // Create edges to applicable element types
                if (!string.IsNullOrEmpty(appliesTo))
                {
                    foreach (var target in appliesTo.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var targetNodeId = $"ELEMTYPE-{target.Trim().Replace(" ", "_")}";
                        AddNodeSafe(new KnowledgeNode
                        {
                            Id = targetNodeId,
                            Name = target.Trim(),
                            NodeType = "ElementType",
                            Description = $"Element type: {target.Trim()}"
                        });

                        if (AddEdgeSafe(new KnowledgeEdge
                        {
                            SourceId = nodeId,
                            TargetId = targetNodeId,
                            RelationType = "Regulates",
                            Strength = 0.8f
                        })) result.EdgesCreated++;
                    }
                }

                // Store as semantic fact
                StoreFactSafe(new SemanticFact
                {
                    Id = nodeId,
                    Subject = ruleName,
                    Predicate = "isCodeRequirement",
                    Object = codeRef,
                    Description = $"{ruleName}: {description}",
                    Category = "BuildingCode",
                    Source = result.FileName,
                    Confidence = 1.0f
                });
                result.FactsStored++;
                result.RowsProcessed++;
            }

            result.Success = true;
            _totalRowsProcessed += result.RowsProcessed;
            return Task.FromResult(result);
        }

        #endregion

        #region Room Definitions Parser

        private Task<CsvIngestionResult> IngestRoomDefinitionsAsync(
            string filePath, CancellationToken ct, IProgress<string> progress)
        {
            var result = new CsvIngestionResult { FileName = Path.GetFileNameWithoutExtension(filePath) };
            var rows = ReadCsv(filePath);

            foreach (var row in rows)
            {
                ct.ThrowIfCancellationRequested();
                var id = GetValue(row, "ID");
                var roomType = GetValue(row, "RoomType");
                if (string.IsNullOrEmpty(roomType)) continue;

                var nodeId = $"ROOM-{id}";
                var minArea = SafeParseDouble(GetValue(row, "MinArea_M2"));
                var maxArea = SafeParseDouble(GetValue(row, "MaxArea_M2"));
                var minWidth = SafeParseDouble(GetValue(row, "MinWidth_M"));
                var minHeight = SafeParseDouble(GetValue(row, "MinHeight_M"));

                var node = new KnowledgeNode
                {
                    Id = nodeId,
                    Name = roomType,
                    NodeType = "RoomType",
                    Description = GetValue(row, "Description"),
                    Properties = new Dictionary<string, object>
                    {
                        ["MinArea_M2"] = minArea,
                        ["MaxArea_M2"] = maxArea,
                        ["MinWidth_M"] = minWidth,
                        ["MinHeight_M"] = minHeight,
                        ["RequiresWindow"] = GetValue(row, "RequiresWindow"),
                        ["RequiresVent"] = GetValue(row, "RequiresVent"),
                        ["RequiresPlumbing"] = GetValue(row, "RequiresPlumbing"),
                        ["DefaultFurniture"] = GetValue(row, "DefaultFurniture")
                    }
                };

                if (AddNodeSafe(node)) result.NodesCreated++;

                // Spatial requirement edges
                if (minArea > 0)
                {
                    var reqNodeId = $"REQ-{nodeId}-Area";
                    AddNodeSafe(new KnowledgeNode
                    {
                        Id = reqNodeId,
                        Name = $"{roomType} Area Requirement",
                        NodeType = "SpatialRequirement",
                        Description = $"Area: {minArea}-{maxArea} m2",
                        Properties = new Dictionary<string, object> { ["Min"] = minArea, ["Max"] = maxArea, ["Unit"] = "m2" }
                    });

                    if (AddEdgeSafe(new KnowledgeEdge
                    {
                        SourceId = nodeId,
                        TargetId = reqNodeId,
                        RelationType = "HasSpatialRequirement",
                        Strength = 1.0f
                    })) result.EdgesCreated++;
                }

                // Default furniture edges
                var furniture = GetValue(row, "DefaultFurniture");
                if (!string.IsNullOrEmpty(furniture))
                {
                    foreach (var item in furniture.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var furnNodeId = $"FURN-{item.Trim().Replace(" ", "_")}";
                        AddNodeSafe(new KnowledgeNode
                        {
                            Id = furnNodeId,
                            Name = item.Trim(),
                            NodeType = "FurnitureItem",
                            Description = $"Furniture: {item.Trim()}"
                        });

                        if (AddEdgeSafe(new KnowledgeEdge
                        {
                            SourceId = nodeId,
                            TargetId = furnNodeId,
                            RelationType = "DefaultFurniture",
                            Strength = 0.8f
                        })) result.EdgesCreated++;
                    }
                }

                StoreFactSafe(new SemanticFact
                {
                    Id = nodeId,
                    Subject = roomType,
                    Predicate = "isRoomType",
                    Object = $"Area {minArea}-{maxArea}m2",
                    Description = $"{roomType}: {GetValue(row, "Description")} (min {minArea}m2, min width {minWidth}m)",
                    Category = "RoomDefinition",
                    Source = result.FileName,
                    Confidence = 1.0f
                });
                result.FactsStored++;
                result.RowsProcessed++;
            }

            result.Success = true;
            _totalRowsProcessed += result.RowsProcessed;
            return Task.FromResult(result);
        }

        #endregion

        #region Wall Types Parser

        private Task<CsvIngestionResult> IngestWallTypesAsync(
            string filePath, CancellationToken ct, IProgress<string> progress)
        {
            var result = new CsvIngestionResult { FileName = Path.GetFileNameWithoutExtension(filePath) };
            var rows = ReadCsv(filePath);

            foreach (var row in rows)
            {
                ct.ThrowIfCancellationRequested();
                var code = GetValue(row, "Mat_Code");
                var name = GetValue(row, "Mat_Name");
                if (string.IsNullOrEmpty(code)) continue;

                var nodeId = $"WALL-{code}";
                var node = new KnowledgeNode
                {
                    Id = nodeId,
                    Name = name,
                    NodeType = "WallType",
                    Description = $"{name} ({GetValue(row, "Mat_Application")})",
                    Properties = new Dictionary<string, object>
                    {
                        ["Code"] = code,
                        ["Category"] = GetValue(row, "Mat_Category"),
                        ["Thickness_MM"] = SafeParseDouble(GetValue(row, "Mat_Thickness_MM")),
                        ["Cost_USD"] = SafeParseDouble(GetValue(row, "Mat_Cost_USD")),
                        ["Cost_UGX"] = SafeParseDouble(GetValue(row, "Mat_Cost_UGX")),
                        ["FireRating"] = GetValue(row, "Prop_Fire_Rating"),
                        ["SoundReduction_DB"] = SafeParseDouble(GetValue(row, "Prop_Sound_Red_DB")),
                        ["ThermalConductivity"] = SafeParseDouble(GetValue(row, "Prop_Thermal_Cond_W_MK")),
                        ["LoadBearing"] = GetValue(row, "Load_Bearing"),
                        ["LayerCount"] = SafeParseInt(GetValue(row, "Mat_Layer_Count")),
                        ["Standard"] = GetValue(row, "Mat_Standard"),
                        ["UseCase"] = GetValue(row, "Use_Case")
                    }
                };

                if (AddNodeSafe(node)) result.NodesCreated++;

                // Material composition edges for each layer
                for (int layer = 1; layer <= 5; layer++)
                {
                    var layerMat = GetValue(row, $"Mat_Layer_{layer}_Material");
                    if (string.IsNullOrEmpty(layerMat) || layerMat == "0") continue;

                    var matNodeId = $"MAT-{layerMat.Replace(" ", "_").Replace("/", "_")}";
                    AddNodeSafe(new KnowledgeNode
                    {
                        Id = matNodeId,
                        Name = layerMat,
                        NodeType = "Material",
                        Description = $"Material: {layerMat}",
                        Properties = new Dictionary<string, object>
                        {
                            ["Thickness_MM"] = SafeParseDouble(GetValue(row, $"Mat_Layer_{layer}_Thickness_MM")),
                            ["Function"] = GetValue(row, $"Mat_Layer_{layer}_Function")
                        }
                    });

                    if (AddEdgeSafe(new KnowledgeEdge
                    {
                        SourceId = nodeId,
                        TargetId = matNodeId,
                        RelationType = "HasLayer",
                        Strength = 1.0f,
                        Properties = new Dictionary<string, object>
                        {
                            ["LayerNumber"] = layer,
                            ["Function"] = GetValue(row, $"Mat_Layer_{layer}_Function")
                        }
                    })) result.EdgesCreated++;
                }

                StoreFactSafe(new SemanticFact
                {
                    Id = nodeId,
                    Subject = name,
                    Predicate = "isWallType",
                    Object = GetValue(row, "Mat_Category"),
                    Description = $"{name}: {GetValue(row, "Mat_Category")}, " +
                                  $"{GetValue(row, "Mat_Thickness_MM")}mm, {GetValue(row, "Use_Case")}",
                    Category = "WallType",
                    Source = result.FileName,
                    Confidence = 1.0f
                });
                result.FactsStored++;
                result.RowsProcessed++;
            }

            result.Success = true;
            _totalRowsProcessed += result.RowsProcessed;
            return Task.FromResult(result);
        }

        #endregion

        #region MEP Systems Parser

        private Task<CsvIngestionResult> IngestMepSystemsAsync(
            string filePath, CancellationToken ct, IProgress<string> progress)
        {
            var result = new CsvIngestionResult { FileName = Path.GetFileNameWithoutExtension(filePath) };
            var rows = ReadCsv(filePath);

            foreach (var row in rows)
            {
                ct.ThrowIfCancellationRequested();
                var equipId = GetValue(row, "EquipmentID");
                var name = GetValue(row, "Name");
                if (string.IsNullOrEmpty(equipId)) continue;

                var nodeId = $"MEP-{equipId}";
                var node = new KnowledgeNode
                {
                    Id = nodeId,
                    Name = name,
                    NodeType = "MEPSystem",
                    Description = GetValue(row, "Description"),
                    Properties = new Dictionary<string, object>
                    {
                        ["Category"] = GetValue(row, "Category"),
                        ["Width_MM"] = SafeParseDouble(GetValue(row, "Width_MM")),
                        ["Height_MM"] = SafeParseDouble(GetValue(row, "Height_MM")),
                        ["Depth_MM"] = SafeParseDouble(GetValue(row, "Depth_MM")),
                        ["Location"] = GetValue(row, "Location"),
                        ["Mounting"] = GetValue(row, "Mounting"),
                        ["Heating_kW"] = SafeParseDouble(GetValue(row, "Heating_kW")),
                        ["Cooling_kW"] = SafeParseDouble(GetValue(row, "Cooling_kW")),
                        ["Airflow_CFM"] = SafeParseDouble(GetValue(row, "Airflow_CFM")),
                        ["Application"] = GetValue(row, "Application"),
                        ["Standard"] = GetValue(row, "Standard"),
                        ["Cost_USD"] = SafeParseDouble(GetValue(row, "Cost_USD"))
                    }
                };

                if (AddNodeSafe(node)) result.NodesCreated++;

                // Specification edges
                var category = GetValue(row, "Category");
                if (!string.IsNullOrEmpty(category))
                {
                    var catNodeId = $"MEPCAT-{category.Replace(" ", "_").Replace("/", "_")}";
                    AddNodeSafe(new KnowledgeNode
                    {
                        Id = catNodeId,
                        Name = category,
                        NodeType = "MEPCategory",
                        Description = $"MEP Category: {category}"
                    });

                    if (AddEdgeSafe(new KnowledgeEdge
                    {
                        SourceId = nodeId,
                        TargetId = catNodeId,
                        RelationType = "BelongsToCategory",
                        Strength = 1.0f
                    })) result.EdgesCreated++;
                }

                // Standard compliance edges
                var standard = GetValue(row, "Standard");
                if (!string.IsNullOrEmpty(standard))
                {
                    var stdNodeId = $"STD-{standard.Replace(" ", "_").Replace("/", "_")}";
                    AddNodeSafe(new KnowledgeNode
                    {
                        Id = stdNodeId,
                        Name = standard,
                        NodeType = "Standard",
                        Description = $"Standard: {standard}"
                    });

                    if (AddEdgeSafe(new KnowledgeEdge
                    {
                        SourceId = nodeId,
                        TargetId = stdNodeId,
                        RelationType = "CompliesWith",
                        Strength = 1.0f
                    })) result.EdgesCreated++;
                }

                StoreFactSafe(new SemanticFact
                {
                    Id = nodeId,
                    Subject = name,
                    Predicate = "isMEPSystem",
                    Object = category,
                    Description = $"{name}: {GetValue(row, "Description")} ({GetValue(row, "Application")})",
                    Category = "MEPSystem",
                    Source = result.FileName,
                    Confidence = 1.0f
                });
                result.FactsStored++;
                result.RowsProcessed++;
            }

            result.Success = true;
            _totalRowsProcessed += result.RowsProcessed;
            return Task.FromResult(result);
        }

        #endregion

        #region Family Catalog Parser

        private Task<CsvIngestionResult> IngestFamilyCatalogAsync(
            string filePath, CancellationToken ct, IProgress<string> progress)
        {
            var result = new CsvIngestionResult { FileName = Path.GetFileNameWithoutExtension(filePath) };
            var rows = ReadCsv(filePath);

            foreach (var row in rows)
            {
                ct.ThrowIfCancellationRequested();
                var familyId = GetValue(row, "FamilyID");
                var familyName = GetValue(row, "FamilyName");
                if (string.IsNullOrEmpty(familyId)) continue;

                var nodeId = $"FAM-{familyId}";
                var category = GetValue(row, "Category");

                var node = new KnowledgeNode
                {
                    Id = nodeId,
                    Name = familyName,
                    NodeType = "FamilyType",
                    Description = GetValue(row, "Description"),
                    Properties = new Dictionary<string, object>
                    {
                        ["Category"] = category,
                        ["DefaultWidth"] = SafeParseDouble(GetValue(row, "DefaultWidth")),
                        ["DefaultHeight"] = SafeParseDouble(GetValue(row, "DefaultHeight")),
                        ["DefaultDepth"] = SafeParseDouble(GetValue(row, "DefaultDepth")),
                        ["PlacementType"] = GetValue(row, "PlacementType"),
                        ["RequiresHost"] = GetValue(row, "RequiresHost"),
                        ["DefaultMaterial"] = GetValue(row, "DefaultMaterial"),
                        ["Application"] = GetValue(row, "Application"),
                        ["UseCase"] = GetValue(row, "UseCase"),
                        ["Standard"] = GetValue(row, "Standard"),
                        ["LoadBearing"] = GetValue(row, "LoadBearing")
                    }
                };

                if (AddNodeSafe(node)) result.NodesCreated++;

                // Category edge
                if (!string.IsNullOrEmpty(category))
                {
                    var catNodeId = $"FAMCAT-{category.Replace(" ", "_")}";
                    AddNodeSafe(new KnowledgeNode
                    {
                        Id = catNodeId,
                        Name = category,
                        NodeType = "FamilyCategory",
                        Description = $"Revit family category: {category}"
                    });

                    if (AddEdgeSafe(new KnowledgeEdge
                    {
                        SourceId = nodeId,
                        TargetId = catNodeId,
                        RelationType = "BelongsToCategory",
                        Strength = 1.0f
                    })) result.EdgesCreated++;
                }

                // Parameter set edge
                var paramSet = GetValue(row, "ParameterSet");
                if (!string.IsNullOrEmpty(paramSet))
                {
                    var paramNodeId = $"PARAMSET-{paramSet}";
                    AddNodeSafe(new KnowledgeNode
                    {
                        Id = paramNodeId,
                        Name = paramSet,
                        NodeType = "ParameterSet",
                        Description = $"Parameter set: {paramSet}"
                    });

                    if (AddEdgeSafe(new KnowledgeEdge
                    {
                        SourceId = nodeId,
                        TargetId = paramNodeId,
                        RelationType = "UsesParameterSet",
                        Strength = 1.0f
                    })) result.EdgesCreated++;
                }

                // Placement rule edge
                var placementType = GetValue(row, "PlacementType");
                if (!string.IsNullOrEmpty(placementType))
                {
                    var placeNodeId = $"PLACEMENT-{placementType.Replace(" ", "_").Replace("-", "_")}";
                    AddNodeSafe(new KnowledgeNode
                    {
                        Id = placeNodeId,
                        Name = placementType,
                        NodeType = "PlacementRule",
                        Description = $"Placement type: {placementType}"
                    });

                    if (AddEdgeSafe(new KnowledgeEdge
                    {
                        SourceId = nodeId,
                        TargetId = placeNodeId,
                        RelationType = "HasPlacementRule",
                        Strength = 1.0f
                    })) result.EdgesCreated++;
                }

                StoreFactSafe(new SemanticFact
                {
                    Id = nodeId,
                    Subject = familyName,
                    Predicate = "isFamilyType",
                    Object = category,
                    Description = $"{familyName}: {GetValue(row, "Description")} ({category}, {GetValue(row, "Application")})",
                    Category = "FamilyCatalog",
                    Source = result.FileName,
                    Confidence = 1.0f
                });
                result.FactsStored++;
                result.RowsProcessed++;
            }

            result.Success = true;
            _totalRowsProcessed += result.RowsProcessed;
            return Task.FromResult(result);
        }

        #endregion

        #region Design Rules Parser

        private Task<CsvIngestionResult> IngestDesignRulesAsync(
            string filePath, CancellationToken ct, IProgress<string> progress)
        {
            var result = new CsvIngestionResult { FileName = Path.GetFileNameWithoutExtension(filePath) };
            var rows = ReadCsv(filePath);

            foreach (var row in rows)
            {
                ct.ThrowIfCancellationRequested();
                var ruleId = GetValue(row, "RuleID");
                var ruleName = GetValue(row, "RuleName");
                if (string.IsNullOrEmpty(ruleId)) continue;

                var nodeId = $"DRULE-{ruleId}";
                var node = new KnowledgeNode
                {
                    Id = nodeId,
                    Name = ruleName,
                    NodeType = "DesignRule",
                    Description = GetValue(row, "Description"),
                    Properties = new Dictionary<string, object>
                    {
                        ["CodeReference"] = GetValue(row, "Code_Reference"),
                        ["Priority"] = GetValue(row, "Priority"),
                        ["AppliesTo"] = GetValue(row, "Applies_To"),
                        ["BuildingType"] = GetValue(row, "Building_Type"),
                        ["Requirement"] = GetValue(row, "Requirement"),
                        ["Category"] = GetValue(row, "Category")
                    }
                };

                if (AddNodeSafe(node)) result.NodesCreated++;

                // Applicability edges
                var appliesTo = GetValue(row, "Applies_To");
                if (!string.IsNullOrEmpty(appliesTo))
                {
                    foreach (var target in appliesTo.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var targetNodeId = $"ELEMTYPE-{target.Trim().Replace(" ", "_")}";
                        AddNodeSafe(new KnowledgeNode
                        {
                            Id = targetNodeId,
                            Name = target.Trim(),
                            NodeType = "ElementType",
                            Description = $"Element type: {target.Trim()}"
                        });

                        if (AddEdgeSafe(new KnowledgeEdge
                        {
                            SourceId = nodeId,
                            TargetId = targetNodeId,
                            RelationType = "AppliesTo",
                            Strength = GetValue(row, "Priority") == "Critical" ? 1.0f : 0.7f
                        })) result.EdgesCreated++;
                    }
                }

                StoreFactSafe(new SemanticFact
                {
                    Id = nodeId,
                    Subject = ruleName,
                    Predicate = "isDesignRule",
                    Object = GetValue(row, "Code_Reference"),
                    Description = $"{ruleName}: {GetValue(row, "Description")} ({GetValue(row, "Requirement")})",
                    Category = "DesignRule",
                    Source = result.FileName,
                    Confidence = 1.0f
                });
                result.FactsStored++;
                result.RowsProcessed++;
            }

            result.Success = true;
            _totalRowsProcessed += result.RowsProcessed;
            return Task.FromResult(result);
        }

        #endregion

        #region Spatial Adjacency Parser

        private Task<CsvIngestionResult> IngestSpatialAdjacencyAsync(
            string filePath, CancellationToken ct, IProgress<string> progress)
        {
            var result = new CsvIngestionResult { FileName = Path.GetFileNameWithoutExtension(filePath) };
            var rows = ReadCsv(filePath);

            foreach (var row in rows)
            {
                ct.ThrowIfCancellationRequested();
                var room1 = GetValue(row, "RoomType1");
                var room2 = GetValue(row, "RoomType2");
                if (string.IsNullOrEmpty(room1) || string.IsNullOrEmpty(room2)) continue;

                var relationship = GetValue(row, "Relationship");
                var priority = GetValue(row, "Priority");

                // Ensure room nodes exist
                var room1Id = $"ADJROOM-{room1.Replace(" ", "_")}";
                var room2Id = $"ADJROOM-{room2.Replace(" ", "_")}";

                AddNodeSafe(new KnowledgeNode
                {
                    Id = room1Id, Name = room1, NodeType = "RoomType",
                    Description = $"Room type: {room1}"
                });

                AddNodeSafe(new KnowledgeNode
                {
                    Id = room2Id, Name = room2, NodeType = "RoomType",
                    Description = $"Room type: {room2}"
                });

                // Create adjacency edge
                float strength = relationship switch
                {
                    "Adjacent" => 1.0f,
                    "Near" => 0.7f,
                    "Separate" => 0.3f,
                    _ => 0.5f
                };

                if (priority == "Critical") strength = Math.Min(1.0f, strength + 0.2f);

                if (AddEdgeSafe(new KnowledgeEdge
                {
                    SourceId = room1Id,
                    TargetId = room2Id,
                    RelationType = $"SpatialAdjacency_{relationship}",
                    Strength = strength,
                    Properties = new Dictionary<string, object>
                    {
                        ["Relationship"] = relationship,
                        ["Priority"] = priority,
                        ["RequiredConnection"] = GetValue(row, "RequiredConnection"),
                        ["PreferredDirection"] = GetValue(row, "PreferredDirection"),
                        ["MinDistance_M"] = SafeParseDouble(GetValue(row, "MinDistance_M")),
                        ["MaxDistance_M"] = SafeParseDouble(GetValue(row, "MaxDistance_M"))
                    }
                })) result.EdgesCreated++;

                StoreFactSafe(new SemanticFact
                {
                    Id = $"ADJ-{room1}-{room2}",
                    Subject = room1,
                    Predicate = $"shouldBe{relationship}To",
                    Object = room2,
                    Description = $"{room1} should be {relationship.ToLower()} to {room2} ({priority}): {GetValue(row, "Notes")}",
                    Category = "SpatialAdjacency",
                    Source = result.FileName,
                    Confidence = strength
                });
                result.FactsStored++;
                result.RowsProcessed++;
            }

            result.Success = true;
            _totalRowsProcessed += result.RowsProcessed;
            return Task.FromResult(result);
        }

        #endregion

        #region Floor Finishes Parser

        private Task<CsvIngestionResult> IngestFloorFinishesAsync(
            string filePath, CancellationToken ct, IProgress<string> progress)
        {
            var result = new CsvIngestionResult { FileName = Path.GetFileNameWithoutExtension(filePath) };
            var rows = ReadCsv(filePath);

            foreach (var row in rows)
            {
                ct.ThrowIfCancellationRequested();
                var matId = GetValue(row, "MaterialId");
                var name = GetValue(row, "Name");
                if (string.IsNullOrEmpty(matId)) continue;

                var nodeId = $"FLOOR-{matId}";
                var node = new KnowledgeNode
                {
                    Id = nodeId,
                    Name = name,
                    NodeType = "FloorFinish",
                    Description = $"{name} ({GetValue(row, "Category")}/{GetValue(row, "SubCategory")})",
                    Properties = new Dictionary<string, object>
                    {
                        ["Category"] = GetValue(row, "Category"),
                        ["SubCategory"] = GetValue(row, "SubCategory"),
                        ["ThermalConductivity"] = SafeParseDouble(GetValue(row, "ThermalConductivity")),
                        ["Density"] = SafeParseDouble(GetValue(row, "Density")),
                        ["UnitCost"] = SafeParseDouble(GetValue(row, "UnitCost")),
                        ["Currency"] = GetValue(row, "Currency"),
                        ["FireRating"] = GetValue(row, "FireRating"),
                        ["Region"] = GetValue(row, "Region")
                    }
                };

                if (AddNodeSafe(node)) result.NodesCreated++;

                // Sub-category edge
                var subCat = GetValue(row, "SubCategory");
                if (!string.IsNullOrEmpty(subCat))
                {
                    var subCatNodeId = $"FLOORSUBCAT-{subCat.Replace(" ", "_").Replace("/", "_")}";
                    AddNodeSafe(new KnowledgeNode
                    {
                        Id = subCatNodeId, Name = subCat, NodeType = "FloorFinishCategory",
                        Description = $"Floor finish category: {subCat}"
                    });

                    if (AddEdgeSafe(new KnowledgeEdge
                    {
                        SourceId = nodeId, TargetId = subCatNodeId,
                        RelationType = "BelongsToCategory", Strength = 1.0f
                    })) result.EdgesCreated++;
                }

                StoreFactSafe(new SemanticFact
                {
                    Id = nodeId,
                    Subject = name,
                    Predicate = "isFloorFinish",
                    Object = GetValue(row, "SubCategory"),
                    Description = $"{name}: {GetValue(row, "SubCategory")}, " +
                                  $"${GetValue(row, "UnitCost")}/unit, {GetValue(row, "FireRating")}",
                    Category = "FloorFinish",
                    Source = result.FileName,
                    Confidence = 1.0f
                });
                result.FactsStored++;
                result.RowsProcessed++;
            }

            result.Success = true;
            _totalRowsProcessed += result.RowsProcessed;
            return Task.FromResult(result);
        }

        #endregion

        #region Plumbing Fixtures Parser

        private Task<CsvIngestionResult> IngestPlumbingFixturesAsync(
            string filePath, CancellationToken ct, IProgress<string> progress)
        {
            var result = new CsvIngestionResult { FileName = Path.GetFileNameWithoutExtension(filePath) };
            var rows = ReadCsv(filePath);

            foreach (var row in rows)
            {
                ct.ThrowIfCancellationRequested();
                var matId = GetValue(row, "MaterialId");
                var name = GetValue(row, "Name");
                if (string.IsNullOrEmpty(matId)) continue;

                var nodeId = $"PLUMB-{matId}";
                var node = new KnowledgeNode
                {
                    Id = nodeId,
                    Name = name,
                    NodeType = "PlumbingFixture",
                    Description = $"{name} ({GetValue(row, "SubCategory")})",
                    Properties = new Dictionary<string, object>
                    {
                        ["Category"] = GetValue(row, "Category"),
                        ["SubCategory"] = GetValue(row, "SubCategory"),
                        ["UnitCost"] = SafeParseDouble(GetValue(row, "UnitCost")),
                        ["Currency"] = GetValue(row, "Currency"),
                        ["FireRating"] = GetValue(row, "FireRating"),
                        ["Region"] = GetValue(row, "Region")
                    }
                };

                if (AddNodeSafe(node)) result.NodesCreated++;

                // Code requirement edges via sub-category
                var subCat = GetValue(row, "SubCategory");
                if (!string.IsNullOrEmpty(subCat))
                {
                    var subCatNodeId = $"PLUMBCAT-{subCat.Replace(" ", "_").Replace("-", "_")}";
                    AddNodeSafe(new KnowledgeNode
                    {
                        Id = subCatNodeId, Name = subCat, NodeType = "PlumbingCategory",
                        Description = $"Plumbing fixture category: {subCat}"
                    });

                    if (AddEdgeSafe(new KnowledgeEdge
                    {
                        SourceId = nodeId, TargetId = subCatNodeId,
                        RelationType = "BelongsToCategory", Strength = 1.0f
                    })) result.EdgesCreated++;
                }

                StoreFactSafe(new SemanticFact
                {
                    Id = nodeId,
                    Subject = name,
                    Predicate = "isPlumbingFixture",
                    Object = subCat,
                    Description = $"{name}: {subCat}, ${GetValue(row, "UnitCost")}",
                    Category = "PlumbingFixture",
                    Source = result.FileName,
                    Confidence = 1.0f
                });
                result.FactsStored++;
                result.RowsProcessed++;
            }

            result.Success = true;
            _totalRowsProcessed += result.RowsProcessed;
            return Task.FromResult(result);
        }

        #endregion

        #region Master Parameters Parser

        private Task<CsvIngestionResult> IngestMasterParametersAsync(
            string filePath, CancellationToken ct, IProgress<string> progress)
        {
            var result = new CsvIngestionResult { FileName = Path.GetFileNameWithoutExtension(filePath) };
            var rows = ReadCsv(filePath);

            foreach (var row in rows)
            {
                ct.ThrowIfCancellationRequested();
                var paramName = GetValue(row, "Parameter_Name");
                var paramGuid = GetValue(row, "Parameter_GUID");
                if (string.IsNullOrEmpty(paramName)) continue;

                var nodeId = $"PARAM-{paramGuid}";
                var revitCat = GetValue(row, "Revit_Category");

                var node = new KnowledgeNode
                {
                    Id = nodeId,
                    Name = paramName,
                    NodeType = "SharedParameter",
                    Description = GetValue(row, "Description"),
                    Properties = new Dictionary<string, object>
                    {
                        ["GUID"] = paramGuid,
                        ["DataType"] = GetValue(row, "Data_Type"),
                        ["GroupName"] = GetValue(row, "Group_Name"),
                        ["BindingType"] = GetValue(row, "Binding_Type"),
                        ["HasFormula"] = GetValue(row, "Has_Formula"),
                        ["Formula"] = GetValue(row, "Formula"),
                        ["Discipline"] = GetValue(row, "Discipline"),
                        ["UserModifiable"] = GetValue(row, "User_Modifiable")
                    }
                };

                if (AddNodeSafe(node)) result.NodesCreated++;

                // Category binding edge
                if (!string.IsNullOrEmpty(revitCat))
                {
                    var catNodeId = $"REVITCAT-{revitCat.Replace(" ", "_")}";
                    AddNodeSafe(new KnowledgeNode
                    {
                        Id = catNodeId, Name = revitCat, NodeType = "RevitCategory",
                        Description = $"Revit category: {revitCat}"
                    });

                    if (AddEdgeSafe(new KnowledgeEdge
                    {
                        SourceId = nodeId, TargetId = catNodeId,
                        RelationType = "BoundToCategory", Strength = 1.0f,
                        Properties = new Dictionary<string, object> { ["BindingType"] = GetValue(row, "Binding_Type") }
                    })) result.EdgesCreated++;
                }

                StoreFactSafe(new SemanticFact
                {
                    Id = nodeId,
                    Subject = paramName,
                    Predicate = "isSharedParameter",
                    Object = revitCat,
                    Description = $"{paramName}: {GetValue(row, "Description")} ({GetValue(row, "Data_Type")}, {revitCat})",
                    Category = "SharedParameter",
                    Source = result.FileName,
                    Confidence = 1.0f
                });
                result.FactsStored++;
                result.RowsProcessed++;
            }

            result.Success = true;
            _totalRowsProcessed += result.RowsProcessed;
            return Task.FromResult(result);
        }

        #endregion

        #region Cost Data Parsers

        private Task<CsvIngestionResult> IngestCostLaborAsync(
            string filePath, CancellationToken ct, IProgress<string> progress)
        {
            var result = new CsvIngestionResult { FileName = Path.GetFileNameWithoutExtension(filePath) };
            var rows = ReadCsv(filePath);

            foreach (var row in rows)
            {
                ct.ThrowIfCancellationRequested();
                var code = GetValue(row, "trade_code");
                var name = GetValue(row, "trade_name");
                if (string.IsNullOrEmpty(code)) continue;

                var nodeId = $"LABOR-{code}";
                var node = new KnowledgeNode
                {
                    Id = nodeId,
                    Name = name,
                    NodeType = "LaborRate",
                    Description = $"{name} ({GetValue(row, "skill_level")}, {GetValue(row, "region")})",
                    Properties = new Dictionary<string, object>
                    {
                        ["SkillLevel"] = GetValue(row, "skill_level"),
                        ["HourlyRate_UGX"] = SafeParseDouble(GetValue(row, "hourly_rate_ugx")),
                        ["DailyRate_UGX"] = SafeParseDouble(GetValue(row, "daily_rate_ugx")),
                        ["MonthlyRate_UGX"] = SafeParseDouble(GetValue(row, "monthly_rate_ugx")),
                        ["Region"] = GetValue(row, "region"),
                        ["ExperienceYears"] = GetValue(row, "experience_years"),
                        ["OvertimeMultiplier"] = SafeParseDouble(GetValue(row, "overtime_multiplier"))
                    }
                };

                if (AddNodeSafe(node)) result.NodesCreated++;

                StoreFactSafe(new SemanticFact
                {
                    Id = nodeId,
                    Subject = name,
                    Predicate = "hasLaborRate",
                    Object = $"UGX {GetValue(row, "daily_rate_ugx")}/day",
                    Description = $"{name}: {GetValue(row, "skill_level")}, UGX {GetValue(row, "daily_rate_ugx")}/day in {GetValue(row, "region")}",
                    Category = "CostData",
                    Source = result.FileName,
                    Confidence = 0.9f
                });
                result.FactsStored++;
                result.RowsProcessed++;
            }

            result.Success = true;
            _totalRowsProcessed += result.RowsProcessed;
            return Task.FromResult(result);
        }

        private Task<CsvIngestionResult> IngestCostMaterialsAsync(
            string filePath, CancellationToken ct, IProgress<string> progress)
        {
            var result = new CsvIngestionResult { FileName = Path.GetFileNameWithoutExtension(filePath) };
            var rows = ReadCsv(filePath);

            foreach (var row in rows)
            {
                ct.ThrowIfCancellationRequested();
                var code = GetValue(row, "material_code");
                var name = GetValue(row, "material_name");
                if (string.IsNullOrEmpty(code)) continue;

                var nodeId = $"COSTMAT-{code}";
                var node = new KnowledgeNode
                {
                    Id = nodeId,
                    Name = name,
                    NodeType = "MaterialCost",
                    Description = $"{name}: UGX {GetValue(row, "unit_cost_ugx")}/{GetValue(row, "unit")}",
                    Properties = new Dictionary<string, object>
                    {
                        ["Category"] = GetValue(row, "category"),
                        ["Unit"] = GetValue(row, "unit"),
                        ["UnitCost_UGX"] = SafeParseDouble(GetValue(row, "unit_cost_ugx")),
                        ["UnitCost_USD"] = SafeParseDouble(GetValue(row, "unit_cost_usd")),
                        ["Region"] = GetValue(row, "region"),
                        ["LeadTimeDays"] = SafeParseInt(GetValue(row, "lead_time_days"))
                    }
                };

                if (AddNodeSafe(node)) result.NodesCreated++;

                // Material-labor relationship edges
                var category = GetValue(row, "category");
                if (!string.IsNullOrEmpty(category))
                {
                    var catNodeId = $"MATCOSTCAT-{category.Replace(" ", "_")}";
                    AddNodeSafe(new KnowledgeNode
                    {
                        Id = catNodeId, Name = category, NodeType = "MaterialCategory",
                        Description = $"Material category: {category}"
                    });

                    if (AddEdgeSafe(new KnowledgeEdge
                    {
                        SourceId = nodeId, TargetId = catNodeId,
                        RelationType = "BelongsToCategory", Strength = 1.0f
                    })) result.EdgesCreated++;
                }

                StoreFactSafe(new SemanticFact
                {
                    Id = nodeId,
                    Subject = name,
                    Predicate = "hasMaterialCost",
                    Object = $"UGX {GetValue(row, "unit_cost_ugx")}/{GetValue(row, "unit")}",
                    Description = $"{name}: {GetValue(row, "category")}, " +
                                  $"UGX {GetValue(row, "unit_cost_ugx")} / USD {GetValue(row, "unit_cost_usd")} per {GetValue(row, "unit")}",
                    Category = "CostData",
                    Source = result.FileName,
                    Confidence = 0.9f
                });
                result.FactsStored++;
                result.RowsProcessed++;
            }

            result.Success = true;
            _totalRowsProcessed += result.RowsProcessed;
            return Task.FromResult(result);
        }

        private Task<CsvIngestionResult> IngestCostEquipmentAsync(
            string filePath, CancellationToken ct, IProgress<string> progress)
        {
            var result = new CsvIngestionResult { FileName = Path.GetFileNameWithoutExtension(filePath) };
            var rows = ReadCsv(filePath);

            foreach (var row in rows)
            {
                ct.ThrowIfCancellationRequested();
                var code = GetValue(row, "equipment_code");
                var name = GetValue(row, "equipment_name");
                if (string.IsNullOrEmpty(code)) continue;

                var nodeId = $"EQUIP-{code}";
                var node = new KnowledgeNode
                {
                    Id = nodeId,
                    Name = name,
                    NodeType = "EquipmentRental",
                    Description = $"{name} ({GetValue(row, "capacity")})",
                    Properties = new Dictionary<string, object>
                    {
                        ["Category"] = GetValue(row, "category"),
                        ["Capacity"] = GetValue(row, "capacity"),
                        ["DailyRate_UGX"] = SafeParseDouble(GetValue(row, "daily_rate_ugx")),
                        ["WeeklyRate_UGX"] = SafeParseDouble(GetValue(row, "weekly_rate_ugx")),
                        ["MonthlyRate_UGX"] = SafeParseDouble(GetValue(row, "monthly_rate_ugx")),
                        ["OperatorIncluded"] = GetValue(row, "operator_included"),
                        ["Region"] = GetValue(row, "region")
                    }
                };

                if (AddNodeSafe(node)) result.NodesCreated++;

                StoreFactSafe(new SemanticFact
                {
                    Id = nodeId,
                    Subject = name,
                    Predicate = "hasEquipmentRentalRate",
                    Object = $"UGX {GetValue(row, "daily_rate_ugx")}/day",
                    Description = $"{name}: {GetValue(row, "capacity")}, UGX {GetValue(row, "daily_rate_ugx")}/day",
                    Category = "CostData",
                    Source = result.FileName,
                    Confidence = 0.9f
                });
                result.FactsStored++;
                result.RowsProcessed++;
            }

            result.Success = true;
            _totalRowsProcessed += result.RowsProcessed;
            return Task.FromResult(result);
        }

        #endregion

        #region Specialty Systems Parsers

        private Task<CsvIngestionResult> IngestAccessibilityAsync(
            string filePath, CancellationToken ct, IProgress<string> progress)
        {
            var result = new CsvIngestionResult { FileName = Path.GetFileNameWithoutExtension(filePath) };
            var rows = ReadCsv(filePath);

            foreach (var row in rows)
            {
                ct.ThrowIfCancellationRequested();
                var code = GetValue(row, "requirement_code");
                var name = GetValue(row, "requirement_name");
                if (string.IsNullOrEmpty(code)) continue;

                var nodeId = $"ACCESS-{code}";
                var node = new KnowledgeNode
                {
                    Id = nodeId,
                    Name = name,
                    NodeType = "AccessibilityRequirement",
                    Description = $"{name}: {GetValue(row, "notes")}",
                    Properties = new Dictionary<string, object>
                    {
                        ["Category"] = GetValue(row, "category"),
                        ["SubCategory"] = GetValue(row, "subcategory"),
                        ["StandardReference"] = GetValue(row, "standard_reference"),
                        ["MinDimension_MM"] = SafeParseDouble(GetValue(row, "min_dimension_mm")),
                        ["MaxDimension_MM"] = SafeParseDouble(GetValue(row, "max_dimension_mm")),
                        ["SlopeRatio"] = GetValue(row, "slope_ratio"),
                        ["Application"] = GetValue(row, "application"),
                        ["Priority"] = GetValue(row, "priority"),
                        ["ComplianceLevel"] = GetValue(row, "compliance_level")
                    }
                };

                if (AddNodeSafe(node)) result.NodesCreated++;

                // Compliance edge to standard
                var stdRef = GetValue(row, "standard_reference");
                if (!string.IsNullOrEmpty(stdRef))
                {
                    var stdNodeId = $"STD-{stdRef.Replace(" ", "_")}";
                    AddNodeSafe(new KnowledgeNode
                    {
                        Id = stdNodeId, Name = stdRef, NodeType = "Standard",
                        Description = $"Standard: {stdRef}"
                    });

                    if (AddEdgeSafe(new KnowledgeEdge
                    {
                        SourceId = nodeId, TargetId = stdNodeId,
                        RelationType = "ReferencesStandard", Strength = 1.0f
                    })) result.EdgesCreated++;
                }

                StoreFactSafe(new SemanticFact
                {
                    Id = nodeId,
                    Subject = name,
                    Predicate = "isAccessibilityRequirement",
                    Object = stdRef,
                    Description = $"{name}: {GetValue(row, "notes")} (ref: {stdRef})",
                    Category = "Accessibility",
                    Source = result.FileName,
                    Confidence = 1.0f
                });
                result.FactsStored++;
                result.RowsProcessed++;
            }

            result.Success = true;
            _totalRowsProcessed += result.RowsProcessed;
            return Task.FromResult(result);
        }

        private Task<CsvIngestionResult> IngestFireProtectionAsync(
            string filePath, CancellationToken ct, IProgress<string> progress)
        {
            var result = new CsvIngestionResult { FileName = Path.GetFileNameWithoutExtension(filePath) };
            var rows = ReadCsv(filePath);

            foreach (var row in rows)
            {
                ct.ThrowIfCancellationRequested();
                var code = GetValue(row, "system_code");
                var name = GetValue(row, "system_name");
                if (string.IsNullOrEmpty(code)) continue;

                var nodeId = $"FIRE-{code}";
                var node = new KnowledgeNode
                {
                    Id = nodeId,
                    Name = name,
                    NodeType = "FireProtectionSystem",
                    Description = $"{name} ({GetValue(row, "category")}/{GetValue(row, "subcategory")})",
                    Properties = new Dictionary<string, object>
                    {
                        ["Category"] = GetValue(row, "category"),
                        ["SubCategory"] = GetValue(row, "subcategory"),
                        ["Application"] = GetValue(row, "application"),
                        ["CoverageArea_SQM"] = GetValue(row, "coverage_area_sqm"),
                        ["CostPerSQM_UGX"] = SafeParseDouble(GetValue(row, "cost_per_sqm_ugx")),
                        ["InstallationComplexity"] = GetValue(row, "installation_complexity"),
                        ["CodeReference"] = GetValue(row, "code_reference"),
                        ["OccupancyType"] = GetValue(row, "occupancy_type"),
                        ["WaterSupplyRequired"] = GetValue(row, "water_supply_required")
                    }
                };

                if (AddNodeSafe(node)) result.NodesCreated++;

                // Code reference edge
                var codeRef = GetValue(row, "code_reference");
                if (!string.IsNullOrEmpty(codeRef))
                {
                    var codeNodeId = $"STD-{codeRef.Replace(" ", "_")}";
                    AddNodeSafe(new KnowledgeNode
                    {
                        Id = codeNodeId, Name = codeRef, NodeType = "Standard",
                        Description = $"Fire code: {codeRef}"
                    });

                    if (AddEdgeSafe(new KnowledgeEdge
                    {
                        SourceId = nodeId, TargetId = codeNodeId,
                        RelationType = "CompliesWith", Strength = 1.0f
                    })) result.EdgesCreated++;
                }

                StoreFactSafe(new SemanticFact
                {
                    Id = nodeId,
                    Subject = name,
                    Predicate = "isFireProtectionSystem",
                    Object = codeRef,
                    Description = $"{name}: {GetValue(row, "application")} ({codeRef})",
                    Category = "FireProtection",
                    Source = result.FileName,
                    Confidence = 1.0f
                });
                result.FactsStored++;
                result.RowsProcessed++;
            }

            result.Success = true;
            _totalRowsProcessed += result.RowsProcessed;
            return Task.FromResult(result);
        }

        private Task<CsvIngestionResult> IngestDrainageSystemsAsync(
            string filePath, CancellationToken ct, IProgress<string> progress)
        {
            var result = new CsvIngestionResult { FileName = Path.GetFileNameWithoutExtension(filePath) };
            var rows = ReadCsv(filePath);

            foreach (var row in rows)
            {
                ct.ThrowIfCancellationRequested();
                var code = GetValue(row, "drainage_code");
                var name = GetValue(row, "system_name");
                if (string.IsNullOrEmpty(code)) continue;

                var nodeId = $"DRAIN-{code}";
                AddNodeSafe(new KnowledgeNode
                {
                    Id = nodeId, Name = name, NodeType = "DrainageSystem",
                    Description = $"{name} ({GetValue(row, "category")}/{GetValue(row, "subcategory")})",
                    Properties = new Dictionary<string, object>
                    {
                        ["Category"] = GetValue(row, "category"),
                        ["Size_MM"] = SafeParseDouble(GetValue(row, "size_mm")),
                        ["Material"] = GetValue(row, "material"),
                        ["SlopeMin_PCT"] = SafeParseDouble(GetValue(row, "slope_min_pct")),
                        ["Capacity_LPS"] = SafeParseDouble(GetValue(row, "capacity_lps")),
                        ["UnitCost_UGX"] = SafeParseDouble(GetValue(row, "unit_cost_ugx")),
                        ["Application"] = GetValue(row, "application"),
                        ["StandardReference"] = GetValue(row, "standard_reference")
                    }
                });
                result.NodesCreated++;

                StoreFactSafe(new SemanticFact
                {
                    Id = nodeId, Subject = name, Predicate = "isDrainageSystem",
                    Object = GetValue(row, "category"),
                    Description = $"{name}: {GetValue(row, "material")} {GetValue(row, "size_mm")}mm, {GetValue(row, "application")}",
                    Category = "DrainageSystem", Source = result.FileName, Confidence = 1.0f
                });
                result.FactsStored++;
                result.RowsProcessed++;
            }

            result.Success = true;
            _totalRowsProcessed += result.RowsProcessed;
            return Task.FromResult(result);
        }

        private Task<CsvIngestionResult> IngestSiteWorkAsync(
            string filePath, CancellationToken ct, IProgress<string> progress)
        {
            var result = new CsvIngestionResult { FileName = Path.GetFileNameWithoutExtension(filePath) };
            var rows = ReadCsv(filePath);

            foreach (var row in rows)
            {
                ct.ThrowIfCancellationRequested();
                var code = GetValue(row, "work_code");
                var name = GetValue(row, "work_name");
                if (string.IsNullOrEmpty(code)) continue;

                var nodeId = $"SITE-{code}";
                AddNodeSafe(new KnowledgeNode
                {
                    Id = nodeId, Name = name, NodeType = "SiteWork",
                    Description = $"{name} ({GetValue(row, "category")})",
                    Properties = new Dictionary<string, object>
                    {
                        ["Category"] = GetValue(row, "category"),
                        ["SubCategory"] = GetValue(row, "subcategory"),
                        ["Unit"] = GetValue(row, "unit"),
                        ["UnitRate_UGX"] = SafeParseDouble(GetValue(row, "unit_rate_ugx")),
                        ["EquipmentRequired"] = GetValue(row, "equipment_required"),
                        ["LaborIntensity"] = GetValue(row, "labor_intensity"),
                        ["Application"] = GetValue(row, "application")
                    }
                });
                result.NodesCreated++;

                StoreFactSafe(new SemanticFact
                {
                    Id = nodeId, Subject = name, Predicate = "isSiteWork",
                    Object = GetValue(row, "category"),
                    Description = $"{name}: {GetValue(row, "application")}, UGX {GetValue(row, "unit_rate_ugx")}/{GetValue(row, "unit")}",
                    Category = "SiteWork", Source = result.FileName, Confidence = 1.0f
                });
                result.FactsStored++;
                result.RowsProcessed++;
            }

            result.Success = true;
            _totalRowsProcessed += result.RowsProcessed;
            return Task.FromResult(result);
        }

        #endregion

        #region AI Training Data Parsers

        private Task<CsvIngestionResult> IngestTrainingIntentsAsync(
            string filePath, CancellationToken ct, IProgress<string> progress)
        {
            var result = new CsvIngestionResult { FileName = Path.GetFileNameWithoutExtension(filePath) };
            var rows = ReadCsv(filePath);

            foreach (var row in rows)
            {
                ct.ThrowIfCancellationRequested();
                var intentId = GetValue(row, "IntentId");
                var intentName = GetValue(row, "IntentName");
                if (string.IsNullOrEmpty(intentId)) continue;

                var nodeId = $"INTENT-{intentId}";
                AddNodeSafe(new KnowledgeNode
                {
                    Id = nodeId, Name = intentName, NodeType = "NLPIntent",
                    Description = GetValue(row, "Description"),
                    Properties = new Dictionary<string, object>
                    {
                        ["Category"] = GetValue(row, "Category"),
                        ["ExamplePhrases"] = GetValue(row, "ExamplePhrases"),
                        ["SlotNames"] = GetValue(row, "SlotNames"),
                        ["Priority"] = SafeParseInt(GetValue(row, "Priority")),
                        ["ConfidenceThreshold"] = SafeParseDouble(GetValue(row, "Confidence_Threshold"))
                    }
                });
                result.NodesCreated++;

                // Slot relationship edges
                var slots = GetValue(row, "SlotNames");
                if (!string.IsNullOrEmpty(slots))
                {
                    foreach (var slot in slots.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var slotNodeId = $"SLOT-{slot.Trim()}";
                        AddNodeSafe(new KnowledgeNode
                        {
                            Id = slotNodeId, Name = slot.Trim(), NodeType = "NLPSlot",
                            Description = $"Intent slot: {slot.Trim()}"
                        });

                        if (AddEdgeSafe(new KnowledgeEdge
                        {
                            SourceId = nodeId, TargetId = slotNodeId,
                            RelationType = "RequiresSlot", Strength = 0.9f
                        })) result.EdgesCreated++;
                    }
                }

                StoreFactSafe(new SemanticFact
                {
                    Id = nodeId, Subject = intentName, Predicate = "isNLPIntent",
                    Object = GetValue(row, "Category"),
                    Description = $"Intent {intentName}: {GetValue(row, "Description")} " +
                                  $"(examples: {GetValue(row, "ExamplePhrases")})",
                    Category = "NLPTraining", Source = result.FileName, Confidence = 1.0f
                });
                result.FactsStored++;
                result.RowsProcessed++;
            }

            result.Success = true;
            _totalRowsProcessed += result.RowsProcessed;
            return Task.FromResult(result);
        }

        private Task<CsvIngestionResult> IngestTrainingEntitiesAsync(
            string filePath, CancellationToken ct, IProgress<string> progress)
        {
            var result = new CsvIngestionResult { FileName = Path.GetFileNameWithoutExtension(filePath) };
            var rows = ReadCsv(filePath);

            foreach (var row in rows)
            {
                ct.ThrowIfCancellationRequested();
                var entity = GetValue(row, "Entity");
                var entityType = GetValue(row, "Type");
                if (string.IsNullOrEmpty(entity)) continue;

                var nodeId = $"ENTITY-{entity.Replace(" ", "_")}";
                AddNodeSafe(new KnowledgeNode
                {
                    Id = nodeId, Name = entity, NodeType = "NLPEntity",
                    Description = GetValue(row, "Description"),
                    Properties = new Dictionary<string, object>
                    {
                        ["Type"] = entityType,
                        ["Synonyms"] = GetValue(row, "Synonyms"),
                        ["AssociatedIntents"] = GetValue(row, "Associated_Intents")
                    }
                });
                result.NodesCreated++;

                // Intent association edges
                var intents = GetValue(row, "Associated_Intents");
                if (!string.IsNullOrEmpty(intents))
                {
                    foreach (var intent in intents.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var intentNodeId = $"INTENT-{intent.Trim()}";
                        if (AddEdgeSafe(new KnowledgeEdge
                        {
                            SourceId = nodeId, TargetId = intentNodeId,
                            RelationType = "AssociatedWithIntent", Strength = 0.8f
                        })) result.EdgesCreated++;
                    }
                }

                StoreFactSafe(new SemanticFact
                {
                    Id = nodeId, Subject = entity, Predicate = "isNLPEntity",
                    Object = entityType,
                    Description = $"Entity '{entity}' (type: {entityType}), synonyms: {GetValue(row, "Synonyms")}",
                    Category = "NLPTraining", Source = result.FileName, Confidence = 1.0f
                });
                result.FactsStored++;
                result.RowsProcessed++;
            }

            result.Success = true;
            _totalRowsProcessed += result.RowsProcessed;
            return Task.FromResult(result);
        }

        private Task<CsvIngestionResult> IngestTrainingUtterancesAsync(
            string filePath, CancellationToken ct, IProgress<string> progress)
        {
            var result = new CsvIngestionResult { FileName = Path.GetFileNameWithoutExtension(filePath) };
            var rows = ReadCsv(filePath);

            foreach (var row in rows)
            {
                ct.ThrowIfCancellationRequested();
                var uttId = GetValue(row, "UtteranceId");
                var text = GetValue(row, "Text");
                var intent = GetValue(row, "Intent");
                if (string.IsNullOrEmpty(uttId)) continue;

                var nodeId = $"UTT-{uttId}";
                AddNodeSafe(new KnowledgeNode
                {
                    Id = nodeId, Name = text, NodeType = "NLPUtterance",
                    Description = $"Utterance for intent {intent}: '{text}'",
                    Properties = new Dictionary<string, object>
                    {
                        ["Intent"] = intent,
                        ["Entities_JSON"] = GetValue(row, "Entities_JSON"),
                        ["Slots_JSON"] = GetValue(row, "Slots_JSON"),
                        ["Canonical"] = GetValue(row, "Canonical"),
                        ["Augmented"] = GetValue(row, "Augmented")
                    }
                });
                result.NodesCreated++;

                // Link to intent
                if (!string.IsNullOrEmpty(intent))
                {
                    if (AddEdgeSafe(new KnowledgeEdge
                    {
                        SourceId = nodeId,
                        TargetId = $"INTENT-{intent}",
                        RelationType = "ExemplifiesIntent",
                        Strength = 0.9f
                    })) result.EdgesCreated++;
                }

                result.RowsProcessed++;
            }

            result.Success = true;
            _totalRowsProcessed += result.RowsProcessed;
            return Task.FromResult(result);
        }

        private Task<CsvIngestionResult> IngestTrainingConversationsAsync(
            string filePath, CancellationToken ct, IProgress<string> progress)
        {
            return IngestGenericCsvAsync(filePath, ct, progress);
        }

        #endregion

        #region IoT Data Parsers

        private Task<CsvIngestionResult> IngestSensorTypesAsync(
            string filePath, CancellationToken ct, IProgress<string> progress)
        {
            var result = new CsvIngestionResult { FileName = Path.GetFileNameWithoutExtension(filePath) };
            var rows = ReadCsv(filePath);

            foreach (var row in rows)
            {
                ct.ThrowIfCancellationRequested();
                var sensorType = GetValue(row, "SensorType");
                if (string.IsNullOrEmpty(sensorType)) continue;

                var nodeId = $"SENSOR-{sensorType.Replace(" ", "_")}";
                AddNodeSafe(new KnowledgeNode
                {
                    Id = nodeId, Name = sensorType, NodeType = "SensorType",
                    Description = GetValue(row, "Description"),
                    Properties = new Dictionary<string, object>
                    {
                        ["Unit"] = GetValue(row, "Unit"),
                        ["MinValue"] = SafeParseDouble(GetValue(row, "MinValue")),
                        ["MaxValue"] = SafeParseDouble(GetValue(row, "MaxValue")),
                        ["DefaultPollIntervalMs"] = SafeParseInt(GetValue(row, "DefaultPollIntervalMs")),
                        ["Category"] = GetValue(row, "Category"),
                        ["Protocol"] = GetValue(row, "Protocol")
                    }
                });
                result.NodesCreated++;

                // Protocol edge
                var protocol = GetValue(row, "Protocol");
                if (!string.IsNullOrEmpty(protocol))
                {
                    var protoNodeId = $"PROTO-{protocol.Replace("/", "_")}";
                    AddNodeSafe(new KnowledgeNode
                    {
                        Id = protoNodeId, Name = protocol, NodeType = "BMSProtocol",
                        Description = $"Protocol: {protocol}"
                    });

                    if (AddEdgeSafe(new KnowledgeEdge
                    {
                        SourceId = nodeId, TargetId = protoNodeId,
                        RelationType = "UsesProtocol", Strength = 0.9f
                    })) result.EdgesCreated++;
                }

                StoreFactSafe(new SemanticFact
                {
                    Id = nodeId, Subject = sensorType, Predicate = "isSensorType",
                    Object = GetValue(row, "Category"),
                    Description = $"Sensor: {sensorType} ({GetValue(row, "Unit")}), range {GetValue(row, "MinValue")}-{GetValue(row, "MaxValue")}, {GetValue(row, "Description")}",
                    Category = "IoTSensor", Source = result.FileName, Confidence = 1.0f
                });
                result.FactsStored++;
                result.RowsProcessed++;
            }

            result.Success = true;
            _totalRowsProcessed += result.RowsProcessed;
            return Task.FromResult(result);
        }

        private Task<CsvIngestionResult> IngestBmsProtocolsAsync(
            string filePath, CancellationToken ct, IProgress<string> progress)
        {
            var result = new CsvIngestionResult { FileName = Path.GetFileNameWithoutExtension(filePath) };
            var rows = ReadCsv(filePath);

            foreach (var row in rows)
            {
                ct.ThrowIfCancellationRequested();
                var protocol = GetValue(row, "Protocol");
                if (string.IsNullOrEmpty(protocol)) continue;

                var nodeId = $"PROTO-{protocol.Replace("/", "_").Replace(" ", "_")}";
                AddNodeSafe(new KnowledgeNode
                {
                    Id = nodeId, Name = protocol, NodeType = "BMSProtocol",
                    Description = GetValue(row, "Description"),
                    Properties = new Dictionary<string, object>
                    {
                        ["PortDefault"] = SafeParseInt(GetValue(row, "PortDefault")),
                        ["DataFormat"] = GetValue(row, "DataFormat"),
                        ["MaxDevices"] = SafeParseInt(GetValue(row, "MaxDevices")),
                        ["Standard"] = GetValue(row, "Standard"),
                        ["PollingSupport"] = GetValue(row, "PollingSupport"),
                        ["EventSupport"] = GetValue(row, "EventSupport")
                    }
                });
                result.NodesCreated++;

                StoreFactSafe(new SemanticFact
                {
                    Id = nodeId, Subject = protocol, Predicate = "isBMSProtocol",
                    Object = GetValue(row, "Standard"),
                    Description = $"BMS Protocol: {protocol} - {GetValue(row, "Description")} (std: {GetValue(row, "Standard")})",
                    Category = "IoTProtocol", Source = result.FileName, Confidence = 1.0f
                });
                result.FactsStored++;
                result.RowsProcessed++;
            }

            result.Success = true;
            _totalRowsProcessed += result.RowsProcessed;
            return Task.FromResult(result);
        }

        private Task<CsvIngestionResult> IngestSustainabilityBenchmarksAsync(
            string filePath, CancellationToken ct, IProgress<string> progress)
        {
            var result = new CsvIngestionResult { FileName = Path.GetFileNameWithoutExtension(filePath) };
            var rows = ReadCsv(filePath);

            foreach (var row in rows)
            {
                ct.ThrowIfCancellationRequested();
                var buildingType = GetValue(row, "BuildingType");
                var standard = GetValue(row, "Standard");
                var metric = GetValue(row, "Metric");
                if (string.IsNullOrEmpty(buildingType)) continue;

                var nodeId = $"BENCH-{buildingType.Replace(" ", "_")}_{metric}";
                AddNodeSafe(new KnowledgeNode
                {
                    Id = nodeId, Name = $"{buildingType} {metric} Benchmark",
                    NodeType = "SustainabilityBenchmark",
                    Description = GetValue(row, "Description"),
                    Properties = new Dictionary<string, object>
                    {
                        ["BuildingType"] = buildingType,
                        ["Standard"] = standard,
                        ["Metric"] = metric,
                        ["Unit"] = GetValue(row, "Unit"),
                        ["Baseline"] = SafeParseDouble(GetValue(row, "Baseline")),
                        ["Good"] = SafeParseDouble(GetValue(row, "Good")),
                        ["Excellent"] = SafeParseDouble(GetValue(row, "Excellent")),
                        ["Region"] = GetValue(row, "Region")
                    }
                });
                result.NodesCreated++;

                StoreFactSafe(new SemanticFact
                {
                    Id = nodeId, Subject = $"{buildingType} {metric}",
                    Predicate = "hasBenchmark",
                    Object = $"Baseline={GetValue(row, "Baseline")}, Good={GetValue(row, "Good")}, Excellent={GetValue(row, "Excellent")} {GetValue(row, "Unit")}",
                    Description = $"{buildingType} {metric}: baseline {GetValue(row, "Baseline")}, " +
                                  $"good {GetValue(row, "Good")}, excellent {GetValue(row, "Excellent")} {GetValue(row, "Unit")} ({standard})",
                    Category = "Sustainability", Source = result.FileName, Confidence = 1.0f
                });
                result.FactsStored++;
                result.RowsProcessed++;
            }

            result.Success = true;
            _totalRowsProcessed += result.RowsProcessed;
            return Task.FromResult(result);
        }

        private Task<CsvIngestionResult> IngestEnergyEmissionFactorsAsync(
            string filePath, CancellationToken ct, IProgress<string> progress)
        {
            var result = new CsvIngestionResult { FileName = Path.GetFileNameWithoutExtension(filePath) };
            var rows = ReadCsv(filePath);

            foreach (var row in rows)
            {
                ct.ThrowIfCancellationRequested();
                var country = GetValue(row, "Country");
                if (string.IsNullOrEmpty(country)) continue;

                var nodeId = $"EMISSION-{country.Replace(" ", "_")}";
                AddNodeSafe(new KnowledgeNode
                {
                    Id = nodeId, Name = $"{country} Emission Factors",
                    NodeType = "EmissionFactor",
                    Description = $"Grid emission: {GetValue(row, "GridEmissionFactor_kgCO2_per_kWh")} kgCO2/kWh",
                    Properties = new Dictionary<string, object>
                    {
                        ["Country"] = country,
                        ["Region"] = GetValue(row, "Region"),
                        ["GridEmissionFactor"] = SafeParseDouble(GetValue(row, "GridEmissionFactor_kgCO2_per_kWh")),
                        ["NaturalGas"] = SafeParseDouble(GetValue(row, "NaturalGas_kgCO2_per_kWh")),
                        ["GridRenewablePct"] = SafeParseDouble(GetValue(row, "GridRenewablePct")),
                        ["Year"] = GetValue(row, "Year"),
                        ["Source"] = GetValue(row, "Source")
                    }
                });
                result.NodesCreated++;

                StoreFactSafe(new SemanticFact
                {
                    Id = nodeId, Subject = country, Predicate = "hasEmissionFactor",
                    Object = $"{GetValue(row, "GridEmissionFactor_kgCO2_per_kWh")} kgCO2/kWh",
                    Description = $"{country} ({GetValue(row, "Region")}): grid emission {GetValue(row, "GridEmissionFactor_kgCO2_per_kWh")} kgCO2/kWh, {GetValue(row, "GridRenewablePct")}% renewable",
                    Category = "EmissionFactor", Source = result.FileName, Confidence = 1.0f
                });
                result.FactsStored++;
                result.RowsProcessed++;
            }

            result.Success = true;
            _totalRowsProcessed += result.RowsProcessed;
            return Task.FromResult(result);
        }

        private Task<CsvIngestionResult> IngestCommissioningChecklistsAsync(
            string filePath, CancellationToken ct, IProgress<string> progress)
        {
            var result = new CsvIngestionResult { FileName = Path.GetFileNameWithoutExtension(filePath) };
            var rows = ReadCsv(filePath);

            foreach (var row in rows)
            {
                ct.ThrowIfCancellationRequested();
                var checkId = GetValue(row, "CheckId");
                var checkItem = GetValue(row, "CheckItem");
                if (string.IsNullOrEmpty(checkId)) continue;

                var nodeId = $"COMM-{checkId}";
                AddNodeSafe(new KnowledgeNode
                {
                    Id = nodeId, Name = checkItem, NodeType = "CommissioningCheck",
                    Description = GetValue(row, "Requirement"),
                    Properties = new Dictionary<string, object>
                    {
                        ["SystemType"] = GetValue(row, "SystemType"),
                        ["Phase"] = GetValue(row, "Phase"),
                        ["Severity"] = GetValue(row, "Severity"),
                        ["TestMethod"] = GetValue(row, "TestMethod"),
                        ["AcceptanceCriteria"] = GetValue(row, "AcceptanceCriteria"),
                        ["Standard"] = GetValue(row, "Standard")
                    }
                });
                result.NodesCreated++;

                StoreFactSafe(new SemanticFact
                {
                    Id = nodeId, Subject = checkItem, Predicate = "isCommissioningCheck",
                    Object = GetValue(row, "SystemType"),
                    Description = $"Commissioning: {checkItem} ({GetValue(row, "Phase")}): {GetValue(row, "Requirement")}",
                    Category = "Commissioning", Source = result.FileName, Confidence = 1.0f
                });
                result.FactsStored++;
                result.RowsProcessed++;
            }

            result.Success = true;
            _totalRowsProcessed += result.RowsProcessed;
            return Task.FromResult(result);
        }

        #endregion

        #region Additional Parsers (Family, Construction, etc.)

        private Task<CsvIngestionResult> IngestFamilyParametersAsync(
            string filePath, CancellationToken ct, IProgress<string> progress)
        {
            return IngestGenericCsvAsync(filePath, ct, progress);
        }

        private Task<CsvIngestionResult> IngestFamilyPlacementRulesAsync(
            string filePath, CancellationToken ct, IProgress<string> progress)
        {
            return IngestGenericCsvAsync(filePath, ct, progress);
        }

        private Task<CsvIngestionResult> IngestFamilyRelationshipsAsync(
            string filePath, CancellationToken ct, IProgress<string> progress)
        {
            return IngestGenericCsvAsync(filePath, ct, progress);
        }

        private Task<CsvIngestionResult> IngestCodeRequirementsAsync(
            string filePath, CancellationToken ct, IProgress<string> progress)
        {
            // Uses same format as building codes
            return IngestBuildingCodesAsync(filePath, ct, progress);
        }

        private Task<CsvIngestionResult> IngestConstructionAssembliesAsync(
            string filePath, CancellationToken ct, IProgress<string> progress)
        {
            return IngestGenericCsvAsync(filePath, ct, progress);
        }

        private Task<CsvIngestionResult> IngestConstructionDetailsAsync(
            string filePath, CancellationToken ct, IProgress<string> progress)
        {
            return IngestGenericCsvAsync(filePath, ct, progress);
        }

        private Task<CsvIngestionResult> IngestEntityDictionaryAsync(
            string filePath, CancellationToken ct, IProgress<string> progress)
        {
            return IngestTrainingEntitiesAsync(filePath, ct, progress);
        }

        private Task<CsvIngestionResult> IngestFurnitureAsync(
            string filePath, CancellationToken ct, IProgress<string> progress)
        {
            return IngestGenericCsvAsync(filePath, ct, progress);
        }

        private Task<CsvIngestionResult> IngestCaseStudiesAsync(
            string filePath, CancellationToken ct, IProgress<string> progress)
        {
            return IngestGenericCsvAsync(filePath, ct, progress);
        }

        private Task<CsvIngestionResult> IngestBuildingComponentsAsync(
            string filePath, CancellationToken ct, IProgress<string> progress)
        {
            return IngestGenericCsvAsync(filePath, ct, progress);
        }

        private Task<CsvIngestionResult> IngestBleMaterialsAsync(
            string filePath, CancellationToken ct, IProgress<string> progress)
        {
            return IngestGenericCsvAsync(filePath, ct, progress);
        }

        private Task<CsvIngestionResult> IngestMepMaterialsAsync(
            string filePath, CancellationToken ct, IProgress<string> progress)
        {
            return IngestGenericCsvAsync(filePath, ct, progress);
        }

        #endregion

        #region Generic CSV Parser (Fallback)

        /// <summary>
        /// Generic fallback parser that creates one node per row using the first column as ID
        /// and all other columns as properties. Creates a semantic fact from all text fields.
        /// </summary>
        private Task<CsvIngestionResult> IngestGenericCsvAsync(
            string filePath, CancellationToken ct, IProgress<string> progress)
        {
            var result = new CsvIngestionResult { FileName = Path.GetFileNameWithoutExtension(filePath) };

            try
            {
                var rows = ReadCsv(filePath);
                if (rows.Count == 0)
                {
                    result.Success = true;
                    return Task.FromResult(result);
                }

                var headers = rows[0].Keys.ToList();
                var idColumn = headers.FirstOrDefault(h =>
                    h.EndsWith("ID", StringComparison.OrdinalIgnoreCase) ||
                    h.EndsWith("Id", StringComparison.OrdinalIgnoreCase) ||
                    h.EndsWith("Code", StringComparison.OrdinalIgnoreCase) ||
                    h.Equals("ID", StringComparison.OrdinalIgnoreCase));

                var nameColumn = headers.FirstOrDefault(h =>
                    h.Equals("Name", StringComparison.OrdinalIgnoreCase) ||
                    h.Contains("Name", StringComparison.OrdinalIgnoreCase));

                int rowIndex = 0;
                foreach (var row in rows)
                {
                    ct.ThrowIfCancellationRequested();
                    rowIndex++;

                    var id = idColumn != null ? GetValue(row, idColumn) : $"ROW-{rowIndex}";
                    var name = nameColumn != null ? GetValue(row, nameColumn) : id;

                    if (string.IsNullOrEmpty(id)) continue;

                    var nodeId = $"GEN-{result.FileName}-{id}".Replace(" ", "_");
                    var props = new Dictionary<string, object>();
                    var descParts = new List<string>();

                    foreach (var kvp in row)
                    {
                        if (!string.IsNullOrWhiteSpace(kvp.Value))
                        {
                            props[kvp.Key] = kvp.Value;
                            if (kvp.Key != idColumn && kvp.Key != nameColumn)
                            {
                                descParts.Add($"{kvp.Key}: {kvp.Value}");
                            }
                        }
                    }

                    var description = descParts.Count > 0
                        ? string.Join(", ", descParts.Take(5))
                        : name;

                    AddNodeSafe(new KnowledgeNode
                    {
                        Id = nodeId,
                        Name = name,
                        NodeType = "GenericData",
                        Description = description,
                        Properties = props
                    });
                    result.NodesCreated++;

                    // Store comprehensive fact
                    StoreFactSafe(new SemanticFact
                    {
                        Id = nodeId,
                        Subject = name,
                        Predicate = "isDataFrom",
                        Object = result.FileName,
                        Description = $"{name}: {description}",
                        Category = result.FileName,
                        Source = result.FileName,
                        Confidence = 0.8f
                    });
                    result.FactsStored++;
                    result.RowsProcessed++;
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"Error in generic CSV parsing: {filePath}");
                result.ErrorMessage = ex.Message;
            }

            result.Success = true;
            _totalRowsProcessed += result.RowsProcessed;
            return Task.FromResult(result);
        }

        #endregion

        #region Reporting

        /// <summary>
        /// Builds a comprehensive ingestion report from all processed files.
        /// </summary>
        private IngestionReport BuildReport(DateTime startTime)
        {
            var report = new IngestionReport
            {
                StartedAt = startTime,
                CompletedAt = DateTime.UtcNow,
                DataDirectory = _dataDirectory,
                TotalNodesCreated = _totalNodesCreated,
                TotalEdgesCreated = _totalEdgesCreated,
                TotalFactsStored = _totalFactsStored,
                TotalRowsProcessed = _totalRowsProcessed,
                FilesProcessed = _totalFilesProcessed,
                FileResults = _ingestionResults.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value,
                    StringComparer.OrdinalIgnoreCase)
            };

            // Compute per-domain breakdown
            report.DomainBreakdown = _ingestionResults.Values
                .Where(r => r.Success)
                .GroupBy(r => ClassifyDomain(r.FileName))
                .ToDictionary(
                    g => g.Key,
                    g => new DomainIngestionStats
                    {
                        Domain = g.Key,
                        FilesProcessed = g.Count(),
                        NodesCreated = g.Sum(r => r.NodesCreated),
                        EdgesCreated = g.Sum(r => r.EdgesCreated),
                        FactsStored = g.Sum(r => r.FactsStored),
                        RowsProcessed = g.Sum(r => r.RowsProcessed)
                    },
                    StringComparer.OrdinalIgnoreCase);

            return report;
        }

        /// <summary>
        /// Classifies a CSV file name into a knowledge domain.
        /// </summary>
        private string ClassifyDomain(string fileName)
        {
            var upper = (fileName ?? "").ToUpperInvariant();

            if (upper.Contains("BUILDING_CODE") || upper.Contains("CODE_REQ") || upper.Contains("DESIGN_RULE"))
                return "BuildingCodes";
            if (upper.Contains("ROOM") || upper.Contains("SPATIAL") || upper.Contains("ADJACENCY"))
                return "SpatialPlanning";
            if (upper.Contains("WALL") || upper.Contains("FLOOR") || upper.Contains("CONSTRUCTION"))
                return "BuildingMaterials";
            if (upper.Contains("MEP") || upper.Contains("PLUMBING") || upper.Contains("HVAC"))
                return "MEPSystems";
            if (upper.Contains("FAMILY") || upper.Contains("PARAMETER"))
                return "RevitFamilies";
            if (upper.Contains("COST") || upper.Contains("LABOR") || upper.Contains("EQUIPMENT"))
                return "CostData";
            if (upper.Contains("FIRE") || upper.Contains("SAFETY") || upper.Contains("ACCESSIBILITY"))
                return "SafetyCompliance";
            if (upper.Contains("AI_TRAINING") || upper.Contains("ENTITY") || upper.Contains("INTENT") || upper.Contains("UTTERANCE"))
                return "NLPTraining";
            if (upper.Contains("SENSOR") || upper.Contains("BMS") || upper.Contains("IOT") || upper.Contains("EMISSION") || upper.Contains("SUSTAINABILITY"))
                return "IoTSustainability";
            if (upper.Contains("DRAINAGE") || upper.Contains("SITE_WORK"))
                return "SiteInfrastructure";
            if (upper.Contains("FURNITURE"))
                return "Furniture";
            if (upper.Contains("CASE_STUD"))
                return "CaseStudies";

            return "General";
        }

        /// <summary>
        /// Gets the current ingestion totals (can be called during ingestion for live progress).
        /// </summary>
        public IngestionProgress GetCurrentProgress()
        {
            return new IngestionProgress
            {
                NodesCreated = _totalNodesCreated,
                EdgesCreated = _totalEdgesCreated,
                FactsStored = _totalFactsStored,
                FilesProcessed = _totalFilesProcessed,
                RowsProcessed = _totalRowsProcessed
            };
        }

        #endregion
    }

    #endregion

    #region Ingestion Result Types

    /// <summary>
    /// Result of ingesting a single CSV file.
    /// </summary>
    public class CsvIngestionResult
    {
        public string FileName { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public int NodesCreated { get; set; }
        public int EdgesCreated { get; set; }
        public int FactsStored { get; set; }
        public int RowsProcessed { get; set; }
    }

    /// <summary>
    /// Comprehensive report from a full ingestion run.
    /// </summary>
    public class IngestionReport
    {
        public DateTime StartedAt { get; set; }
        public DateTime CompletedAt { get; set; }
        public TimeSpan Duration => CompletedAt - StartedAt;
        public string DataDirectory { get; set; }
        public int TotalNodesCreated { get; set; }
        public int TotalEdgesCreated { get; set; }
        public int TotalFactsStored { get; set; }
        public int TotalRowsProcessed { get; set; }
        public int FilesProcessed { get; set; }
        public Dictionary<string, CsvIngestionResult> FileResults { get; set; } = new Dictionary<string, CsvIngestionResult>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, DomainIngestionStats> DomainBreakdown { get; set; } = new Dictionary<string, DomainIngestionStats>(StringComparer.OrdinalIgnoreCase);

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== CSV Knowledge Ingestion Report ===");
            sb.AppendLine($"Duration: {Duration.TotalSeconds:F1} seconds");
            sb.AppendLine($"Files Processed: {FilesProcessed}");
            sb.AppendLine($"Rows Processed: {TotalRowsProcessed:N0}");
            sb.AppendLine($"Nodes Created: {TotalNodesCreated:N0}");
            sb.AppendLine($"Edges Created: {TotalEdgesCreated:N0}");
            sb.AppendLine($"Facts Stored: {TotalFactsStored:N0}");
            sb.AppendLine();
            sb.AppendLine("Domain Breakdown:");
            foreach (var domain in DomainBreakdown.OrderByDescending(d => d.Value.NodesCreated))
            {
                sb.AppendLine($"  {domain.Key}: {domain.Value.NodesCreated} nodes, " +
                              $"{domain.Value.EdgesCreated} edges, " +
                              $"{domain.Value.FactsStored} facts ({domain.Value.FilesProcessed} files)");
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// Statistics for a single knowledge domain.
    /// </summary>
    public class DomainIngestionStats
    {
        public string Domain { get; set; }
        public int FilesProcessed { get; set; }
        public int NodesCreated { get; set; }
        public int EdgesCreated { get; set; }
        public int FactsStored { get; set; }
        public int RowsProcessed { get; set; }
    }

    /// <summary>
    /// Live progress of an ongoing ingestion.
    /// </summary>
    public class IngestionProgress
    {
        public int NodesCreated { get; set; }
        public int EdgesCreated { get; set; }
        public int FactsStored { get; set; }
        public int FilesProcessed { get; set; }
        public int RowsProcessed { get; set; }
    }

    #endregion
}
