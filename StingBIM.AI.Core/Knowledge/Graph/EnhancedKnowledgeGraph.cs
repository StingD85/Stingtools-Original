// =========================================================================
// StingBIM.AI.Knowledge - Enhanced Knowledge Graph for Phase 2
// Full graph construction with room types, relationships, and reasoning
// =========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using NLog;
using StingBIM.AI.Core.Common;

namespace StingBIM.AI.Knowledge.Graph
{
    /// <summary>
    /// Enhanced knowledge graph with comprehensive BIM domain knowledge.
    /// Supports path finding, constraint propagation, and conflict detection.
    /// </summary>
    public class EnhancedKnowledgeGraph
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly Dictionary<string, GraphNode> _nodes;
        private readonly Dictionary<string, GraphEdge> _edges;
        private readonly Dictionary<string, List<string>> _adjacencyList;
        private readonly Dictionary<string, List<string>> _reverseAdjacencyList;
        private readonly Dictionary<string, NodeIndex> _indices;
        private readonly GraphSchema _schema;

        public int NodeCount => _nodes.Count;
        public int EdgeCount => _edges.Count;

        public EnhancedKnowledgeGraph()
        {
            _nodes = new Dictionary<string, GraphNode>();
            _edges = new Dictionary<string, GraphEdge>();
            _adjacencyList = new Dictionary<string, List<string>>();
            _reverseAdjacencyList = new Dictionary<string, List<string>>();
            _indices = new Dictionary<string, NodeIndex>();
            _schema = new GraphSchema();

            InitializeSchema();
            LoadBuiltInKnowledge();
        }

        #region Initialization

        private void InitializeSchema()
        {
            // Define node types
            _schema.NodeTypes.Add(new NodeTypeDefinition
            {
                TypeId = "RoomType",
                Properties = new[] { "Name", "Category", "MinArea", "MaxArea", "RequiresWindow", "RequiresPlumbing" },
                RequiredProperties = new[] { "Name", "Category" }
            });

            _schema.NodeTypes.Add(new NodeTypeDefinition
            {
                TypeId = "Element",
                Properties = new[] { "Name", "Category", "Family", "Type" },
                RequiredProperties = new[] { "Name", "Category" }
            });

            _schema.NodeTypes.Add(new NodeTypeDefinition
            {
                TypeId = "Material",
                Properties = new[] { "Name", "Category", "Density", "ThermalConductivity", "Cost" },
                RequiredProperties = new[] { "Name", "Category" }
            });

            _schema.NodeTypes.Add(new NodeTypeDefinition
            {
                TypeId = "Standard",
                Properties = new[] { "Code", "Title", "Version", "Region" },
                RequiredProperties = new[] { "Code", "Title" }
            });

            _schema.NodeTypes.Add(new NodeTypeDefinition
            {
                TypeId = "Concept",
                Properties = new[] { "Name", "Domain", "Description" },
                RequiredProperties = new[] { "Name" }
            });

            // Define edge types
            _schema.EdgeTypes.Add(new EdgeTypeDefinition
            {
                TypeId = "SpatialRelation",
                Properties = new[] { "RelationType", "Strength", "MinDistance", "MaxDistance", "RequiresAccess" },
                AllowedSourceTypes = new[] { "RoomType" },
                AllowedTargetTypes = new[] { "RoomType" }
            });

            _schema.EdgeTypes.Add(new EdgeTypeDefinition
            {
                TypeId = "Requires",
                Properties = new[] { "Condition", "Priority" },
                AllowedSourceTypes = new[] { "RoomType", "Element" },
                AllowedTargetTypes = new[] { "Element", "Material", "Concept" }
            });

            _schema.EdgeTypes.Add(new EdgeTypeDefinition
            {
                TypeId = "Contains",
                Properties = new[] { "Typical", "Quantity" },
                AllowedSourceTypes = new[] { "RoomType", "Element" },
                AllowedTargetTypes = new[] { "Element" }
            });

            _schema.EdgeTypes.Add(new EdgeTypeDefinition
            {
                TypeId = "RegulatedBy",
                Properties = new[] { "Section", "Requirement" },
                AllowedSourceTypes = new[] { "RoomType", "Element", "Concept" },
                AllowedTargetTypes = new[] { "Standard" }
            });

            _schema.EdgeTypes.Add(new EdgeTypeDefinition
            {
                TypeId = "MadeOf",
                Properties = new[] { "Layer", "Thickness" },
                AllowedSourceTypes = new[] { "Element" },
                AllowedTargetTypes = new[] { "Material" }
            });

            _schema.EdgeTypes.Add(new EdgeTypeDefinition
            {
                TypeId = "IsA",
                Properties = new string[] { },
                AllowedSourceTypes = new[] { "RoomType", "Element", "Material", "Concept" },
                AllowedTargetTypes = new[] { "Concept" }
            });

            _schema.EdgeTypes.Add(new EdgeTypeDefinition
            {
                TypeId = "CausalRelation",
                Properties = new[] { "Cause", "Effect", "Strength", "Reversibility", "TimeToManifest", "MitigationStrategy" },
                AllowedSourceTypes = new[] { "Concept", "Element", "RoomType", "Material" },
                AllowedTargetTypes = new[] { "Concept", "Element", "RoomType", "Material" }
            });
        }

        private void LoadBuiltInKnowledge()
        {
            // Load room type concepts
            LoadRoomTypeHierarchy();

            // Load element concepts
            LoadElementHierarchy();

            // Load spatial relationship rules
            LoadSpatialRules();

            // Load standard requirements (all 32 standards)
            LoadStandardRequirements();

            // Load MEP system hierarchy
            LoadMEPHierarchy();

            // Load additional commercial/institutional room types
            LoadAdditionalRoomTypes();

            // Load material hierarchy with properties
            LoadMaterialHierarchy();

            // Load causal knowledge for reasoning
            LoadCausalKnowledge();

            // Build indices
            RebuildIndices();
        }

        private void LoadRoomTypeHierarchy()
        {
            // High-level categories
            AddNode(new GraphNode
            {
                NodeId = "CAT_LIVING",
                NodeType = "Concept",
                Properties = new Dictionary<string, object>
                {
                    ["Name"] = "Living Spaces",
                    ["Domain"] = "Architecture",
                    ["Description"] = "Spaces for habitation and daily living"
                }
            });

            AddNode(new GraphNode
            {
                NodeId = "CAT_SERVICE",
                NodeType = "Concept",
                Properties = new Dictionary<string, object>
                {
                    ["Name"] = "Service Spaces",
                    ["Domain"] = "Architecture",
                    ["Description"] = "Support and utility spaces"
                }
            });

            AddNode(new GraphNode
            {
                NodeId = "CAT_CIRCULATION",
                NodeType = "Concept",
                Properties = new Dictionary<string, object>
                {
                    ["Name"] = "Circulation Spaces",
                    ["Domain"] = "Architecture",
                    ["Description"] = "Movement and access spaces"
                }
            });

            // Room types with full properties
            var roomTypes = new[]
            {
                ("RT_KITCHEN", "Kitchen", "CAT_SERVICE", 8.0, 25.0, 2.4, true, true, true),
                ("RT_LIVING", "Living Room", "CAT_LIVING", 15.0, 40.0, 2.7, true, false, false),
                ("RT_DINING", "Dining Room", "CAT_LIVING", 10.0, 25.0, 2.6, true, false, false),
                ("RT_BEDROOM", "Bedroom", "CAT_LIVING", 9.0, 25.0, 2.4, true, false, false),
                ("RT_BATHROOM", "Bathroom", "CAT_SERVICE", 3.0, 9.0, 2.4, false, true, true),
                ("RT_ENTRANCE", "Entrance Hall", "CAT_CIRCULATION", 4.0, 15.0, 2.4, false, false, false),
                ("RT_CORRIDOR", "Corridor", "CAT_CIRCULATION", 2.0, 10.0, 2.4, false, false, false),
                ("RT_STAIRS", "Stairwell", "CAT_CIRCULATION", 4.0, 12.0, 5.0, false, false, false),
                ("RT_UTILITY", "Utility Room", "CAT_SERVICE", 4.0, 10.0, 2.4, false, true, true),
                ("RT_STORAGE", "Storage", "CAT_SERVICE", 2.0, 8.0, 2.4, false, false, false),
                ("RT_GARAGE", "Garage", "CAT_SERVICE", 15.0, 45.0, 2.4, false, false, false),
                ("RT_OFFICE", "Home Office", "CAT_LIVING", 8.0, 20.0, 2.4, true, false, false),
                ("RT_TERRACE", "Terrace", "CAT_LIVING", 8.0, 40.0, 0.0, false, false, false),
            };

            foreach (var (id, name, category, minArea, maxArea, minHeight, needsWindow, needsPlumbing, needsVent) in roomTypes)
            {
                AddNode(new GraphNode
                {
                    NodeId = id,
                    NodeType = "RoomType",
                    Properties = new Dictionary<string, object>
                    {
                        ["Name"] = name,
                        ["Category"] = category,
                        ["MinArea"] = minArea,
                        ["MaxArea"] = maxArea,
                        ["MinHeight"] = minHeight,
                        ["RequiresWindow"] = needsWindow,
                        ["RequiresPlumbing"] = needsPlumbing,
                        ["RequiresVentilation"] = needsVent
                    }
                });

                // Link to category
                AddEdge(new GraphEdge
                {
                    EdgeId = $"E_{id}_ISA_{category}",
                    EdgeType = "IsA",
                    SourceNodeId = id,
                    TargetNodeId = category,
                    Properties = new Dictionary<string, object>()
                });
            }
        }

        private void LoadElementHierarchy()
        {
            // Element categories
            var categories = new[]
            {
                ("ELEM_STRUCTURAL", "Structural Elements", "Load-bearing building components"),
                ("ELEM_ENVELOPE", "Building Envelope", "External enclosure elements"),
                ("ELEM_INTERIOR", "Interior Elements", "Internal finishing elements"),
                ("ELEM_MEP", "MEP Elements", "Mechanical, electrical, plumbing")
            };

            foreach (var (id, name, desc) in categories)
            {
                AddNode(new GraphNode
                {
                    NodeId = id,
                    NodeType = "Concept",
                    Properties = new Dictionary<string, object>
                    {
                        ["Name"] = name,
                        ["Domain"] = "Construction",
                        ["Description"] = desc
                    }
                });
            }

            // Element types
            var elements = new[]
            {
                ("EL_WALL", "Wall", "ELEM_ENVELOPE"),
                ("EL_FLOOR", "Floor", "ELEM_STRUCTURAL"),
                ("EL_ROOF", "Roof", "ELEM_ENVELOPE"),
                ("EL_COLUMN", "Column", "ELEM_STRUCTURAL"),
                ("EL_BEAM", "Beam", "ELEM_STRUCTURAL"),
                ("EL_DOOR", "Door", "ELEM_INTERIOR"),
                ("EL_WINDOW", "Window", "ELEM_ENVELOPE"),
                ("EL_STAIR", "Stair", "ELEM_INTERIOR"),
                ("EL_CEILING", "Ceiling", "ELEM_INTERIOR"),
                ("EL_DUCT", "Duct", "ELEM_MEP"),
                ("EL_PIPE", "Pipe", "ELEM_MEP"),
                ("EL_CONDUIT", "Conduit", "ELEM_MEP"),
            };

            foreach (var (id, name, category) in elements)
            {
                AddNode(new GraphNode
                {
                    NodeId = id,
                    NodeType = "Element",
                    Properties = new Dictionary<string, object>
                    {
                        ["Name"] = name,
                        ["Category"] = category
                    }
                });

                AddEdge(new GraphEdge
                {
                    EdgeId = $"E_{id}_ISA_{category}",
                    EdgeType = "IsA",
                    SourceNodeId = id,
                    TargetNodeId = category,
                    Properties = new Dictionary<string, object>()
                });
            }
        }

        private void LoadSpatialRules()
        {
            // Define spatial relationships between room types
            var spatialRules = new[]
            {
                ("RT_KITCHEN", "RT_DINING", "Adjacent", 0.95, 0, 3, true),
                ("RT_KITCHEN", "RT_LIVING", "Adjacent", 0.80, 0, 5, false),
                ("RT_KITCHEN", "RT_UTILITY", "Near", 0.75, 0, 8, false),
                ("RT_KITCHEN", "RT_BEDROOM", "Avoid", 0.85, 8, 999, false),
                ("RT_LIVING", "RT_DINING", "Adjacent", 0.90, 0, 5, false),
                ("RT_LIVING", "RT_ENTRANCE", "Adjacent", 0.85, 0, 4, true),
                ("RT_LIVING", "RT_TERRACE", "Adjacent", 0.85, 0, 2, true),
                ("RT_BEDROOM", "RT_BATHROOM", "Near", 0.85, 0, 6, false),
                ("RT_BEDROOM", "RT_LIVING", "Separated", 0.75, 5, 15, false),
                ("RT_BATHROOM", "RT_BEDROOM", "Near", 0.85, 0, 6, false),
                ("RT_BATHROOM", "RT_KITCHEN", "Avoid", 0.70, 5, 999, false),
                ("RT_ENTRANCE", "RT_STAIRS", "Adjacent", 0.90, 0, 3, true),
                ("RT_GARAGE", "RT_KITCHEN", "Near", 0.70, 3, 10, false),
                ("RT_GARAGE", "RT_BEDROOM", "Avoid", 0.85, 10, 999, false),
                ("RT_OFFICE", "RT_LIVING", "Separated", 0.70, 5, 12, false),
                ("RT_UTILITY", "RT_KITCHEN", "Near", 0.80, 0, 8, false),
            };

            foreach (var (source, target, relType, strength, minDist, maxDist, needsAccess) in spatialRules)
            {
                var edgeId = $"SR_{source}_{target}_{relType}";
                AddEdge(new GraphEdge
                {
                    EdgeId = edgeId,
                    EdgeType = "SpatialRelation",
                    SourceNodeId = source,
                    TargetNodeId = target,
                    Properties = new Dictionary<string, object>
                    {
                        ["RelationType"] = relType,
                        ["Strength"] = strength,
                        ["MinDistance"] = minDist,
                        ["MaxDistance"] = maxDist,
                        ["RequiresAccess"] = needsAccess
                    }
                });
            }
        }

        private void LoadStandardRequirements()
        {
            // All 32 international building standards
            var standards = new[]
            {
                // US Codes
                ("STD_IBC", "IBC 2021", "International Building Code", "2021", "US"),
                ("STD_IMC", "IMC 2021", "International Mechanical Code", "2021", "US"),
                ("STD_IPC", "IPC 2021", "International Plumbing Code", "2021", "US"),
                ("STD_NEC", "NEC 2023", "National Electrical Code", "2023", "US"),
                ("STD_ADA", "ADA", "Americans with Disabilities Act", "2010", "US"),
                // HVAC/Energy
                ("STD_ASHRAE_901", "ASHRAE 90.1", "Energy Standard for Buildings", "2022", "International"),
                ("STD_ASHRAE_621", "ASHRAE 62.1", "Ventilation for Acceptable IAQ", "2022", "International"),
                ("STD_CIBSE", "CIBSE", "Building Services Engineers Guide", "2021", "UK"),
                ("STD_SMACNA", "SMACNA", "Sheet Metal and Air Conditioning", "2020", "International"),
                // Structural
                ("STD_ASCE7", "ASCE 7", "Minimum Design Loads", "2022", "US"),
                ("STD_ACI318", "ACI 318", "Concrete Design Code", "2019", "US"),
                ("STD_EUROCODE", "Eurocodes", "European Structural Design Standards", "2024", "EU"),
                ("STD_EUROCODE_COMP", "Eurocodes Complete", "Extended Eurocode Coverage", "2024", "EU"),
                ("STD_BS6399", "BS 6399", "Loading for Buildings", "1996", "UK"),
                ("STD_BS_STRUCT", "BS Structural", "British Structural Codes", "2015", "UK"),
                // Electrical
                ("STD_BS7671", "BS 7671", "IET Wiring Regulations", "2022", "UK"),
                ("STD_IEEE", "IEEE", "Electrical and Electronics Standards", "2023", "International"),
                // Fire Protection
                ("STD_NFPA", "NFPA", "Fire Protection Standards", "2023", "International"),
                ("STD_NFPA_ADD", "NFPA Additional", "Extended Fire Protection", "2023", "International"),
                ("STD_BS9999", "BS 9999", "Fire Safety in Buildings", "2017", "UK"),
                // Materials/Testing
                ("STD_ASTM", "ASTM", "Materials Testing Standards", "2023", "International"),
                // British Comprehensive
                ("STD_BS_COMP", "BS Comprehensive", "BS EN Standards Suite", "2022", "UK"),
                // Green/Sustainability
                ("STD_GREEN", "Green Building", "Sustainability Standards", "2023", "International"),
                // ISO
                ("STD_ISO19650", "ISO 19650", "BIM Information Management", "2018", "International"),
                ("STD_ISO_ADD", "ISO Additional", "Extended ISO Coverage", "2023", "International"),
                // East Africa
                ("STD_EAS", "EAS", "East African Standards", "2023", "East Africa"),
                ("STD_KEBS", "KEBS", "Kenya Bureau of Standards", "2023", "Kenya"),
                ("STD_UNBS", "UNBS", "Uganda National Bureau of Standards", "2023", "Uganda"),
                ("STD_TBS", "TBS", "Tanzania Bureau of Standards", "2023", "Tanzania"),
                ("STD_RSB", "RSB", "Rwanda Standards Board", "2023", "Rwanda"),
                ("STD_SSBS", "SSBS", "South Sudan Bureau of Standards", "2023", "South Sudan"),
                // West/Southern Africa
                ("STD_ECOWAS", "ECOWAS", "West African Regional Standards", "2023", "West Africa"),
                ("STD_SANS", "SANS", "South African National Standards", "2023", "South Africa"),
                ("STD_CIDB", "CIDB", "Construction Industry Development Board", "2023", "South Africa"),
                // Building Regulations
                ("STD_BBN", "BBN", "Building Regulations", "2023", "International"),
            };

            foreach (var (id, code, title, version, region) in standards)
            {
                AddNode(new GraphNode
                {
                    NodeId = id,
                    NodeType = "Standard",
                    Properties = new Dictionary<string, object>
                    {
                        ["Code"] = code,
                        ["Title"] = title,
                        ["Version"] = version,
                        ["Region"] = region
                    }
                });
            }

            // RegulatedBy edges linking rooms and elements to applicable standards
            var regulations = new[]
            {
                // Room-level regulations
                ("REG_BATHROOM_ADA", "RT_BATHROOM", "STD_ADA", "603", "Accessible toilet rooms"),
                ("REG_BATHROOM_IPC", "RT_BATHROOM", "STD_IPC", "4", "Minimum fixture requirements"),
                ("REG_BATHROOM_ASHRAE", "RT_BATHROOM", "STD_ASHRAE_621", "6.2", "Exhaust ventilation for toilet rooms"),
                ("REG_STAIRS_IBC", "RT_STAIRS", "STD_IBC", "1011", "Stairway construction and dimensions"),
                ("REG_STAIRS_NFPA", "RT_STAIRS", "STD_NFPA", "7.2", "Means of egress — stairs"),
                ("REG_KITCHEN_ASHRAE", "RT_KITCHEN", "STD_ASHRAE_621", "6.2", "Minimum ventilation rates"),
                ("REG_KITCHEN_IMC", "RT_KITCHEN", "STD_IMC", "5", "Exhaust systems for commercial kitchens"),
                ("REG_KITCHEN_NFPA", "RT_KITCHEN", "STD_NFPA", "96", "Ventilation control for commercial cooking"),
                ("REG_CORRIDOR_IBC", "RT_CORRIDOR", "STD_IBC", "1020", "Corridor width and fire rating"),
                ("REG_CORRIDOR_ADA", "RT_CORRIDOR", "STD_ADA", "403", "Accessible walking surfaces"),
                ("REG_BEDROOM_IBC", "RT_BEDROOM", "STD_IBC", "1030", "Emergency escape and rescue openings"),
                ("REG_BEDROOM_ASHRAE", "RT_BEDROOM", "STD_ASHRAE_621", "6.2.2", "Bedroom ventilation requirements"),
                ("REG_LIVING_IBC", "RT_LIVING", "STD_IBC", "1204", "Minimum natural light and ventilation"),
                ("REG_ENTRANCE_ADA", "RT_ENTRANCE", "STD_ADA", "404", "Accessible doors and doorways"),
                ("REG_GARAGE_IMC", "RT_GARAGE", "STD_IMC", "4", "Garage ventilation"),
                ("REG_STORAGE_NFPA", "RT_STORAGE", "STD_NFPA", "13", "Fire sprinkler requirements"),
                // Element-level regulations
                ("REG_WALL_IBC", "EL_WALL", "STD_IBC", "602", "Fire-resistance rated construction"),
                ("REG_WALL_ASCE7", "EL_WALL", "STD_ASCE7", "12", "Seismic design requirements"),
                ("REG_FLOOR_IBC", "EL_FLOOR", "STD_IBC", "722", "Fire-resistance of floor assemblies"),
                ("REG_FLOOR_ASCE7", "EL_FLOOR", "STD_ASCE7", "4", "Live load requirements"),
                ("REG_ROOF_IBC", "EL_ROOF", "STD_IBC", "1507", "Roof covering requirements"),
                ("REG_ROOF_ASCE7", "EL_ROOF", "STD_ASCE7", "7", "Snow and rain loads"),
                ("REG_COLUMN_ACI", "EL_COLUMN", "STD_ACI318", "10", "Column design requirements"),
                ("REG_BEAM_ACI", "EL_BEAM", "STD_ACI318", "9", "Beam design requirements"),
                ("REG_DOOR_IBC", "EL_DOOR", "STD_IBC", "1010", "Door and gate requirements"),
                ("REG_DOOR_ADA", "EL_DOOR", "STD_ADA", "404.2", "Manual door requirements"),
                ("REG_WINDOW_ASHRAE", "EL_WINDOW", "STD_ASHRAE_901", "5", "Fenestration requirements"),
                ("REG_STAIR_IBC", "EL_STAIR", "STD_IBC", "1011", "Stair construction"),
                ("REG_DUCT_SMACNA", "EL_DUCT", "STD_SMACNA", "3", "Duct construction standards"),
                ("REG_PIPE_IPC", "EL_PIPE", "STD_IPC", "6", "Water supply and distribution"),
                ("REG_CONDUIT_NEC", "EL_CONDUIT", "STD_NEC", "344", "Rigid metal conduit"),
                // Africa-specific regulations
                ("REG_ROOM_EAS", "RT_BEDROOM", "STD_EAS", "1.1", "Habitable room standards"),
                ("REG_ROOM_KEBS", "RT_LIVING", "STD_KEBS", "2.1", "Residential building standards"),
                ("REG_ROOM_UNBS", "RT_KITCHEN", "STD_UNBS", "3.1", "Kitchen ventilation for tropical climate"),
            };

            foreach (var (edgeId, sourceId, targetId, section, requirement) in regulations)
            {
                AddEdge(new GraphEdge
                {
                    EdgeId = edgeId,
                    EdgeType = "RegulatedBy",
                    SourceNodeId = sourceId,
                    TargetNodeId = targetId,
                    Properties = new Dictionary<string, object>
                    {
                        ["Section"] = section,
                        ["Requirement"] = requirement
                    }
                });
            }

            Logger.Info($"Loaded {standards.Length} standards and {regulations.Length} regulation edges");
        }

        private void LoadMEPHierarchy()
        {
            // MEP category nodes
            var mepCategories = new[]
            {
                ("MEP_HVAC", "HVAC Systems", "Heating, ventilation, and air conditioning"),
                ("MEP_PLUMBING", "Plumbing Systems", "Water supply, drainage, and fixtures"),
                ("MEP_ELECTRICAL", "Electrical Systems", "Power distribution, lighting, and controls"),
                ("MEP_FIRE", "Fire Protection", "Sprinklers, alarms, and suppression"),
            };

            foreach (var (id, name, description) in mepCategories)
            {
                AddNode(new GraphNode
                {
                    NodeId = id,
                    NodeType = "Concept",
                    Properties = new Dictionary<string, object>
                    {
                        ["Name"] = name,
                        ["Description"] = description,
                        ["Domain"] = "MEP"
                    }
                });

                AddEdge(new GraphEdge
                {
                    EdgeId = $"ISA_{id}_MEP",
                    EdgeType = "IsA",
                    SourceNodeId = id,
                    TargetNodeId = "ELEM_MEP"
                });
            }

            // HVAC subsystems
            var hvacElements = new[]
            {
                ("MEP_AHU", "Air Handling Unit", "HVAC", "CentralAir"),
                ("MEP_VAV", "VAV Box", "HVAC", "ZoneControl"),
                ("MEP_CHILLER", "Chiller", "HVAC", "Cooling"),
                ("MEP_BOILER", "Boiler", "HVAC", "Heating"),
                ("MEP_FCU", "Fan Coil Unit", "HVAC", "RoomUnit"),
                ("MEP_EXHAUST", "Exhaust Fan", "HVAC", "Ventilation"),
                ("MEP_DIFFUSER", "Air Diffuser", "HVAC", "AirDistribution"),
            };

            // Plumbing subsystems
            var plumbingElements = new[]
            {
                ("MEP_TOILET", "Toilet", "Plumbing", "Fixture"),
                ("MEP_SINK", "Sink", "Plumbing", "Fixture"),
                ("MEP_SHOWER", "Shower", "Plumbing", "Fixture"),
                ("MEP_WATER_HEATER", "Water Heater", "Plumbing", "Equipment"),
                ("MEP_PUMP", "Water Pump", "Plumbing", "Equipment"),
                ("MEP_TANK", "Water Tank", "Plumbing", "Storage"),
                ("MEP_DRAIN", "Floor Drain", "Plumbing", "Drainage"),
            };

            // Electrical subsystems
            var electricalElements = new[]
            {
                ("MEP_PANEL", "Distribution Panel", "Electrical", "Distribution"),
                ("MEP_BREAKER", "Circuit Breaker", "Electrical", "Protection"),
                ("MEP_OUTLET", "Power Outlet", "Electrical", "Device"),
                ("MEP_SWITCH", "Light Switch", "Electrical", "Device"),
                ("MEP_LUMINAIRE", "Luminaire", "Electrical", "Lighting"),
                ("MEP_TRANSFORMER", "Transformer", "Electrical", "Distribution"),
                ("MEP_GENERATOR", "Backup Generator", "Electrical", "Emergency"),
            };

            // Fire protection subsystems
            var fireElements = new[]
            {
                ("MEP_SPRINKLER", "Sprinkler Head", "Fire", "Suppression"),
                ("MEP_SMOKE_DET", "Smoke Detector", "Fire", "Detection"),
                ("MEP_FIRE_ALARM", "Fire Alarm Panel", "Fire", "Alarm"),
                ("MEP_EXTINGUISHER", "Fire Extinguisher", "Fire", "Portable"),
            };

            var allMepElements = hvacElements.Select(e => (e.Item1, e.Item2, e.Item3, e.Item4, "MEP_HVAC"))
                .Concat(plumbingElements.Select(e => (e.Item1, e.Item2, e.Item3, e.Item4, "MEP_PLUMBING")))
                .Concat(electricalElements.Select(e => (e.Item1, e.Item2, e.Item3, e.Item4, "MEP_ELECTRICAL")))
                .Concat(fireElements.Select(e => (e.Item1, e.Item2, e.Item3, e.Item4, "MEP_FIRE")));

            foreach (var (id, name, system, subCategory, parentId) in allMepElements)
            {
                AddNode(new GraphNode
                {
                    NodeId = id,
                    NodeType = "Element",
                    Properties = new Dictionary<string, object>
                    {
                        ["Name"] = name,
                        ["Category"] = "MEP",
                        ["System"] = system,
                        ["SubCategory"] = subCategory
                    }
                });

                AddEdge(new GraphEdge
                {
                    EdgeId = $"ISA_{id}",
                    EdgeType = "IsA",
                    SourceNodeId = id,
                    TargetNodeId = parentId
                });
            }

            // MEP-Room relationships (which MEP elements are required in which rooms)
            var mepRoomRequirements = new[]
            {
                ("RT_BATHROOM", "MEP_TOILET", "Required"), ("RT_BATHROOM", "MEP_SINK", "Required"),
                ("RT_BATHROOM", "MEP_EXHAUST", "Required"), ("RT_BATHROOM", "MEP_SHOWER", "Common"),
                ("RT_KITCHEN", "MEP_SINK", "Required"), ("RT_KITCHEN", "MEP_EXHAUST", "Required"),
                ("RT_BEDROOM", "MEP_OUTLET", "Required"), ("RT_BEDROOM", "MEP_LUMINAIRE", "Required"),
                ("RT_BEDROOM", "MEP_SMOKE_DET", "Required"),
                ("RT_LIVING", "MEP_OUTLET", "Required"), ("RT_LIVING", "MEP_LUMINAIRE", "Required"),
                ("RT_CORRIDOR", "MEP_LUMINAIRE", "Required"), ("RT_CORRIDOR", "MEP_SMOKE_DET", "Required"),
                ("RT_GARAGE", "MEP_EXHAUST", "Required"), ("RT_GARAGE", "MEP_SMOKE_DET", "Required"),
            };

            foreach (var (roomId, mepId, necessity) in mepRoomRequirements)
            {
                AddEdge(new GraphEdge
                {
                    EdgeId = $"REQ_{roomId}_{mepId}",
                    EdgeType = "Requires",
                    SourceNodeId = roomId,
                    TargetNodeId = mepId,
                    Properties = new Dictionary<string, object>
                    {
                        ["Necessity"] = necessity
                    }
                });
            }

            Logger.Info($"Loaded {allMepElements.Count()} MEP elements with {mepRoomRequirements.Length} room requirements");
        }

        private void LoadAdditionalRoomTypes()
        {
            // Commercial and institutional room types
            var additionalRooms = new (string Id, string Name, string Category, double MinArea, double MaxArea,
                bool RequiresWindow, bool RequiresPlumbing, bool RequiresVentilation)[]
            {
                ("RT_CONFERENCE", "Conference Room", "Commercial", 15.0, 60.0, true, false, true),
                ("RT_LOBBY", "Lobby", "Circulation", 20.0, 200.0, true, false, true),
                ("RT_ELEVATOR_LOBBY", "Elevator Lobby", "Circulation", 6.0, 30.0, false, false, true),
                ("RT_SERVER_ROOM", "Server Room", "Technical", 10.0, 100.0, false, false, true),
                ("RT_ELECTRICAL_ROOM", "Electrical Room", "Technical", 6.0, 40.0, false, false, true),
                ("RT_MECHANICAL_ROOM", "Mechanical Room", "Technical", 10.0, 80.0, false, false, true),
                ("RT_RECEPTION", "Reception", "Commercial", 10.0, 50.0, true, false, true),
                ("RT_OPEN_OFFICE", "Open Office", "Commercial", 50.0, 500.0, true, false, true),
                ("RT_PRIVATE_OFFICE", "Private Office", "Commercial", 9.0, 25.0, true, false, true),
                ("RT_BREAK_ROOM", "Break Room", "Service", 10.0, 40.0, true, true, true),
                ("RT_RESTROOM", "Public Restroom", "Service", 5.0, 30.0, false, true, true),
                ("RT_LOADING_DOCK", "Loading Dock", "Service", 30.0, 200.0, false, false, true),
                ("RT_PARKING", "Parking Garage", "Circulation", 100.0, 5000.0, false, false, true),
                ("RT_STAIRWELL", "Stairwell", "Circulation", 4.0, 15.0, false, false, true),
                ("RT_ELEVATOR_SHAFT", "Elevator Shaft", "Circulation", 3.0, 8.0, false, false, true),
                ("RT_JANITOR", "Janitor Closet", "Service", 2.0, 6.0, false, true, true),
                ("RT_TELECOM", "Telecom Room", "Technical", 3.0, 15.0, false, false, true),
                ("RT_CLASSROOM", "Classroom", "Institutional", 40.0, 100.0, true, false, true),
                ("RT_LABORATORY", "Laboratory", "Institutional", 20.0, 80.0, true, true, true),
                ("RT_CAFETERIA", "Cafeteria", "Institutional", 50.0, 300.0, true, true, true),
                ("RT_AUDITORIUM", "Auditorium", "Institutional", 100.0, 1000.0, false, false, true),
                ("RT_LIBRARY", "Library", "Institutional", 40.0, 500.0, true, false, true),
            };

            // Map categories to parent concept IDs
            var categoryParents = new Dictionary<string, string>
            {
                ["Commercial"] = "CAT_LIVING",
                ["Service"] = "CAT_SERVICE",
                ["Circulation"] = "CAT_CIRCULATION",
                ["Technical"] = "CAT_SERVICE",
                ["Institutional"] = "CAT_LIVING",
            };

            foreach (var room in additionalRooms)
            {
                AddNode(new GraphNode
                {
                    NodeId = room.Id,
                    NodeType = "RoomType",
                    Properties = new Dictionary<string, object>
                    {
                        ["Name"] = room.Name,
                        ["Category"] = room.Category,
                        ["MinArea"] = room.MinArea,
                        ["MaxArea"] = room.MaxArea,
                        ["RequiresWindow"] = room.RequiresWindow,
                        ["RequiresPlumbing"] = room.RequiresPlumbing,
                        ["RequiresVentilation"] = room.RequiresVentilation
                    }
                });

                if (categoryParents.TryGetValue(room.Category, out var parentId))
                {
                    AddEdge(new GraphEdge
                    {
                        EdgeId = $"ISA_{room.Id}",
                        EdgeType = "IsA",
                        SourceNodeId = room.Id,
                        TargetNodeId = parentId
                    });
                }
            }

            Logger.Info($"Loaded {additionalRooms.Length} additional room types");
        }

        private void LoadMaterialHierarchy()
        {
            // Material category concepts
            var materialCategories = new[]
            {
                ("MAT_STRUCTURAL", "Structural Materials", "Load-bearing materials"),
                ("MAT_FINISH", "Finish Materials", "Surface finish and cladding"),
                ("MAT_INSULATION", "Insulation Materials", "Thermal and acoustic insulation"),
                ("MAT_ROOFING", "Roofing Materials", "Roof covering materials"),
                ("MAT_GLAZING", "Glazing Materials", "Glass and transparent materials"),
            };

            foreach (var (id, name, desc) in materialCategories)
            {
                AddNode(new GraphNode
                {
                    NodeId = id,
                    NodeType = "Concept",
                    Properties = new Dictionary<string, object>
                    {
                        ["Name"] = name,
                        ["Description"] = desc,
                        ["Domain"] = "Materials"
                    }
                });
            }

            // Expanded materials with thermal and cost properties
            var materials = new (string Id, string Name, string Category, string ParentId,
                double Density, double ThermalConductivity, double CostPerUnit, string FireRating)[]
            {
                // Structural
                ("MAT_CONCRETE_RC", "Reinforced Concrete", "Structural", "MAT_STRUCTURAL", 2400, 1.7, 120, "A1"),
                ("MAT_CONCRETE_PRECAST", "Precast Concrete", "Structural", "MAT_STRUCTURAL", 2300, 1.5, 150, "A1"),
                ("MAT_STEEL_STRUCTURAL", "Structural Steel", "Structural", "MAT_STRUCTURAL", 7850, 50, 250, "A1"),
                ("MAT_TIMBER_HARDWOOD", "Hardwood Timber", "Structural", "MAT_STRUCTURAL", 700, 0.16, 180, "D"),
                ("MAT_TIMBER_SOFTWOOD", "Softwood Timber", "Structural", "MAT_STRUCTURAL", 500, 0.13, 100, "D"),
                ("MAT_MASONRY_BRICK", "Clay Brick", "Structural", "MAT_STRUCTURAL", 1900, 0.77, 60, "A1"),
                ("MAT_MASONRY_BLOCK", "Concrete Block", "Structural", "MAT_STRUCTURAL", 1800, 1.13, 45, "A1"),
                ("MAT_STONE", "Natural Stone", "Structural", "MAT_STRUCTURAL", 2600, 1.5, 200, "A1"),
                // Finish
                ("MAT_PLASTER", "Plaster/Render", "Finish", "MAT_FINISH", 1300, 0.57, 25, "A1"),
                ("MAT_GYPSUM", "Gypsum Board", "Finish", "MAT_FINISH", 800, 0.16, 15, "A2"),
                ("MAT_CERAMIC_TILE", "Ceramic Tile", "Finish", "MAT_FINISH", 2300, 1.3, 40, "A1"),
                ("MAT_PAINT", "Paint", "Finish", "MAT_FINISH", 0, 0, 5, "Various"),
                ("MAT_VINYL", "Vinyl Flooring", "Finish", "MAT_FINISH", 1400, 0.17, 30, "Bfl-s1"),
                ("MAT_CARPET", "Carpet", "Finish", "MAT_FINISH", 200, 0.06, 35, "Cfl-s2"),
                ("MAT_ALUMINUM", "Aluminum", "Finish", "MAT_FINISH", 2700, 237, 300, "A1"),
                // Insulation
                ("MAT_MINERAL_WOOL", "Mineral Wool", "Insulation", "MAT_INSULATION", 30, 0.035, 20, "A1"),
                ("MAT_EPS", "Expanded Polystyrene", "Insulation", "MAT_INSULATION", 20, 0.038, 15, "E"),
                ("MAT_XPS", "Extruded Polystyrene", "Insulation", "MAT_INSULATION", 35, 0.034, 25, "E"),
                ("MAT_PIR", "PIR Board", "Insulation", "MAT_INSULATION", 30, 0.022, 35, "C"),
                // Roofing
                ("MAT_CLAY_TILES", "Clay Roof Tiles", "Roofing", "MAT_ROOFING", 1900, 0.77, 50, "A1"),
                ("MAT_METAL_ROOF", "Metal Roofing", "Roofing", "MAT_ROOFING", 7850, 50, 40, "A1"),
                ("MAT_BITUMEN", "Bituminous Membrane", "Roofing", "MAT_ROOFING", 1100, 0.17, 20, "E"),
                // Glazing
                ("MAT_FLOAT_GLASS", "Float Glass", "Glazing", "MAT_GLAZING", 2500, 1.0, 50, "A1"),
                ("MAT_TEMPERED_GLASS", "Tempered Glass", "Glazing", "MAT_GLAZING", 2500, 1.0, 80, "A1"),
                ("MAT_LAMINATED_GLASS", "Laminated Glass", "Glazing", "MAT_GLAZING", 2500, 1.0, 100, "A1"),
            };

            foreach (var mat in materials)
            {
                AddNode(new GraphNode
                {
                    NodeId = mat.Id,
                    NodeType = "Material",
                    Properties = new Dictionary<string, object>
                    {
                        ["Name"] = mat.Name,
                        ["Category"] = mat.Category,
                        ["Density"] = mat.Density,
                        ["ThermalConductivity"] = mat.ThermalConductivity,
                        ["CostPerUnit"] = mat.CostPerUnit,
                        ["FireRating"] = mat.FireRating
                    }
                });

                AddEdge(new GraphEdge
                {
                    EdgeId = $"ISA_{mat.Id}",
                    EdgeType = "IsA",
                    SourceNodeId = mat.Id,
                    TargetNodeId = mat.ParentId
                });
            }

            // Element-Material relationships (common material assignments)
            var elementMaterials = new[]
            {
                ("EL_WALL", "MAT_MASONRY_BRICK", "Primary", "0.215"),
                ("EL_WALL", "MAT_CONCRETE_RC", "Primary", "0.200"),
                ("EL_WALL", "MAT_GYPSUM", "Finish", "0.013"),
                ("EL_FLOOR", "MAT_CONCRETE_RC", "Structure", "0.150"),
                ("EL_FLOOR", "MAT_CERAMIC_TILE", "Finish", "0.010"),
                ("EL_COLUMN", "MAT_CONCRETE_RC", "Primary", ""),
                ("EL_COLUMN", "MAT_STEEL_STRUCTURAL", "Primary", ""),
                ("EL_BEAM", "MAT_CONCRETE_RC", "Primary", ""),
                ("EL_BEAM", "MAT_STEEL_STRUCTURAL", "Primary", ""),
                ("EL_ROOF", "MAT_CLAY_TILES", "Covering", ""),
                ("EL_ROOF", "MAT_METAL_ROOF", "Covering", ""),
            };

            foreach (var (elementId, materialId, layer, thickness) in elementMaterials)
            {
                AddEdge(new GraphEdge
                {
                    EdgeId = $"MADEOF_{elementId}_{materialId}_{layer}",
                    EdgeType = "MadeOf",
                    SourceNodeId = elementId,
                    TargetNodeId = materialId,
                    Properties = new Dictionary<string, object>
                    {
                        ["Layer"] = layer,
                        ["Thickness"] = thickness
                    }
                });
            }

            Logger.Info($"Loaded {materialCategories.Length} material categories, {materials.Length} materials, {elementMaterials.Length} material assignments");
        }

        private void LoadCausalKnowledge()
        {
            // High-level causal concepts derived from CAUSAL_RELATIONSHIPS.csv domains
            var causalConcepts = new[]
            {
                ("CAUSE_DAYLIGHTING", "Daylighting", "Natural light provision and its effects on occupant wellbeing"),
                ("CAUSE_ACOUSTICS", "Acoustic Performance", "Sound transmission, noise control, and acoustic comfort"),
                ("CAUSE_THERMAL", "Thermal Performance", "Heat transfer, insulation, and energy efficiency"),
                ("CAUSE_IAQ", "Indoor Air Quality", "Ventilation, pollutant control, and air freshness"),
                ("CAUSE_FIRE_SAFETY", "Fire Safety", "Fire prevention, detection, and means of egress"),
                ("CAUSE_CIRCULATION", "Circulation", "Movement paths, accessibility, and wayfinding"),
                ("CAUSE_STRUCTURAL", "Structural Integrity", "Load paths, stability, and structural adequacy"),
                ("CAUSE_ENERGY", "Energy Efficiency", "Energy consumption, passive design, and sustainability"),
                ("CAUSE_MOISTURE", "Moisture Control", "Waterproofing, condensation, and damp prevention"),
                ("CAUSE_DURABILITY", "Durability", "Material longevity, maintenance, and lifecycle cost"),
                ("CAUSE_MAINTENANCE", "Maintainability", "Access for maintenance, service life, and replacement"),
                ("CAUSE_COST", "Cost Efficiency", "Construction cost, operational cost, and value engineering"),
                ("CAUSE_ACCESSIBILITY", "Accessibility", "Universal design, disability access, and inclusive design"),
                ("CAUSE_SECURITY", "Security", "Physical security, access control, and surveillance"),
                ("CAUSE_COORDINATION", "Coordination", "Cross-discipline clash detection and coordination"),
            };

            foreach (var (id, name, description) in causalConcepts)
            {
                AddNode(new GraphNode
                {
                    NodeId = id,
                    NodeType = "Concept",
                    Properties = new Dictionary<string, object>
                    {
                        ["Name"] = name,
                        ["Description"] = description,
                        ["Domain"] = "Causal"
                    }
                });
            }

            // Key causal relationships between building concepts
            var causalRelations = new (string SourceId, string TargetId, string Cause, string Effect,
                double Strength, string Reversibility, string TimeToManifest)[]
            {
                // Daylighting chain
                ("EL_WINDOW", "CAUSE_DAYLIGHTING", "Window placement", "Daylight factor", 0.95, "Reversible", "Immediate"),
                ("CAUSE_DAYLIGHTING", "CAUSE_ENERGY", "Natural light", "Reduced artificial lighting energy", 0.80, "Reversible", "Seasonal"),
                // Acoustic chain
                ("EL_WALL", "CAUSE_ACOUSTICS", "Wall construction", "Sound transmission class", 0.90, "Reversible", "Immediate"),
                ("CAUSE_ACOUSTICS", "RT_BEDROOM", "Noise isolation", "Occupant sleep quality", 0.85, "Reversible", "Immediate"),
                // Thermal chain
                ("MAT_MINERAL_WOOL", "CAUSE_THERMAL", "Insulation R-value", "Thermal resistance", 0.95, "Reversible", "Immediate"),
                ("CAUSE_THERMAL", "CAUSE_ENERGY", "Building envelope performance", "Heating/cooling energy", 0.90, "Reversible", "Seasonal"),
                // IAQ chain
                ("MEP_EXHAUST", "CAUSE_IAQ", "Exhaust ventilation", "Pollutant removal", 0.90, "Reversible", "Immediate"),
                ("CAUSE_IAQ", "RT_KITCHEN", "Cooking pollutants", "Required ventilation rate", 0.85, "Reversible", "Immediate"),
                // Fire safety chain
                ("MEP_SPRINKLER", "CAUSE_FIRE_SAFETY", "Sprinkler coverage", "Fire suppression", 0.95, "Irreversible", "Immediate"),
                ("CAUSE_FIRE_SAFETY", "RT_CORRIDOR", "Egress requirements", "Required corridor width", 0.90, "Irreversible", "Immediate"),
                // Circulation
                ("RT_CORRIDOR", "CAUSE_CIRCULATION", "Corridor layout", "Movement efficiency", 0.85, "Reversible", "Immediate"),
                ("CAUSE_CIRCULATION", "CAUSE_FIRE_SAFETY", "Egress distance", "Emergency evacuation time", 0.90, "Irreversible", "Immediate"),
                // Structural
                ("EL_COLUMN", "CAUSE_STRUCTURAL", "Column spacing", "Structural grid", 0.95, "Irreversible", "Immediate"),
                ("EL_BEAM", "CAUSE_STRUCTURAL", "Beam depth", "Floor-to-floor height", 0.85, "Irreversible", "Immediate"),
                // Moisture
                ("MAT_BITUMEN", "CAUSE_MOISTURE", "Waterproofing membrane", "Moisture barrier", 0.90, "Degradable", "Years"),
                ("CAUSE_MOISTURE", "CAUSE_DURABILITY", "Moisture ingress", "Material degradation", 0.85, "Irreversible", "Years"),
                // Coordination
                ("MEP_HVAC", "CAUSE_COORDINATION", "Duct routing", "Ceiling void requirements", 0.80, "Reversible", "Immediate"),
                ("MEP_PLUMBING", "CAUSE_COORDINATION", "Pipe routing", "Wall cavity requirements", 0.75, "Reversible", "Immediate"),
                // Cost
                ("CAUSE_ENERGY", "CAUSE_COST", "Energy efficiency", "Operational cost reduction", 0.80, "Reversible", "Years"),
                ("CAUSE_DURABILITY", "CAUSE_COST", "Material longevity", "Lifecycle cost", 0.85, "Irreversible", "Years"),
            };

            foreach (var rel in causalRelations)
            {
                AddEdge(new GraphEdge
                {
                    EdgeId = $"CAUSAL_{rel.SourceId}_{rel.TargetId}",
                    EdgeType = "CausalRelation",
                    SourceNodeId = rel.SourceId,
                    TargetNodeId = rel.TargetId,
                    Strength = (float)rel.Strength,
                    Properties = new Dictionary<string, object>
                    {
                        ["Cause"] = rel.Cause,
                        ["Effect"] = rel.Effect,
                        ["Strength"] = rel.Strength,
                        ["Reversibility"] = rel.Reversibility,
                        ["TimeToManifest"] = rel.TimeToManifest
                    }
                });
            }

            Logger.Info($"Loaded {causalConcepts.Length} causal concepts and {causalRelations.Length} causal relationships");
        }

        private void RebuildIndices()
        {
            _indices.Clear();

            // Index by node type
            var byType = new NodeIndex { IndexName = "ByType" };
            foreach (var node in _nodes.Values)
            {
                if (!byType.Index.ContainsKey(node.NodeType))
                    byType.Index[node.NodeType] = new List<string>();
                byType.Index[node.NodeType].Add(node.NodeId);
            }
            _indices["ByType"] = byType;

            // Index by category (for room types)
            var byCategory = new NodeIndex { IndexName = "ByCategory" };
            foreach (var node in _nodes.Values.Where(n => n.NodeType == "RoomType"))
            {
                var category = node.Properties.GetValueOrDefault("Category", "Unknown").ToString();
                if (!byCategory.Index.ContainsKey(category))
                    byCategory.Index[category] = new List<string>();
                byCategory.Index[category].Add(node.NodeId);
            }
            _indices["ByCategory"] = byCategory;
        }

        #endregion

        #region Core Graph Operations

        public void AddNode(GraphNode node)
        {
            if (_nodes.ContainsKey(node.NodeId))
                throw new InvalidOperationException($"Node {node.NodeId} already exists");

            _nodes[node.NodeId] = node;
            _adjacencyList[node.NodeId] = new List<string>();
            _reverseAdjacencyList[node.NodeId] = new List<string>();
        }

        public void AddEdge(GraphEdge edge)
        {
            if (_edges.ContainsKey(edge.EdgeId))
                return; // Skip duplicates

            if (!_nodes.ContainsKey(edge.SourceNodeId))
                throw new InvalidOperationException($"Source node {edge.SourceNodeId} not found");
            if (!_nodes.ContainsKey(edge.TargetNodeId))
                throw new InvalidOperationException($"Target node {edge.TargetNodeId} not found");

            _edges[edge.EdgeId] = edge;
            _adjacencyList[edge.SourceNodeId].Add(edge.EdgeId);
            _reverseAdjacencyList[edge.TargetNodeId].Add(edge.EdgeId);
        }

        public GraphNode GetNode(string nodeId)
        {
            return _nodes.GetValueOrDefault(nodeId);
        }

        public GraphEdge GetEdge(string edgeId)
        {
            return _edges.GetValueOrDefault(edgeId);
        }

        public IEnumerable<GraphNode> GetNodesByType(string nodeType)
        {
            if (_indices.TryGetValue("ByType", out var index) &&
                index.Index.TryGetValue(nodeType, out var nodeIds))
            {
                return nodeIds.Select(id => _nodes[id]);
            }
            return _nodes.Values.Where(n => n.NodeType == nodeType);
        }

        public IEnumerable<GraphEdge> GetOutgoingEdges(string nodeId)
        {
            if (_adjacencyList.TryGetValue(nodeId, out var edgeIds))
            {
                return edgeIds.Select(id => _edges[id]);
            }
            return Enumerable.Empty<GraphEdge>();
        }

        public IEnumerable<GraphEdge> GetIncomingEdges(string nodeId)
        {
            if (_reverseAdjacencyList.TryGetValue(nodeId, out var edgeIds))
            {
                return edgeIds.Select(id => _edges[id]);
            }
            return Enumerable.Empty<GraphEdge>();
        }

        public IEnumerable<GraphNode> GetNeighbors(string nodeId, string edgeType = null)
        {
            var outgoing = GetOutgoingEdges(nodeId);
            if (!string.IsNullOrEmpty(edgeType))
                outgoing = outgoing.Where(e => e.EdgeType == edgeType);

            return outgoing.Select(e => _nodes[e.TargetNodeId]);
        }

        #endregion

        #region Spatial Relationship Queries

        /// <summary>
        /// Get spatial relationship between two room types.
        /// </summary>
        public SpatialRelationship GetSpatialRelationship(string roomType1, string roomType2)
        {
            // Check direct relationship
            var directEdge = GetOutgoingEdges(roomType1)
                .FirstOrDefault(e => e.EdgeType == "SpatialRelation" && e.TargetNodeId == roomType2);

            if (directEdge != null)
            {
                return new SpatialRelationship
                {
                    RoomType1 = roomType1,
                    RoomType2 = roomType2,
                    RelationType = directEdge.Properties.GetValueOrDefault("RelationType", "Unknown").ToString(),
                    Strength = Convert.ToDouble(directEdge.Properties.GetValueOrDefault("Strength", 0.5)),
                    MinDistance = Convert.ToDouble(directEdge.Properties.GetValueOrDefault("MinDistance", 0)),
                    MaxDistance = Convert.ToDouble(directEdge.Properties.GetValueOrDefault("MaxDistance", 999)),
                    RequiresDirectAccess = Convert.ToBoolean(directEdge.Properties.GetValueOrDefault("RequiresAccess", false))
                };
            }

            // Check reverse relationship
            var reverseEdge = GetOutgoingEdges(roomType2)
                .FirstOrDefault(e => e.EdgeType == "SpatialRelation" && e.TargetNodeId == roomType1);

            if (reverseEdge != null)
            {
                return new SpatialRelationship
                {
                    RoomType1 = roomType1,
                    RoomType2 = roomType2,
                    RelationType = reverseEdge.Properties.GetValueOrDefault("RelationType", "Unknown").ToString(),
                    Strength = Convert.ToDouble(reverseEdge.Properties.GetValueOrDefault("Strength", 0.5)) * 0.9, // Slightly lower for reverse
                    MinDistance = Convert.ToDouble(reverseEdge.Properties.GetValueOrDefault("MinDistance", 0)),
                    MaxDistance = Convert.ToDouble(reverseEdge.Properties.GetValueOrDefault("MaxDistance", 999)),
                    RequiresDirectAccess = Convert.ToBoolean(reverseEdge.Properties.GetValueOrDefault("RequiresAccess", false))
                };
            }

            // No explicit relationship - return neutral
            return new SpatialRelationship
            {
                RoomType1 = roomType1,
                RoomType2 = roomType2,
                RelationType = "Neutral",
                Strength = 0.5,
                MinDistance = 0,
                MaxDistance = 999,
                RequiresDirectAccess = false
            };
        }

        /// <summary>
        /// Get all rooms that should be adjacent to a given room type.
        /// </summary>
        public List<AdjacentRoomSuggestion> GetAdjacentRoomSuggestions(string roomType)
        {
            var suggestions = new List<AdjacentRoomSuggestion>();

            var edges = GetOutgoingEdges(roomType)
                .Where(e => e.EdgeType == "SpatialRelation")
                .ToList();

            foreach (var edge in edges)
            {
                var relType = edge.Properties.GetValueOrDefault("RelationType", "").ToString();
                if (relType == "Adjacent" || relType == "Near")
                {
                    var targetNode = GetNode(edge.TargetNodeId);
                    suggestions.Add(new AdjacentRoomSuggestion
                    {
                        RoomTypeId = edge.TargetNodeId,
                        RoomTypeName = targetNode?.Properties.GetValueOrDefault("Name", "").ToString(),
                        RelationType = relType,
                        Priority = Convert.ToDouble(edge.Properties.GetValueOrDefault("Strength", 0.5)),
                        Reason = $"{relType} relationship with strength {edge.Properties.GetValueOrDefault("Strength", 0.5)}"
                    });
                }
            }

            // Also check incoming edges
            var incomingEdges = GetIncomingEdges(roomType)
                .Where(e => e.EdgeType == "SpatialRelation")
                .ToList();

            foreach (var edge in incomingEdges)
            {
                var relType = edge.Properties.GetValueOrDefault("RelationType", "").ToString();
                if (relType == "Adjacent" || relType == "Near")
                {
                    var sourceNode = GetNode(edge.SourceNodeId);
                    if (!suggestions.Any(s => s.RoomTypeId == edge.SourceNodeId))
                    {
                        suggestions.Add(new AdjacentRoomSuggestion
                        {
                            RoomTypeId = edge.SourceNodeId,
                            RoomTypeName = sourceNode?.Properties.GetValueOrDefault("Name", "").ToString(),
                            RelationType = relType,
                            Priority = Convert.ToDouble(edge.Properties.GetValueOrDefault("Strength", 0.5)) * 0.9,
                            Reason = $"Inverse {relType} relationship"
                        });
                    }
                }
            }

            return suggestions.OrderByDescending(s => s.Priority).ToList();
        }

        /// <summary>
        /// Get rooms that should be avoided near a given room type.
        /// </summary>
        public List<string> GetAvoidedRooms(string roomType)
        {
            var avoided = new List<string>();

            var edges = GetOutgoingEdges(roomType)
                .Where(e => e.EdgeType == "SpatialRelation")
                .ToList();

            foreach (var edge in edges)
            {
                var relType = edge.Properties.GetValueOrDefault("RelationType", "").ToString();
                if (relType == "Avoid" || relType == "Separated")
                {
                    avoided.Add(edge.TargetNodeId);
                }
            }

            return avoided;
        }

        #endregion

        #region Path Finding

        /// <summary>
        /// Find shortest path between two nodes.
        /// </summary>
        public GraphPath FindShortestPath(string startNodeId, string endNodeId, string edgeType = null)
        {
            if (!_nodes.ContainsKey(startNodeId) || !_nodes.ContainsKey(endNodeId))
                return null;

            var visited = new HashSet<string>();
            var queue = new Queue<List<string>>();
            queue.Enqueue(new List<string> { startNodeId });

            while (queue.Count > 0)
            {
                var path = queue.Dequeue();
                var currentNode = path.Last();

                if (currentNode == endNodeId)
                {
                    return new GraphPath
                    {
                        StartNodeId = startNodeId,
                        EndNodeId = endNodeId,
                        NodeIds = path,
                        Length = path.Count - 1
                    };
                }

                if (visited.Contains(currentNode))
                    continue;

                visited.Add(currentNode);

                var edges = GetOutgoingEdges(currentNode);
                if (!string.IsNullOrEmpty(edgeType))
                    edges = edges.Where(e => e.EdgeType == edgeType);

                foreach (var edge in edges)
                {
                    if (!visited.Contains(edge.TargetNodeId))
                    {
                        var newPath = new List<string>(path) { edge.TargetNodeId };
                        queue.Enqueue(newPath);
                    }
                }
            }

            return null; // No path found
        }

        /// <summary>
        /// Find all paths between two nodes up to a maximum length.
        /// </summary>
        public List<GraphPath> FindAllPaths(string startNodeId, string endNodeId, int maxLength = 5, string edgeType = null)
        {
            var allPaths = new List<GraphPath>();
            var currentPath = new List<string> { startNodeId };
            var visited = new HashSet<string> { startNodeId };

            FindPathsDFS(startNodeId, endNodeId, currentPath, visited, allPaths, maxLength, edgeType);

            return allPaths;
        }

        private void FindPathsDFS(
            string current,
            string target,
            List<string> currentPath,
            HashSet<string> visited,
            List<GraphPath> allPaths,
            int maxLength,
            string edgeType)
        {
            if (currentPath.Count > maxLength + 1)
                return;

            if (current == target && currentPath.Count > 1)
            {
                allPaths.Add(new GraphPath
                {
                    StartNodeId = currentPath.First(),
                    EndNodeId = target,
                    NodeIds = new List<string>(currentPath),
                    Length = currentPath.Count - 1
                });
                return;
            }

            var edges = GetOutgoingEdges(current);
            if (!string.IsNullOrEmpty(edgeType))
                edges = edges.Where(e => e.EdgeType == edgeType);

            foreach (var edge in edges)
            {
                if (!visited.Contains(edge.TargetNodeId))
                {
                    visited.Add(edge.TargetNodeId);
                    currentPath.Add(edge.TargetNodeId);

                    FindPathsDFS(edge.TargetNodeId, target, currentPath, visited, allPaths, maxLength, edgeType);

                    currentPath.RemoveAt(currentPath.Count - 1);
                    visited.Remove(edge.TargetNodeId);
                }
            }
        }

        #endregion

        #region Constraint Propagation

        /// <summary>
        /// Propagate constraints from a source node.
        /// When enforce is true, derived constraints are applied to node properties.
        /// </summary>
        public ConstraintPropagationResult PropagateConstraints(
            string sourceNodeId, Constraint constraint, bool enforce = false)
        {
            var result = new ConstraintPropagationResult
            {
                SourceNodeId = sourceNodeId,
                OriginalConstraint = constraint,
                AffectedNodes = new List<AffectedNode>()
            };

            var visited = new HashSet<string>();
            var queue = new Queue<(string NodeId, Constraint Constraint, int Depth)>();
            queue.Enqueue((sourceNodeId, constraint, 0));

            while (queue.Count > 0)
            {
                var (currentId, currentConstraint, depth) = queue.Dequeue();

                if (visited.Contains(currentId) || depth > constraint.MaxPropagationDepth)
                    continue;

                visited.Add(currentId);

                // Apply constraint to current node
                var node = GetNode(currentId);
                if (node != null && depth > 0)
                {
                    var derivedConstraint = DeriveConstraint(currentConstraint, depth);
                    var affectedNode = new AffectedNode
                    {
                        NodeId = currentId,
                        DerivedConstraint = derivedConstraint,
                        Depth = depth
                    };

                    // Enforce: apply the constraint to the node's properties
                    if (enforce)
                    {
                        affectedNode.Enforced = EnforceConstraint(node, derivedConstraint);
                    }

                    result.AffectedNodes.Add(affectedNode);
                }

                // Propagate to connected nodes
                var edges = GetOutgoingEdges(currentId)
                    .Where(e => constraint.PropagationEdgeTypes.Contains(e.EdgeType));

                foreach (var edge in edges)
                {
                    var strength = Convert.ToDouble(edge.Properties.GetValueOrDefault("Strength", 0.5));
                    if (strength >= constraint.MinStrengthForPropagation)
                    {
                        queue.Enqueue((edge.TargetNodeId, currentConstraint, depth + 1));
                    }
                }
            }

            result.EnforcedCount = result.AffectedNodes.Count(a => a.Enforced);
            return result;
        }

        /// <summary>
        /// Enforces a single constraint on a node by updating its properties.
        /// Returns true if the constraint was applied (value changed or set).
        /// </summary>
        private bool EnforceConstraint(GraphNode node, Constraint constraint)
        {
            if (string.IsNullOrEmpty(constraint.Property) || constraint.Value == null)
                return false;

            var propKey = $"constraint:{constraint.Property}";

            switch (constraint.ConstraintType)
            {
                case "Minimum":
                    // Enforce minimum value: only apply if current value is below
                    if (node.Properties.TryGetValue(constraint.Property, out var currentMin) &&
                        currentMin is IConvertible)
                    {
                        var currentVal = Convert.ToDouble(currentMin);
                        var requiredVal = Convert.ToDouble(constraint.Value);
                        if (currentVal < requiredVal)
                        {
                            node.Properties[constraint.Property] = requiredVal;
                            node.Properties[propKey] = $"Enforced minimum {requiredVal} (strength: {constraint.Strength:F2})";
                            return true;
                        }
                        return false;
                    }
                    break;

                case "Maximum":
                    // Enforce maximum value: only apply if current value is above
                    if (node.Properties.TryGetValue(constraint.Property, out var currentMax) &&
                        currentMax is IConvertible)
                    {
                        var currentVal = Convert.ToDouble(currentMax);
                        var requiredVal = Convert.ToDouble(constraint.Value);
                        if (currentVal > requiredVal)
                        {
                            node.Properties[constraint.Property] = requiredVal;
                            node.Properties[propKey] = $"Enforced maximum {requiredVal} (strength: {constraint.Strength:F2})";
                            return true;
                        }
                        return false;
                    }
                    break;

                case "Required":
                    // Enforce required value: set if not present
                    if (!node.Properties.ContainsKey(constraint.Property))
                    {
                        node.Properties[constraint.Property] = constraint.Value;
                        node.Properties[propKey] = $"Enforced required value (strength: {constraint.Strength:F2})";
                        return true;
                    }
                    return false;

                default:
                    // Generic enforcement: set value with constraint metadata
                    node.Properties[constraint.Property] = constraint.Value;
                    node.Properties[propKey] = $"Enforced {constraint.ConstraintType} (strength: {constraint.Strength:F2})";
                    return true;
            }

            // Property not yet set - initialize it with the constraint value
            node.Properties[constraint.Property] = constraint.Value;
            node.Properties[propKey] = $"Initialized by constraint (strength: {constraint.Strength:F2})";
            return true;
        }

        private Constraint DeriveConstraint(Constraint original, int depth)
        {
            // Reduce constraint strength based on depth
            var strengthFactor = Math.Pow(0.8, depth);

            return new Constraint
            {
                ConstraintType = original.ConstraintType,
                Property = original.Property,
                Value = original.Value,
                Strength = original.Strength * strengthFactor,
                MaxPropagationDepth = 0, // Derived constraints don't propagate further
                PropagationEdgeTypes = original.PropagationEdgeTypes,
                MinStrengthForPropagation = original.MinStrengthForPropagation
            };
        }

        #endregion

        #region Conflict Detection

        /// <summary>
        /// Detect conflicts in a proposed layout.
        /// </summary>
        public ConflictDetectionResult DetectConflicts(ProposedLayout layout)
        {
            var result = new ConflictDetectionResult
            {
                Layout = layout,
                Conflicts = new List<LayoutConflict>()
            };

            // Check spatial relationship conflicts
            foreach (var room1 in layout.Rooms)
            {
                foreach (var room2 in layout.Rooms.Where(r => r.RoomId != room1.RoomId))
                {
                    var relationship = GetSpatialRelationship(room1.RoomTypeId, room2.RoomTypeId);
                    var actualDistance = CalculateDistance(room1.Center, room2.Center);

                    // Check if rooms that should be avoided are too close
                    if (relationship.RelationType == "Avoid" && actualDistance < relationship.MinDistance)
                    {
                        result.Conflicts.Add(new LayoutConflict
                        {
                            ConflictType = ConflictType.SpatialViolation,
                            Severity = ConflictSeverity.Error,
                            Description = $"{room1.Name} and {room2.Name} should be separated (min {relationship.MinDistance}m) but are {actualDistance:F1}m apart",
                            InvolvedRooms = new[] { room1.RoomId, room2.RoomId },
                            Suggestion = $"Increase distance between {room1.Name} and {room2.Name}"
                        });
                    }

                    // Check if adjacent rooms are too far
                    if (relationship.RelationType == "Adjacent" && actualDistance > relationship.MaxDistance)
                    {
                        result.Conflicts.Add(new LayoutConflict
                        {
                            ConflictType = ConflictType.AdjacencyViolation,
                            Severity = ConflictSeverity.Warning,
                            Description = $"{room1.Name} and {room2.Name} should be adjacent (max {relationship.MaxDistance}m) but are {actualDistance:F1}m apart",
                            InvolvedRooms = new[] { room1.RoomId, room2.RoomId },
                            Suggestion = $"Move {room1.Name} closer to {room2.Name}"
                        });
                    }

                    // Check direct access requirements
                    if (relationship.RequiresDirectAccess && !HasDirectAccess(room1, room2, layout))
                    {
                        result.Conflicts.Add(new LayoutConflict
                        {
                            ConflictType = ConflictType.AccessViolation,
                            Severity = ConflictSeverity.Error,
                            Description = $"{room1.Name} requires direct access to {room2.Name}",
                            InvolvedRooms = new[] { room1.RoomId, room2.RoomId },
                            Suggestion = $"Add door or opening between {room1.Name} and {room2.Name}"
                        });
                    }
                }
            }

            // Check room requirements
            foreach (var room in layout.Rooms)
            {
                var roomNode = GetNode(room.RoomTypeId);
                if (roomNode == null) continue;

                // Check area
                var minArea = Convert.ToDouble(roomNode.Properties.GetValueOrDefault("MinArea", 0));
                var maxArea = Convert.ToDouble(roomNode.Properties.GetValueOrDefault("MaxArea", 999));

                if (room.Area < minArea)
                {
                    result.Conflicts.Add(new LayoutConflict
                    {
                        ConflictType = ConflictType.AreaViolation,
                        Severity = ConflictSeverity.Error,
                        Description = $"{room.Name} area ({room.Area:F1}m²) is below minimum ({minArea}m²)",
                        InvolvedRooms = new[] { room.RoomId },
                        Suggestion = $"Increase {room.Name} area to at least {minArea}m²"
                    });
                }

                if (room.Area > maxArea)
                {
                    result.Conflicts.Add(new LayoutConflict
                    {
                        ConflictType = ConflictType.AreaViolation,
                        Severity = ConflictSeverity.Warning,
                        Description = $"{room.Name} area ({room.Area:F1}m²) exceeds typical maximum ({maxArea}m²)",
                        InvolvedRooms = new[] { room.RoomId },
                        Suggestion = $"Consider subdividing {room.Name}"
                    });
                }

                // Check window requirement
                var needsWindow = Convert.ToBoolean(roomNode.Properties.GetValueOrDefault("RequiresWindow", false));
                if (needsWindow && !room.HasExternalWall)
                {
                    result.Conflicts.Add(new LayoutConflict
                    {
                        ConflictType = ConflictType.RequirementViolation,
                        Severity = ConflictSeverity.Error,
                        Description = $"{room.Name} requires natural light but has no external wall",
                        InvolvedRooms = new[] { room.RoomId },
                        Suggestion = $"Relocate {room.Name} to have external wall access"
                    });
                }
            }

            result.HasCriticalConflicts = result.Conflicts.Any(c => c.Severity == ConflictSeverity.Error);

            return result;
        }

        private double CalculateDistance(Point3D p1, Point3D p2)
        {
            var dx = p2.X - p1.X;
            var dy = p2.Y - p1.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private bool HasDirectAccess(ProposedRoom room1, ProposedRoom room2, ProposedLayout layout)
        {
            // Check if there's a door between the rooms
            return layout.Doors?.Any(d =>
                (d.FromRoomId == room1.RoomId && d.ToRoomId == room2.RoomId) ||
                (d.FromRoomId == room2.RoomId && d.ToRoomId == room1.RoomId)) ?? false;
        }

        #endregion

        #region Query Interface

        /// <summary>
        /// Execute a graph query.
        /// </summary>
        public QueryResult ExecuteQuery(GraphQuery query)
        {
            var result = new QueryResult
            {
                Query = query,
                Results = new List<Dictionary<string, object>>()
            };

            IEnumerable<GraphNode> candidates = _nodes.Values;

            // Filter by node type
            if (!string.IsNullOrEmpty(query.NodeType))
            {
                candidates = candidates.Where(n => n.NodeType == query.NodeType);
            }

            // Filter by properties
            if (query.PropertyFilters != null)
            {
                foreach (var filter in query.PropertyFilters)
                {
                    candidates = candidates.Where(n =>
                        n.Properties.ContainsKey(filter.Key) &&
                        MatchesFilter(n.Properties[filter.Key], filter.Value));
                }
            }

            // Apply edge filter (nodes must have specific edge)
            if (!string.IsNullOrEmpty(query.RequiredEdgeType))
            {
                candidates = candidates.Where(n =>
                    GetOutgoingEdges(n.NodeId).Any(e => e.EdgeType == query.RequiredEdgeType));
            }

            // Convert to results
            foreach (var node in candidates.Take(query.MaxResults))
            {
                var row = new Dictionary<string, object>
                {
                    ["NodeId"] = node.NodeId,
                    ["NodeType"] = node.NodeType
                };

                foreach (var prop in node.Properties)
                {
                    row[prop.Key] = prop.Value;
                }

                result.Results.Add(row);
            }

            result.TotalCount = result.Results.Count;

            return result;
        }

        private bool MatchesFilter(object value, object filter)
        {
            if (filter is string filterStr && filterStr.StartsWith(">"))
            {
                if (double.TryParse(filterStr.Substring(1), System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var threshold))
                {
                    try { return Convert.ToDouble(value) > threshold; }
                    catch { return false; }
                }
                return false;
            }

            if (filter is string filterStr2 && filterStr2.StartsWith("<"))
            {
                if (double.TryParse(filterStr2.Substring(1), System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var threshold))
                {
                    try { return Convert.ToDouble(value) < threshold; }
                    catch { return false; }
                }
                return false;
            }

            return value?.ToString() == filter?.ToString();
        }

        #endregion
    }

    #region Supporting Types

    public class GraphSchema
    {
        public List<NodeTypeDefinition> NodeTypes { get; set; } = new();
        public List<EdgeTypeDefinition> EdgeTypes { get; set; } = new();
    }

    public class NodeTypeDefinition
    {
        public string TypeId { get; set; }
        public string[] Properties { get; set; }
        public string[] RequiredProperties { get; set; }
    }

    public class EdgeTypeDefinition
    {
        public string TypeId { get; set; }
        public string[] Properties { get; set; }
        public string[] AllowedSourceTypes { get; set; }
        public string[] AllowedTargetTypes { get; set; }
    }

    public class GraphNode
    {
        public string NodeId { get; set; }
        public string Id { get => NodeId; set => NodeId = value; }
        public string NodeType { get; set; }
        public Dictionary<string, object> Properties { get; set; } = new();
    }

    public class GraphEdge
    {
        public string EdgeId { get; set; }
        public string EdgeType { get; set; }
        public string SourceNodeId { get; set; }
        public string TargetNodeId { get; set; }
        public string SourceId { get => SourceNodeId; set => SourceNodeId = value; }
        public string TargetId { get => TargetNodeId; set => TargetNodeId = value; }
        public float Strength { get; set; }
        public Dictionary<string, object> Properties { get; set; } = new();
    }

    public class NodeIndex
    {
        public string IndexName { get; set; }
        public Dictionary<string, List<string>> Index { get; set; } = new();
    }

    public class SpatialRelationship
    {
        public string RoomType1 { get; set; }
        public string RoomType2 { get; set; }
        public string RelationType { get; set; }
        public double Strength { get; set; }
        public double MinDistance { get; set; }
        public double MaxDistance { get; set; }
        public bool RequiresDirectAccess { get; set; }
    }

    public class AdjacentRoomSuggestion
    {
        public string RoomTypeId { get; set; }
        public string RoomTypeName { get; set; }
        public string RelationType { get; set; }
        public double Priority { get; set; }
        public string Reason { get; set; }
    }

    public class GraphPath
    {
        public string StartNodeId { get; set; }
        public string EndNodeId { get; set; }
        public List<string> NodeIds { get; set; }
        public int Length { get; set; }
    }

    public class Constraint
    {
        public string ConstraintType { get; set; }
        public string Property { get; set; }
        public object Value { get; set; }
        public double Strength { get; set; } = 1.0;
        public int MaxPropagationDepth { get; set; } = 3;
        public string[] PropagationEdgeTypes { get; set; } = new[] { "SpatialRelation", "Requires" };
        public double MinStrengthForPropagation { get; set; } = 0.5;
    }

    public class ConstraintPropagationResult
    {
        public string SourceNodeId { get; set; }
        public Constraint OriginalConstraint { get; set; }
        public List<AffectedNode> AffectedNodes { get; set; }
        public int EnforcedCount { get; set; }
    }

    public class AffectedNode
    {
        public string NodeId { get; set; }
        public Constraint DerivedConstraint { get; set; }
        public int Depth { get; set; }
        public bool Enforced { get; set; }
    }

    public class ProposedLayout
    {
        public string LayoutId { get; set; }
        public List<ProposedRoom> Rooms { get; set; }
        public List<ProposedDoor> Doors { get; set; }
    }

    public class ProposedRoom
    {
        public string RoomId { get; set; }
        public string RoomTypeId { get; set; }
        public string Name { get; set; }
        public double Area { get; set; }
        public Point3D Center { get; set; }
        public bool HasExternalWall { get; set; }
    }

    public class ProposedDoor
    {
        public string DoorId { get; set; }
        public string FromRoomId { get; set; }
        public string ToRoomId { get; set; }
    }

    public class ConflictDetectionResult
    {
        public ProposedLayout Layout { get; set; }
        public List<LayoutConflict> Conflicts { get; set; }
        public bool HasCriticalConflicts { get; set; }
    }

    public class LayoutConflict
    {
        public ConflictType ConflictType { get; set; }
        public ConflictSeverity Severity { get; set; }
        public string Description { get; set; }
        public string[] InvolvedRooms { get; set; }
        public string Suggestion { get; set; }
    }

    public class GraphQuery
    {
        public string NodeType { get; set; }
        public Dictionary<string, object> PropertyFilters { get; set; }
        public string RequiredEdgeType { get; set; }
        public int MaxResults { get; set; } = 100;
    }

    public class QueryResult
    {
        public GraphQuery Query { get; set; }
        public List<Dictionary<string, object>> Results { get; set; }
        public int TotalCount { get; set; }
    }

    public enum ConflictType
    {
        SpatialViolation,
        AdjacencyViolation,
        AccessViolation,
        AreaViolation,
        RequirementViolation,
        CodeViolation
    }

    public enum ConflictSeverity
    {
        Info,
        Warning,
        Error,
        Critical
    }

    #endregion
}
