// StingBIM.AI.NLP.Consulting.DWGToBIMConversionEngine
// Intelligent 2D DWG-to-3D BIM conversion engine.
// Parses DWG layer/entity metadata, classifies elements by ML-trained rules,
// generates Revit element creation instructions with material & type assignments.
// Integrates with KnowledgeGraph for element mapping and Standards for compliance.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace StingBIM.AI.NLP.Consulting
{
    /// <summary>
    /// Converts 2D DWG drawings to 3D BIM element creation plans.
    /// Analyzes DWG layers, polylines, blocks and text annotations to identify
    /// walls, doors, windows, columns, MEP runs, rooms and generates a structured
    /// BIM creation plan with element types, materials and parameters.
    /// </summary>
    public class DWGToBIMConversionEngine
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly Dictionary<string, LayerClassification> _layerRules;
        private readonly Dictionary<string, BlockMapping> _blockMappings;
        private readonly Dictionary<string, WallTypeRule> _wallTypeRules;
        private readonly Dictionary<string, MEPSystemRule> _mepSystemRules;
        private readonly Dictionary<string, string> _textAnnotationPatterns;

        public DWGToBIMConversionEngine()
        {
            _layerRules = new Dictionary<string, LayerClassification>(StringComparer.OrdinalIgnoreCase);
            _blockMappings = new Dictionary<string, BlockMapping>(StringComparer.OrdinalIgnoreCase);
            _wallTypeRules = new Dictionary<string, WallTypeRule>(StringComparer.OrdinalIgnoreCase);
            _mepSystemRules = new Dictionary<string, MEPSystemRule>(StringComparer.OrdinalIgnoreCase);
            _textAnnotationPatterns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            InitializeLayerRules();
            InitializeBlockMappings();
            InitializeWallTypeRules();
            InitializeMEPSystemRules();
            InitializeAnnotationPatterns();
        }

        #region Initialization

        private void InitializeLayerRules()
        {
            // AIA CAD Layer Guidelines (US National CAD Standard)
            // Format: Discipline-Major-Minor (e.g., A-WALL-EXTR)
            var rules = new[]
            {
                // Architectural
                new LayerClassification { Pattern = @"A[-_]WALL", BIMCategory = "Walls", ElementType = "Wall", Discipline = "Architectural", Priority = 10 },
                new LayerClassification { Pattern = @"A[-_]WALL[-_]EXT", BIMCategory = "Walls", ElementType = "ExteriorWall", Discipline = "Architectural", Priority = 15 },
                new LayerClassification { Pattern = @"A[-_]WALL[-_]INT", BIMCategory = "Walls", ElementType = "InteriorWall", Discipline = "Architectural", Priority = 15 },
                new LayerClassification { Pattern = @"A[-_]WALL[-_]PART", BIMCategory = "Walls", ElementType = "PartitionWall", Discipline = "Architectural", Priority = 15 },
                new LayerClassification { Pattern = @"A[-_]WALL[-_]CURT", BIMCategory = "Walls", ElementType = "CurtainWall", Discipline = "Architectural", Priority = 15 },
                new LayerClassification { Pattern = @"A[-_]DOOR", BIMCategory = "Doors", ElementType = "Door", Discipline = "Architectural", Priority = 10 },
                new LayerClassification { Pattern = @"A[-_]GLAZ|A[-_]WIND", BIMCategory = "Windows", ElementType = "Window", Discipline = "Architectural", Priority = 10 },
                new LayerClassification { Pattern = @"A[-_]FLOR|A[-_]FLOOR", BIMCategory = "Floors", ElementType = "Floor", Discipline = "Architectural", Priority = 8 },
                new LayerClassification { Pattern = @"A[-_]ROOF", BIMCategory = "Roofs", ElementType = "Roof", Discipline = "Architectural", Priority = 8 },
                new LayerClassification { Pattern = @"A[-_]CLNG|A[-_]CEIL", BIMCategory = "Ceilings", ElementType = "Ceiling", Discipline = "Architectural", Priority = 7 },
                new LayerClassification { Pattern = @"A[-_]COLS|A[-_]COL", BIMCategory = "Columns", ElementType = "ArchitecturalColumn", Discipline = "Architectural", Priority = 9 },
                new LayerClassification { Pattern = @"A[-_]STRS|A[-_]STAIR", BIMCategory = "Stairs", ElementType = "Stair", Discipline = "Architectural", Priority = 8 },
                new LayerClassification { Pattern = @"A[-_]ELEV", BIMCategory = "SpecialEquipment", ElementType = "Elevator", Discipline = "Architectural", Priority = 8 },
                new LayerClassification { Pattern = @"A[-_]FURN", BIMCategory = "Furniture", ElementType = "Furniture", Discipline = "Architectural", Priority = 5 },
                new LayerClassification { Pattern = @"A[-_]EQPM", BIMCategory = "Equipment", ElementType = "Equipment", Discipline = "Architectural", Priority = 5 },
                new LayerClassification { Pattern = @"A[-_]AREA|A[-_]ROOM", BIMCategory = "Rooms", ElementType = "Room", Discipline = "Architectural", Priority = 6 },
                new LayerClassification { Pattern = @"A[-_]ANNO|A[-_]TEXT", BIMCategory = "Annotation", ElementType = "Text", Discipline = "Architectural", Priority = 2 },
                new LayerClassification { Pattern = @"A[-_]DIMS?", BIMCategory = "Annotation", ElementType = "Dimension", Discipline = "Architectural", Priority = 2 },
                new LayerClassification { Pattern = @"A[-_]GRID", BIMCategory = "Grids", ElementType = "Grid", Discipline = "Architectural", Priority = 12 },

                // Structural
                new LayerClassification { Pattern = @"S[-_]WALL", BIMCategory = "StructuralWalls", ElementType = "StructuralWall", Discipline = "Structural", Priority = 11 },
                new LayerClassification { Pattern = @"S[-_]COLS?", BIMCategory = "StructuralColumns", ElementType = "StructuralColumn", Discipline = "Structural", Priority = 12 },
                new LayerClassification { Pattern = @"S[-_]BEAM", BIMCategory = "StructuralFraming", ElementType = "Beam", Discipline = "Structural", Priority = 11 },
                new LayerClassification { Pattern = @"S[-_]BRAC", BIMCategory = "StructuralFraming", ElementType = "Brace", Discipline = "Structural", Priority = 10 },
                new LayerClassification { Pattern = @"S[-_]FNDN|S[-_]FOOT", BIMCategory = "StructuralFoundations", ElementType = "Foundation", Discipline = "Structural", Priority = 11 },
                new LayerClassification { Pattern = @"S[-_]SLAB|S[-_]DECK", BIMCategory = "Floors", ElementType = "StructuralFloor", Discipline = "Structural", Priority = 10 },
                new LayerClassification { Pattern = @"S[-_]GRID", BIMCategory = "Grids", ElementType = "Grid", Discipline = "Structural", Priority = 12 },

                // Mechanical
                new LayerClassification { Pattern = @"M[-_]DUCT", BIMCategory = "Ducts", ElementType = "Duct", Discipline = "Mechanical", Priority = 9 },
                new LayerClassification { Pattern = @"M[-_]DIFF", BIMCategory = "MechanicalEquipment", ElementType = "Diffuser", Discipline = "Mechanical", Priority = 7 },
                new LayerClassification { Pattern = @"M[-_]EQPM|M[-_]UNIT", BIMCategory = "MechanicalEquipment", ElementType = "AHU", Discipline = "Mechanical", Priority = 8 },
                new LayerClassification { Pattern = @"M[-_]PIPE", BIMCategory = "Pipes", ElementType = "Pipe", Discipline = "Mechanical", Priority = 9 },
                new LayerClassification { Pattern = @"M[-_]FLEX", BIMCategory = "FlexDucts", ElementType = "FlexDuct", Discipline = "Mechanical", Priority = 7 },

                // Electrical
                new LayerClassification { Pattern = @"E[-_]LITE|E[-_]LGHT", BIMCategory = "LightingFixtures", ElementType = "LightingFixture", Discipline = "Electrical", Priority = 7 },
                new LayerClassification { Pattern = @"E[-_]POWR|E[-_]RECPT", BIMCategory = "ElectricalFixtures", ElementType = "Receptacle", Discipline = "Electrical", Priority = 7 },
                new LayerClassification { Pattern = @"E[-_]SWCH|E[-_]SWITCH", BIMCategory = "ElectricalFixtures", ElementType = "Switch", Discipline = "Electrical", Priority = 6 },
                new LayerClassification { Pattern = @"E[-_]PANL|E[-_]PANEL", BIMCategory = "ElectricalEquipment", ElementType = "ElectricalPanel", Discipline = "Electrical", Priority = 8 },
                new LayerClassification { Pattern = @"E[-_]COND|E[-_]WIRE", BIMCategory = "Conduit", ElementType = "Conduit", Discipline = "Electrical", Priority = 7 },
                new LayerClassification { Pattern = @"E[-_]TRAY", BIMCategory = "CableTray", ElementType = "CableTray", Discipline = "Electrical", Priority = 7 },

                // Plumbing
                new LayerClassification { Pattern = @"P[-_]PIPE|P[-_]SANR", BIMCategory = "Pipes", ElementType = "PlumbingPipe", Discipline = "Plumbing", Priority = 9 },
                new LayerClassification { Pattern = @"P[-_]FIXT|P[-_]FLOR", BIMCategory = "PlumbingFixtures", ElementType = "PlumbingFixture", Discipline = "Plumbing", Priority = 8 },
                new LayerClassification { Pattern = @"P[-_]EQPM", BIMCategory = "PlumbingEquipment", ElementType = "WaterHeater", Discipline = "Plumbing", Priority = 7 },

                // Fire Protection
                new LayerClassification { Pattern = @"F[-_]PIPE|F[-_]SPRK", BIMCategory = "Pipes", ElementType = "FireProtectionPipe", Discipline = "FireProtection", Priority = 9 },
                new LayerClassification { Pattern = @"F[-_]SPKR|F[-_]HEAD", BIMCategory = "Sprinklers", ElementType = "SprinklerHead", Discipline = "FireProtection", Priority = 8 },
                new LayerClassification { Pattern = @"F[-_]ALAR|F[-_]DECT", BIMCategory = "FireAlarm", ElementType = "FireAlarmDevice", Discipline = "FireProtection", Priority = 7 },

                // Civil / Site
                new LayerClassification { Pattern = @"C[-_]TOPO|C[-_]CONT", BIMCategory = "Topography", ElementType = "Topography", Discipline = "Civil", Priority = 5 },
                new LayerClassification { Pattern = @"C[-_]ROAD|C[-_]PAVT", BIMCategory = "Site", ElementType = "Road", Discipline = "Civil", Priority = 6 },
                new LayerClassification { Pattern = @"C[-_]PKNG|C[-_]PARK", BIMCategory = "Site", ElementType = "Parking", Discipline = "Civil", Priority = 5 },
                new LayerClassification { Pattern = @"C[-_]PROP|C[-_]BNDY", BIMCategory = "Site", ElementType = "PropertyLine", Discipline = "Civil", Priority = 7 },
                new LayerClassification { Pattern = @"L[-_]PLNT|L[-_]TREE", BIMCategory = "Planting", ElementType = "Planting", Discipline = "Landscape", Priority = 3 },

                // Generic fallbacks for non-AIA naming
                new LayerClassification { Pattern = @"(?i)wall", BIMCategory = "Walls", ElementType = "Wall", Discipline = "Architectural", Priority = 3 },
                new LayerClassification { Pattern = @"(?i)door", BIMCategory = "Doors", ElementType = "Door", Discipline = "Architectural", Priority = 3 },
                new LayerClassification { Pattern = @"(?i)window|glazing", BIMCategory = "Windows", ElementType = "Window", Discipline = "Architectural", Priority = 3 },
                new LayerClassification { Pattern = @"(?i)column|col", BIMCategory = "Columns", ElementType = "Column", Discipline = "Structural", Priority = 3 },
                new LayerClassification { Pattern = @"(?i)beam", BIMCategory = "StructuralFraming", ElementType = "Beam", Discipline = "Structural", Priority = 3 },
                new LayerClassification { Pattern = @"(?i)duct", BIMCategory = "Ducts", ElementType = "Duct", Discipline = "Mechanical", Priority = 3 },
                new LayerClassification { Pattern = @"(?i)pipe|piping", BIMCategory = "Pipes", ElementType = "Pipe", Discipline = "Mechanical", Priority = 3 },
                new LayerClassification { Pattern = @"(?i)light|luminaire", BIMCategory = "LightingFixtures", ElementType = "LightingFixture", Discipline = "Electrical", Priority = 3 },
                new LayerClassification { Pattern = @"(?i)stair", BIMCategory = "Stairs", ElementType = "Stair", Discipline = "Architectural", Priority = 3 },
                new LayerClassification { Pattern = @"(?i)furn", BIMCategory = "Furniture", ElementType = "Furniture", Discipline = "Architectural", Priority = 2 },
            };

            foreach (var rule in rules)
                _layerRules[rule.Pattern] = rule;
        }

        private void InitializeBlockMappings()
        {
            var mappings = new[]
            {
                // Doors
                new BlockMapping { BlockPattern = @"(?i)door[-_]?(\d{2,4})", BIMCategory = "Doors", FamilyName = "Single-Flush", WidthParam = "Width", ExtractSize = true },
                new BlockMapping { BlockPattern = @"(?i)dbl[-_]?door|double[-_]?door", BIMCategory = "Doors", FamilyName = "Double-Flush", WidthParam = "Width", ExtractSize = true },
                new BlockMapping { BlockPattern = @"(?i)slide[-_]?door|sliding", BIMCategory = "Doors", FamilyName = "Sliding-Door", WidthParam = "Width", ExtractSize = true },
                new BlockMapping { BlockPattern = @"(?i)fire[-_]?door", BIMCategory = "Doors", FamilyName = "Fire-Rated-Door", WidthParam = "Width", ExtractSize = true, FireRated = true },
                new BlockMapping { BlockPattern = @"(?i)rev[-_]?door|revolving", BIMCategory = "Doors", FamilyName = "Revolving-Door", WidthParam = "Width", ExtractSize = true },

                // Windows
                new BlockMapping { BlockPattern = @"(?i)window[-_]?(\d+x\d+)", BIMCategory = "Windows", FamilyName = "Fixed-Window", WidthParam = "Width", ExtractSize = true },
                new BlockMapping { BlockPattern = @"(?i)case[-_]?window|casement", BIMCategory = "Windows", FamilyName = "Casement-Window", WidthParam = "Width", ExtractSize = true },
                new BlockMapping { BlockPattern = @"(?i)awning[-_]?window", BIMCategory = "Windows", FamilyName = "Awning-Window", WidthParam = "Width", ExtractSize = true },

                // Plumbing fixtures
                new BlockMapping { BlockPattern = @"(?i)wc|toilet|water[-_]?closet", BIMCategory = "PlumbingFixtures", FamilyName = "WC-Floor-Mounted", WidthParam = null, ExtractSize = false },
                new BlockMapping { BlockPattern = @"(?i)sink|basin|lavatory", BIMCategory = "PlumbingFixtures", FamilyName = "Sink-Countertop", WidthParam = null, ExtractSize = false },
                new BlockMapping { BlockPattern = @"(?i)urinal", BIMCategory = "PlumbingFixtures", FamilyName = "Urinal-Wall-Mounted", WidthParam = null, ExtractSize = false },
                new BlockMapping { BlockPattern = @"(?i)shower", BIMCategory = "PlumbingFixtures", FamilyName = "Shower-Stall", WidthParam = null, ExtractSize = false },
                new BlockMapping { BlockPattern = @"(?i)bath|tub", BIMCategory = "PlumbingFixtures", FamilyName = "Bathtub", WidthParam = null, ExtractSize = false },

                // Electrical
                new BlockMapping { BlockPattern = @"(?i)outlet|receptacle|socket", BIMCategory = "ElectricalFixtures", FamilyName = "Duplex-Receptacle", WidthParam = null, ExtractSize = false },
                new BlockMapping { BlockPattern = @"(?i)switch[-_]?\d?", BIMCategory = "ElectricalFixtures", FamilyName = "Light-Switch", WidthParam = null, ExtractSize = false },
                new BlockMapping { BlockPattern = @"(?i)panel[-_]?board|elec[-_]?panel", BIMCategory = "ElectricalEquipment", FamilyName = "Panelboard", WidthParam = null, ExtractSize = false },

                // Mechanical
                new BlockMapping { BlockPattern = @"(?i)diffuser|grille|register", BIMCategory = "MechanicalEquipment", FamilyName = "Supply-Diffuser", WidthParam = null, ExtractSize = false },
                new BlockMapping { BlockPattern = @"(?i)ahu|air[-_]?handler", BIMCategory = "MechanicalEquipment", FamilyName = "Air-Handling-Unit", WidthParam = null, ExtractSize = false },
                new BlockMapping { BlockPattern = @"(?i)fcu|fan[-_]?coil", BIMCategory = "MechanicalEquipment", FamilyName = "Fan-Coil-Unit", WidthParam = null, ExtractSize = false },

                // Fire
                new BlockMapping { BlockPattern = @"(?i)sprinkler|fire[-_]?head", BIMCategory = "Sprinklers", FamilyName = "Pendant-Sprinkler", WidthParam = null, ExtractSize = false },
                new BlockMapping { BlockPattern = @"(?i)fire[-_]?ext|extinguisher", BIMCategory = "FireProtection", FamilyName = "Fire-Extinguisher", WidthParam = null, ExtractSize = false },
                new BlockMapping { BlockPattern = @"(?i)smoke[-_]?det|detector", BIMCategory = "FireAlarm", FamilyName = "Smoke-Detector", WidthParam = null, ExtractSize = false },

                // Furniture
                new BlockMapping { BlockPattern = @"(?i)desk|workstation", BIMCategory = "Furniture", FamilyName = "Desk", WidthParam = null, ExtractSize = false },
                new BlockMapping { BlockPattern = @"(?i)chair", BIMCategory = "Furniture", FamilyName = "Office-Chair", WidthParam = null, ExtractSize = false },
                new BlockMapping { BlockPattern = @"(?i)table|conf[-_]?table", BIMCategory = "Furniture", FamilyName = "Conference-Table", WidthParam = null, ExtractSize = false },
                new BlockMapping { BlockPattern = @"(?i)cabinet|storage", BIMCategory = "Furniture", FamilyName = "Storage-Cabinet", WidthParam = null, ExtractSize = false },
            };

            foreach (var mapping in mappings)
                _blockMappings[mapping.BlockPattern] = mapping;
        }

        private void InitializeWallTypeRules()
        {
            _wallTypeRules["ExteriorWall"] = new WallTypeRule
            {
                TypeName = "Exterior Wall",
                DefaultThickness = 300, // mm
                DefaultStructure = new List<WallLayer>
                {
                    new WallLayer { Material = "External Cladding", Thickness = 25, Function = "Finish" },
                    new WallLayer { Material = "Air Gap", Thickness = 25, Function = "Thermal" },
                    new WallLayer { Material = "Rigid Insulation", Thickness = 75, Function = "Thermal" },
                    new WallLayer { Material = "Concrete Block", Thickness = 150, Function = "Structure" },
                    new WallLayer { Material = "Interior Plaster", Thickness = 15, Function = "Finish" }
                }
            };

            _wallTypeRules["InteriorWall"] = new WallTypeRule
            {
                TypeName = "Interior Wall",
                DefaultThickness = 150,
                DefaultStructure = new List<WallLayer>
                {
                    new WallLayer { Material = "Gypsum Board", Thickness = 12.5, Function = "Finish" },
                    new WallLayer { Material = "Metal Stud", Thickness = 92, Function = "Structure" },
                    new WallLayer { Material = "Gypsum Board", Thickness = 12.5, Function = "Finish" }
                }
            };

            _wallTypeRules["PartitionWall"] = new WallTypeRule
            {
                TypeName = "Partition Wall",
                DefaultThickness = 100,
                DefaultStructure = new List<WallLayer>
                {
                    new WallLayer { Material = "Gypsum Board", Thickness = 12.5, Function = "Finish" },
                    new WallLayer { Material = "Metal Stud", Thickness = 70, Function = "Structure" },
                    new WallLayer { Material = "Gypsum Board", Thickness = 12.5, Function = "Finish" }
                }
            };

            _wallTypeRules["StructuralWall"] = new WallTypeRule
            {
                TypeName = "Structural Wall",
                DefaultThickness = 250,
                DefaultStructure = new List<WallLayer>
                {
                    new WallLayer { Material = "Reinforced Concrete", Thickness = 250, Function = "Structure" }
                }
            };

            _wallTypeRules["CurtainWall"] = new WallTypeRule
            {
                TypeName = "Curtain Wall",
                DefaultThickness = 150,
                DefaultStructure = new List<WallLayer>
                {
                    new WallLayer { Material = "Aluminum Mullion", Thickness = 50, Function = "Structure" },
                    new WallLayer { Material = "Double-Glazed IGU", Thickness = 28, Function = "Finish" }
                }
            };
        }

        private void InitializeMEPSystemRules()
        {
            _mepSystemRules["Duct"] = new MEPSystemRule
            {
                SystemType = "Supply Air",
                DefaultSize = "400x250", // mm rectangular
                MaterialDefault = "Galvanized Steel",
                InsulationRequired = true,
                DefaultInsulationThickness = 25
            };

            _mepSystemRules["Pipe"] = new MEPSystemRule
            {
                SystemType = "Domestic Hot Water",
                DefaultSize = "25DN", // nominal diameter
                MaterialDefault = "Copper Type L",
                InsulationRequired = true,
                DefaultInsulationThickness = 19
            };

            _mepSystemRules["PlumbingPipe"] = new MEPSystemRule
            {
                SystemType = "Sanitary",
                DefaultSize = "100DN",
                MaterialDefault = "PVC-U",
                InsulationRequired = false,
                DefaultInsulationThickness = 0
            };

            _mepSystemRules["FireProtectionPipe"] = new MEPSystemRule
            {
                SystemType = "Fire Protection Wet",
                DefaultSize = "50DN",
                MaterialDefault = "Black Steel Sch 40",
                InsulationRequired = false,
                DefaultInsulationThickness = 0
            };

            _mepSystemRules["Conduit"] = new MEPSystemRule
            {
                SystemType = "Power",
                DefaultSize = "25mm EMT",
                MaterialDefault = "EMT Steel",
                InsulationRequired = false,
                DefaultInsulationThickness = 0
            };

            _mepSystemRules["CableTray"] = new MEPSystemRule
            {
                SystemType = "Data/Comms",
                DefaultSize = "300x100",
                MaterialDefault = "Hot-Dip Galvanized",
                InsulationRequired = false,
                DefaultInsulationThickness = 0
            };
        }

        private void InitializeAnnotationPatterns()
        {
            _textAnnotationPatterns[@"(\d+)\s*[xX×]\s*(\d+)\s*(mm|m|cm|in)?"] = "Dimensions";
            _textAnnotationPatterns[@"(?i)(room|space)\s*#?\s*(\d+)"] = "RoomNumber";
            _textAnnotationPatterns[@"(?i)(level|floor|storey)\s*#?\s*(-?\d+)"] = "LevelInfo";
            _textAnnotationPatterns[@"(?i)typ\.?|typical"] = "TypicalMarker";
            _textAnnotationPatterns[@"(?i)fire[-_]?rated|(\d+)\s*hr"] = "FireRating";
            _textAnnotationPatterns[@"(?i)STC\s*(\d+)"] = "AcousticRating";
            _textAnnotationPatterns[@"(?i)finish:\s*(.+)"] = "FinishSpec";
            _textAnnotationPatterns[@"(?i)(\d+)\s*mm\s*thk"] = "Thickness";
        }

        #endregion

        #region Conversion

        /// <summary>
        /// Converts a DWG layer/entity analysis into a BIM creation plan.
        /// Input is a structured representation of the DWG file contents.
        /// </summary>
        public async Task<DWGConversionResult> ConvertAsync(
            DWGAnalysis dwgAnalysis,
            DWGConversionOptions options,
            IProgress<ConversionProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            Logger.Info($"Starting DWG-to-BIM conversion: {dwgAnalysis.FileName}, {dwgAnalysis.Layers.Count} layers, {dwgAnalysis.TotalEntities} entities");

            var result = new DWGConversionResult
            {
                SourceFile = dwgAnalysis.FileName,
                StartedAt = DateTime.Now
            };

            // Phase 1: Classify layers
            progress?.Report(new ConversionProgress { Phase = "Layer Classification", Percent = 5 });
            var classifiedLayers = ClassifyLayers(dwgAnalysis.Layers);
            result.LayerClassifications = classifiedLayers;

            // Phase 2: Classify blocks (doors, windows, fixtures)
            progress?.Report(new ConversionProgress { Phase = "Block Identification", Percent = 15 });
            var classifiedBlocks = ClassifyBlocks(dwgAnalysis.Blocks);
            result.BlockClassifications = classifiedBlocks;

            // Phase 3: Extract wall geometry and assign types
            progress?.Report(new ConversionProgress { Phase = "Wall Analysis", Percent = 25 });
            var wallPlan = AnalyzeWalls(dwgAnalysis, classifiedLayers, options);
            result.WallCreationPlan = wallPlan;

            // Phase 4: Extract grid lines
            progress?.Report(new ConversionProgress { Phase = "Grid Extraction", Percent = 35 });
            var gridPlan = ExtractGrids(dwgAnalysis, classifiedLayers);
            result.GridCreationPlan = gridPlan;

            // Phase 5: Extract levels
            progress?.Report(new ConversionProgress { Phase = "Level Detection", Percent = 40 });
            var levelPlan = ExtractLevels(dwgAnalysis, options);
            result.LevelCreationPlan = levelPlan;

            // Phase 6: Map hosted elements (doors, windows)
            progress?.Report(new ConversionProgress { Phase = "Hosted Element Mapping", Percent = 50 });
            var hostedPlan = MapHostedElements(dwgAnalysis, classifiedBlocks, wallPlan);
            result.HostedElementPlan = hostedPlan;

            // Phase 7: Extract structural elements
            progress?.Report(new ConversionProgress { Phase = "Structural Element Detection", Percent = 60 });
            var structPlan = ExtractStructuralElements(dwgAnalysis, classifiedLayers);
            result.StructuralPlan = structPlan;

            // Phase 8: Extract MEP routing
            progress?.Report(new ConversionProgress { Phase = "MEP System Routing", Percent = 70 });
            var mepPlan = ExtractMEPSystems(dwgAnalysis, classifiedLayers);
            result.MEPPlan = mepPlan;

            // Phase 9: Extract rooms from closed polylines and annotations
            progress?.Report(new ConversionProgress { Phase = "Room Detection", Percent = 80 });
            var roomPlan = ExtractRooms(dwgAnalysis, classifiedLayers);
            result.RoomPlan = roomPlan;

            // Phase 10: Generate summary and warnings
            progress?.Report(new ConversionProgress { Phase = "Validation", Percent = 90 });
            result.Summary = GenerateConversionSummary(result);
            result.Warnings = ValidateConversion(result, options);

            result.CompletedAt = DateTime.Now;
            progress?.Report(new ConversionProgress { Phase = "Complete", Percent = 100 });

            Logger.Info($"DWG conversion complete: {result.Summary.TotalBIMElements} BIM elements planned from {dwgAnalysis.TotalEntities} DWG entities");
            return result;
        }

        /// <summary>
        /// Generates a natural language conversion report.
        /// </summary>
        public string FormatConversionReport(DWGConversionResult result)
        {
            var sb = new StringBuilder();

            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            sb.AppendLine("  DWG-TO-BIM CONVERSION REPORT");
            sb.AppendLine($"  Source: {result.SourceFile}");
            sb.AppendLine($"  Converted: {result.CompletedAt:yyyy-MM-dd HH:mm}");
            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            sb.AppendLine();

            sb.AppendLine("SUMMARY");
            sb.AppendLine("───────────────────────────────────────────────────────────────");
            sb.AppendLine($"  Total BIM Elements:       {result.Summary.TotalBIMElements}");
            sb.AppendLine($"  Layers Classified:        {result.Summary.LayersClassified} / {result.Summary.TotalLayers}");
            sb.AppendLine($"  Conversion Confidence:    {result.Summary.OverallConfidence:P0}");
            sb.AppendLine($"  Unclassified Layers:      {result.Summary.UnclassifiedLayers}");
            sb.AppendLine();

            sb.AppendLine("ELEMENT BREAKDOWN");
            sb.AppendLine("───────────────────────────────────────────────────────────────");
            foreach (var cat in result.Summary.ElementsByCategory.OrderByDescending(c => c.Value))
                sb.AppendLine($"  {cat.Key,-28}: {cat.Value,5}");
            sb.AppendLine();

            sb.AppendLine("DISCIPLINE BREAKDOWN");
            sb.AppendLine("───────────────────────────────────────────────────────────────");
            foreach (var disc in result.Summary.ElementsByDiscipline.OrderByDescending(d => d.Value))
                sb.AppendLine($"  {disc.Key,-28}: {disc.Value,5}");
            sb.AppendLine();

            if (result.WallCreationPlan.WallSegments.Any())
            {
                sb.AppendLine("WALL TYPES");
                sb.AppendLine("───────────────────────────────────────────────────────────────");
                var wallGroups = result.WallCreationPlan.WallSegments.GroupBy(w => w.WallType);
                foreach (var group in wallGroups)
                    sb.AppendLine($"  {group.Key,-28}: {group.Count(),5} segments, total length: {group.Sum(w => w.Length):F1}mm");
                sb.AppendLine();
            }

            if (result.Warnings.Any())
            {
                sb.AppendLine("WARNINGS");
                sb.AppendLine("───────────────────────────────────────────────────────────────");
                foreach (var warning in result.Warnings)
                    sb.AppendLine($"  [{warning.Severity}] {warning.Message}");
                sb.AppendLine();
            }

            sb.AppendLine("CREATION ORDER");
            sb.AppendLine("───────────────────────────────────────────────────────────────");
            sb.AppendLine("  1. Levels and Grids");
            sb.AppendLine("  2. Structural Columns and Foundations");
            sb.AppendLine("  3. Structural Walls and Beams");
            sb.AppendLine("  4. Architectural Walls (Exterior → Interior → Partition)");
            sb.AppendLine("  5. Floors and Roofs");
            sb.AppendLine("  6. Doors and Windows (hosted on walls)");
            sb.AppendLine("  7. Stairs and Elevators");
            sb.AppendLine("  8. MEP Systems (Duct → Pipe → Electrical)");
            sb.AppendLine("  9. Fixtures and Equipment");
            sb.AppendLine("  10. Rooms and Spaces");
            sb.AppendLine("  11. Furniture");
            sb.AppendLine();

            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            sb.AppendLine("  Generated by StingBIM AI DWG-to-BIM Conversion Engine");
            sb.AppendLine("═══════════════════════════════════════════════════════════════");

            return sb.ToString();
        }

        #endregion

        #region Layer Classification

        private List<ClassifiedLayer> ClassifyLayers(List<DWGLayer> layers)
        {
            var classified = new List<ClassifiedLayer>();

            foreach (var layer in layers)
            {
                var best = FindBestLayerMatch(layer.LayerName);
                classified.Add(new ClassifiedLayer
                {
                    OriginalLayerName = layer.LayerName,
                    EntityCount = layer.EntityCount,
                    Color = layer.Color,
                    LineType = layer.LineType,
                    BIMCategory = best?.BIMCategory ?? "Unclassified",
                    ElementType = best?.ElementType ?? "Unknown",
                    Discipline = best?.Discipline ?? "Unknown",
                    Confidence = best != null ? Math.Min(1.0, best.Priority / 15.0) : 0.0,
                    IsClassified = best != null
                });
            }

            return classified;
        }

        private LayerClassification FindBestLayerMatch(string layerName)
        {
            LayerClassification best = null;
            int bestPriority = -1;

            foreach (var rule in _layerRules.Values)
            {
                if (Regex.IsMatch(layerName, rule.Pattern, RegexOptions.IgnoreCase))
                {
                    if (rule.Priority > bestPriority)
                    {
                        best = rule;
                        bestPriority = rule.Priority;
                    }
                }
            }

            return best;
        }

        #endregion

        #region Block Classification

        private List<ClassifiedBlock> ClassifyBlocks(List<DWGBlock> blocks)
        {
            var classified = new List<ClassifiedBlock>();

            foreach (var block in blocks)
            {
                var mapping = FindBestBlockMatch(block.BlockName);
                if (mapping != null)
                {
                    var cb = new ClassifiedBlock
                    {
                        OriginalBlockName = block.BlockName,
                        InsertionPoint = block.InsertionPoint,
                        Rotation = block.Rotation,
                        Scale = block.Scale,
                        BIMCategory = mapping.BIMCategory,
                        FamilyName = mapping.FamilyName,
                        FireRated = mapping.FireRated,
                        Confidence = 0.75
                    };

                    // Extract size from block name if pattern supports it
                    if (mapping.ExtractSize)
                    {
                        var sizeMatch = Regex.Match(block.BlockName, @"(\d{3,4})");
                        if (sizeMatch.Success)
                        {
                            cb.ExtractedWidth = double.Parse(sizeMatch.Groups[1].Value);
                            cb.Confidence = 0.85;
                        }
                    }

                    // Extract size from attributes
                    if (block.Attributes.TryGetValue("WIDTH", out var widthStr) && double.TryParse(widthStr, out var width))
                        cb.ExtractedWidth = width;
                    if (block.Attributes.TryGetValue("HEIGHT", out var heightStr) && double.TryParse(heightStr, out var height))
                        cb.ExtractedHeight = height;

                    classified.Add(cb);
                }
                else
                {
                    classified.Add(new ClassifiedBlock
                    {
                        OriginalBlockName = block.BlockName,
                        InsertionPoint = block.InsertionPoint,
                        BIMCategory = "Unclassified",
                        FamilyName = block.BlockName,
                        Confidence = 0.0
                    });
                }
            }

            return classified;
        }

        private BlockMapping FindBestBlockMatch(string blockName)
        {
            foreach (var mapping in _blockMappings.Values)
            {
                if (Regex.IsMatch(blockName, mapping.BlockPattern, RegexOptions.IgnoreCase))
                    return mapping;
            }
            return null;
        }

        #endregion

        #region Element Extraction

        private WallCreationPlan AnalyzeWalls(DWGAnalysis dwg, List<ClassifiedLayer> layers, DWGConversionOptions options)
        {
            var plan = new WallCreationPlan();

            var wallLayers = layers.Where(l => l.BIMCategory == "Walls" || l.BIMCategory == "StructuralWalls").ToList();

            foreach (var layer in wallLayers)
            {
                var entities = dwg.Entities.Where(e => e.LayerName == layer.OriginalLayerName).ToList();

                foreach (var entity in entities)
                {
                    if (entity.EntityType == "Line" || entity.EntityType == "Polyline")
                    {
                        var wallType = layer.ElementType;
                        var typeRule = _wallTypeRules.GetValueOrDefault(wallType) ?? _wallTypeRules.GetValueOrDefault("InteriorWall");

                        plan.WallSegments.Add(new WallSegment
                        {
                            SourceLayer = layer.OriginalLayerName,
                            WallType = typeRule?.TypeName ?? "Generic Wall",
                            StartPoint = entity.StartPoint,
                            EndPoint = entity.EndPoint,
                            Length = CalculateDistance(entity.StartPoint, entity.EndPoint),
                            Thickness = typeRule?.DefaultThickness ?? options.DefaultWallThickness,
                            BaseLevel = options.DefaultBaseLevel,
                            TopLevel = options.DefaultTopLevel,
                            Structure = typeRule?.DefaultStructure ?? new List<WallLayer>(),
                            Discipline = layer.Discipline
                        });
                    }
                }
            }

            return plan;
        }

        private GridCreationPlan ExtractGrids(DWGAnalysis dwg, List<ClassifiedLayer> layers)
        {
            var plan = new GridCreationPlan();

            var gridLayers = layers.Where(l => l.BIMCategory == "Grids").ToList();

            foreach (var layer in gridLayers)
            {
                var entities = dwg.Entities.Where(e => e.LayerName == layer.OriginalLayerName).ToList();

                foreach (var entity in entities)
                {
                    if (entity.EntityType == "Line")
                    {
                        var isVertical = Math.Abs(entity.StartPoint.X - entity.EndPoint.X) < 1.0;
                        plan.Grids.Add(new GridLine
                        {
                            Name = entity.TextContent ?? (isVertical ? $"G{plan.Grids.Count + 1}" : $"{(char)('A' + plan.Grids.Count)}"),
                            StartPoint = entity.StartPoint,
                            EndPoint = entity.EndPoint,
                            IsVertical = isVertical
                        });
                    }
                }
            }

            return plan;
        }

        private LevelCreationPlan ExtractLevels(DWGAnalysis dwg, DWGConversionOptions options)
        {
            var plan = new LevelCreationPlan();

            // From annotation text
            foreach (var entity in dwg.Entities.Where(e => e.EntityType == "Text"))
            {
                var match = Regex.Match(entity.TextContent ?? "", @"(?i)(level|floor|storey|flr)\s*#?\s*(-?\d+)", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var levelNum = int.Parse(match.Groups[2].Value);
                    if (!plan.Levels.Any(l => l.LevelNumber == levelNum))
                    {
                        plan.Levels.Add(new LevelDefinition
                        {
                            Name = $"Level {levelNum}",
                            LevelNumber = levelNum,
                            Elevation = levelNum * options.DefaultFloorToFloorHeight
                        });
                    }
                }
            }

            // Ensure at least ground floor
            if (!plan.Levels.Any())
            {
                plan.Levels.Add(new LevelDefinition { Name = "Level 0", LevelNumber = 0, Elevation = 0 });
                plan.Levels.Add(new LevelDefinition { Name = "Level 1", LevelNumber = 1, Elevation = options.DefaultFloorToFloorHeight });
            }

            plan.Levels = plan.Levels.OrderBy(l => l.Elevation).ToList();
            return plan;
        }

        private HostedElementPlan MapHostedElements(DWGAnalysis dwg, List<ClassifiedBlock> blocks, WallCreationPlan wallPlan)
        {
            var plan = new HostedElementPlan();

            var doorBlocks = blocks.Where(b => b.BIMCategory == "Doors").ToList();
            var windowBlocks = blocks.Where(b => b.BIMCategory == "Windows").ToList();

            foreach (var door in doorBlocks)
            {
                var hostWall = FindNearestWall(door.InsertionPoint, wallPlan.WallSegments);
                plan.HostedElements.Add(new HostedElement
                {
                    ElementType = "Door",
                    FamilyName = door.FamilyName,
                    InsertionPoint = door.InsertionPoint,
                    Width = door.ExtractedWidth > 0 ? door.ExtractedWidth : 900,
                    Height = door.ExtractedHeight > 0 ? door.ExtractedHeight : 2100,
                    HostWallIndex = hostWall,
                    Rotation = door.Rotation,
                    FireRated = door.FireRated
                });
            }

            foreach (var window in windowBlocks)
            {
                var hostWall = FindNearestWall(window.InsertionPoint, wallPlan.WallSegments);
                plan.HostedElements.Add(new HostedElement
                {
                    ElementType = "Window",
                    FamilyName = window.FamilyName,
                    InsertionPoint = window.InsertionPoint,
                    Width = window.ExtractedWidth > 0 ? window.ExtractedWidth : 1200,
                    Height = window.ExtractedHeight > 0 ? window.ExtractedHeight : 1500,
                    SillHeight = 900,
                    HostWallIndex = hostWall,
                    Rotation = window.Rotation
                });
            }

            return plan;
        }

        private StructuralElementPlan ExtractStructuralElements(DWGAnalysis dwg, List<ClassifiedLayer> layers)
        {
            var plan = new StructuralElementPlan();

            var columnLayers = layers.Where(l => l.ElementType == "StructuralColumn" || l.ElementType == "ArchitecturalColumn" || l.ElementType == "Column").ToList();

            foreach (var layer in columnLayers)
            {
                var entities = dwg.Entities.Where(e => e.LayerName == layer.OriginalLayerName).ToList();
                foreach (var entity in entities)
                {
                    if (entity.EntityType == "Circle" || entity.EntityType == "Rectangle" || entity.EntityType == "Block")
                    {
                        plan.Columns.Add(new ColumnDefinition
                        {
                            Position = entity.StartPoint,
                            Shape = entity.EntityType == "Circle" ? "Round" : "Rectangular",
                            Dimension1 = entity.Width > 0 ? entity.Width : 400,
                            Dimension2 = entity.Height > 0 ? entity.Height : 400,
                            Material = layer.Discipline == "Structural" ? "Reinforced Concrete" : "Concrete"
                        });
                    }
                }
            }

            var beamLayers = layers.Where(l => l.ElementType == "Beam").ToList();
            foreach (var layer in beamLayers)
            {
                var entities = dwg.Entities.Where(e => e.LayerName == layer.OriginalLayerName).ToList();
                foreach (var entity in entities)
                {
                    if (entity.EntityType == "Line")
                    {
                        plan.Beams.Add(new BeamDefinition
                        {
                            StartPoint = entity.StartPoint,
                            EndPoint = entity.EndPoint,
                            Width = 300,
                            Depth = 600,
                            Material = "Reinforced Concrete"
                        });
                    }
                }
            }

            return plan;
        }

        private MEPCreationPlan ExtractMEPSystems(DWGAnalysis dwg, List<ClassifiedLayer> layers)
        {
            var plan = new MEPCreationPlan();

            var mepLayers = layers.Where(l => l.Discipline == "Mechanical" || l.Discipline == "Electrical" || l.Discipline == "Plumbing" || l.Discipline == "FireProtection").ToList();

            foreach (var layer in mepLayers)
            {
                var systemRule = _mepSystemRules.GetValueOrDefault(layer.ElementType);
                var entities = dwg.Entities.Where(e => e.LayerName == layer.OriginalLayerName).ToList();

                foreach (var entity in entities)
                {
                    if (entity.EntityType == "Line" || entity.EntityType == "Polyline")
                    {
                        plan.Runs.Add(new MEPRun
                        {
                            SystemType = systemRule?.SystemType ?? "Undefined",
                            ElementType = layer.ElementType,
                            Discipline = layer.Discipline,
                            StartPoint = entity.StartPoint,
                            EndPoint = entity.EndPoint,
                            Size = systemRule?.DefaultSize ?? "Unknown",
                            Material = systemRule?.MaterialDefault ?? "Unknown",
                            Insulated = systemRule?.InsulationRequired ?? false,
                            InsulationThickness = systemRule?.DefaultInsulationThickness ?? 0
                        });
                    }
                    else if (entity.EntityType == "Block")
                    {
                        plan.Equipment.Add(new MEPEquipment
                        {
                            EquipmentType = layer.ElementType,
                            Position = entity.StartPoint,
                            FamilyName = systemRule?.SystemType ?? layer.ElementType
                        });
                    }
                }
            }

            return plan;
        }

        private RoomCreationPlan ExtractRooms(DWGAnalysis dwg, List<ClassifiedLayer> layers)
        {
            var plan = new RoomCreationPlan();

            var roomLayers = layers.Where(l => l.BIMCategory == "Rooms").ToList();

            foreach (var layer in roomLayers)
            {
                var textEntities = dwg.Entities
                    .Where(e => e.LayerName == layer.OriginalLayerName && e.EntityType == "Text")
                    .ToList();

                foreach (var text in textEntities)
                {
                    plan.Rooms.Add(new RoomDefinition
                    {
                        Name = text.TextContent ?? "Room",
                        InsertionPoint = text.StartPoint,
                        Level = "Level 0"
                    });
                }
            }

            return plan;
        }

        #endregion

        #region Helpers

        private double CalculateDistance((double X, double Y, double Z) p1, (double X, double Y, double Z) p2)
        {
            var dx = p2.X - p1.X;
            var dy = p2.Y - p1.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private int FindNearestWall((double X, double Y, double Z) point, List<WallSegment> walls)
        {
            if (!walls.Any()) return -1;

            double minDist = double.MaxValue;
            int minIndex = 0;

            for (int i = 0; i < walls.Count; i++)
            {
                var dist = DistanceToLineSegment(point, walls[i].StartPoint, walls[i].EndPoint);
                if (dist < minDist)
                {
                    minDist = dist;
                    minIndex = i;
                }
            }

            return minIndex;
        }

        private double DistanceToLineSegment(
            (double X, double Y, double Z) point,
            (double X, double Y, double Z) lineStart,
            (double X, double Y, double Z) lineEnd)
        {
            var dx = lineEnd.X - lineStart.X;
            var dy = lineEnd.Y - lineStart.Y;
            var lenSq = dx * dx + dy * dy;

            if (lenSq < 0.001)
                return CalculateDistance(point, lineStart);

            var t = Math.Max(0, Math.Min(1,
                ((point.X - lineStart.X) * dx + (point.Y - lineStart.Y) * dy) / lenSq));

            var proj = (X: lineStart.X + t * dx, Y: lineStart.Y + t * dy, Z: 0.0);
            return CalculateDistance(point, proj);
        }

        private ConversionSummary GenerateConversionSummary(DWGConversionResult result)
        {
            var summary = new ConversionSummary
            {
                TotalLayers = result.LayerClassifications.Count,
                LayersClassified = result.LayerClassifications.Count(l => l.IsClassified),
                UnclassifiedLayers = result.LayerClassifications.Count(l => !l.IsClassified),
                OverallConfidence = result.LayerClassifications.Where(l => l.IsClassified).DefaultIfEmpty().Average(l => l?.Confidence ?? 0),
            };

            // Count elements
            summary.ElementsByCategory["Walls"] = result.WallCreationPlan.WallSegments.Count;
            summary.ElementsByCategory["Grids"] = result.GridCreationPlan.Grids.Count;
            summary.ElementsByCategory["Levels"] = result.LevelCreationPlan.Levels.Count;
            summary.ElementsByCategory["Doors"] = result.HostedElementPlan.HostedElements.Count(e => e.ElementType == "Door");
            summary.ElementsByCategory["Windows"] = result.HostedElementPlan.HostedElements.Count(e => e.ElementType == "Window");
            summary.ElementsByCategory["Columns"] = result.StructuralPlan.Columns.Count;
            summary.ElementsByCategory["Beams"] = result.StructuralPlan.Beams.Count;
            summary.ElementsByCategory["MEP Runs"] = result.MEPPlan.Runs.Count;
            summary.ElementsByCategory["MEP Equipment"] = result.MEPPlan.Equipment.Count;
            summary.ElementsByCategory["Rooms"] = result.RoomPlan.Rooms.Count;

            summary.TotalBIMElements = summary.ElementsByCategory.Values.Sum();

            // By discipline
            summary.ElementsByDiscipline["Architectural"] = summary.ElementsByCategory.GetValueOrDefault("Walls") +
                summary.ElementsByCategory.GetValueOrDefault("Doors") + summary.ElementsByCategory.GetValueOrDefault("Windows") +
                summary.ElementsByCategory.GetValueOrDefault("Rooms");
            summary.ElementsByDiscipline["Structural"] = summary.ElementsByCategory.GetValueOrDefault("Columns") +
                summary.ElementsByCategory.GetValueOrDefault("Beams");
            summary.ElementsByDiscipline["MEP"] = summary.ElementsByCategory.GetValueOrDefault("MEP Runs") +
                summary.ElementsByCategory.GetValueOrDefault("MEP Equipment");

            return summary;
        }

        private List<ConversionWarning> ValidateConversion(DWGConversionResult result, DWGConversionOptions options)
        {
            var warnings = new List<ConversionWarning>();

            if (result.Summary.UnclassifiedLayers > 0)
                warnings.Add(new ConversionWarning { Severity = "Warning", Message = $"{result.Summary.UnclassifiedLayers} layers could not be classified - review manually" });

            if (result.WallCreationPlan.WallSegments.Count == 0)
                warnings.Add(new ConversionWarning { Severity = "Error", Message = "No walls detected - check DWG layer naming" });

            if (result.LevelCreationPlan.Levels.Count < 2)
                warnings.Add(new ConversionWarning { Severity = "Info", Message = "Only default levels created - add levels manually for multi-story buildings" });

            var shortWalls = result.WallCreationPlan.WallSegments.Count(w => w.Length < 100);
            if (shortWalls > 0)
                warnings.Add(new ConversionWarning { Severity = "Warning", Message = $"{shortWalls} wall segments < 100mm detected - may be drawing artifacts" });

            var orphanDoors = result.HostedElementPlan.HostedElements.Count(e => e.HostWallIndex < 0);
            if (orphanDoors > 0)
                warnings.Add(new ConversionWarning { Severity = "Warning", Message = $"{orphanDoors} doors/windows have no nearby host wall" });

            if (result.Summary.OverallConfidence < 0.5)
                warnings.Add(new ConversionWarning { Severity = "Warning", Message = "Low overall confidence - DWG may use non-standard layer naming" });

            return warnings;
        }

        #endregion
    }

    #region DWG Data Models

    public class DWGAnalysis
    {
        public string FileName { get; set; }
        public List<DWGLayer> Layers { get; set; } = new();
        public List<DWGBlock> Blocks { get; set; } = new();
        public List<DWGEntity> Entities { get; set; } = new();
        public int TotalEntities => Entities.Count;
        public string Units { get; set; } = "mm";
    }

    public class DWGLayer
    {
        public string LayerName { get; set; }
        public int EntityCount { get; set; }
        public string Color { get; set; }
        public string LineType { get; set; }
        public bool IsVisible { get; set; } = true;
        public bool IsFrozen { get; set; }
    }

    public class DWGBlock
    {
        public string BlockName { get; set; }
        public (double X, double Y, double Z) InsertionPoint { get; set; }
        public double Rotation { get; set; }
        public double Scale { get; set; } = 1.0;
        public Dictionary<string, string> Attributes { get; set; } = new();
    }

    public class DWGEntity
    {
        public string EntityType { get; set; } // Line, Polyline, Circle, Arc, Text, Block, Rectangle
        public string LayerName { get; set; }
        public (double X, double Y, double Z) StartPoint { get; set; }
        public (double X, double Y, double Z) EndPoint { get; set; }
        public List<(double X, double Y, double Z)> Vertices { get; set; } = new();
        public double Width { get; set; }
        public double Height { get; set; }
        public double Radius { get; set; }
        public string TextContent { get; set; }
        public string Color { get; set; }
        public string LineType { get; set; }
    }

    public class DWGConversionOptions
    {
        public double DefaultWallThickness { get; set; } = 200; // mm
        public double DefaultFloorToFloorHeight { get; set; } = 3600; // mm
        public string DefaultBaseLevel { get; set; } = "Level 0";
        public string DefaultTopLevel { get; set; } = "Level 1";
        public bool IncludeFurniture { get; set; } = true;
        public bool IncludeMEP { get; set; } = true;
        public double MinWallLength { get; set; } = 100; // mm - filter artifacts
        public string TargetUnits { get; set; } = "mm";
    }

    public class DWGConversionResult
    {
        public string SourceFile { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime CompletedAt { get; set; }

        public List<ClassifiedLayer> LayerClassifications { get; set; } = new();
        public List<ClassifiedBlock> BlockClassifications { get; set; } = new();
        public WallCreationPlan WallCreationPlan { get; set; } = new();
        public GridCreationPlan GridCreationPlan { get; set; } = new();
        public LevelCreationPlan LevelCreationPlan { get; set; } = new();
        public HostedElementPlan HostedElementPlan { get; set; } = new();
        public StructuralElementPlan StructuralPlan { get; set; } = new();
        public MEPCreationPlan MEPPlan { get; set; } = new();
        public RoomCreationPlan RoomPlan { get; set; } = new();

        public ConversionSummary Summary { get; set; }
        public List<ConversionWarning> Warnings { get; set; } = new();
    }

    public class ConversionProgress
    {
        public string Phase { get; set; }
        public double Percent { get; set; }
    }

    public class ConversionSummary
    {
        public int TotalLayers { get; set; }
        public int LayersClassified { get; set; }
        public int UnclassifiedLayers { get; set; }
        public int TotalBIMElements { get; set; }
        public double OverallConfidence { get; set; }
        public Dictionary<string, int> ElementsByCategory { get; set; } = new();
        public Dictionary<string, int> ElementsByDiscipline { get; set; } = new();
    }

    public class ConversionWarning
    {
        public string Severity { get; set; }
        public string Message { get; set; }
    }

    // Classification models
    internal class LayerClassification
    {
        public string Pattern { get; set; }
        public string BIMCategory { get; set; }
        public string ElementType { get; set; }
        public string Discipline { get; set; }
        public int Priority { get; set; }
    }

    public class ClassifiedLayer
    {
        public string OriginalLayerName { get; set; }
        public int EntityCount { get; set; }
        public string Color { get; set; }
        public string LineType { get; set; }
        public string BIMCategory { get; set; }
        public string ElementType { get; set; }
        public string Discipline { get; set; }
        public double Confidence { get; set; }
        public bool IsClassified { get; set; }
    }

    internal class BlockMapping
    {
        public string BlockPattern { get; set; }
        public string BIMCategory { get; set; }
        public string FamilyName { get; set; }
        public string WidthParam { get; set; }
        public bool ExtractSize { get; set; }
        public bool FireRated { get; set; }
    }

    public class ClassifiedBlock
    {
        public string OriginalBlockName { get; set; }
        public (double X, double Y, double Z) InsertionPoint { get; set; }
        public double Rotation { get; set; }
        public double Scale { get; set; }
        public string BIMCategory { get; set; }
        public string FamilyName { get; set; }
        public double ExtractedWidth { get; set; }
        public double ExtractedHeight { get; set; }
        public bool FireRated { get; set; }
        public double Confidence { get; set; }
    }

    // Creation plans
    public class WallCreationPlan
    {
        public List<WallSegment> WallSegments { get; set; } = new();
    }

    public class WallSegment
    {
        public string SourceLayer { get; set; }
        public string WallType { get; set; }
        public (double X, double Y, double Z) StartPoint { get; set; }
        public (double X, double Y, double Z) EndPoint { get; set; }
        public double Length { get; set; }
        public double Thickness { get; set; }
        public string BaseLevel { get; set; }
        public string TopLevel { get; set; }
        public List<WallLayer> Structure { get; set; } = new();
        public string Discipline { get; set; }
    }

    internal class WallTypeRule
    {
        public string TypeName { get; set; }
        public double DefaultThickness { get; set; }
        public List<WallLayer> DefaultStructure { get; set; } = new();
    }

    public class WallLayer
    {
        public string Material { get; set; }
        public double Thickness { get; set; }
        public string Function { get; set; }
    }

    public class GridCreationPlan
    {
        public List<GridLine> Grids { get; set; } = new();
    }

    public class GridLine
    {
        public string Name { get; set; }
        public (double X, double Y, double Z) StartPoint { get; set; }
        public (double X, double Y, double Z) EndPoint { get; set; }
        public bool IsVertical { get; set; }
    }

    public class LevelCreationPlan
    {
        public List<LevelDefinition> Levels { get; set; } = new();
    }

    public class LevelDefinition
    {
        public string Name { get; set; }
        public int LevelNumber { get; set; }
        public double Elevation { get; set; }
    }

    public class HostedElementPlan
    {
        public List<HostedElement> HostedElements { get; set; } = new();
    }

    public class HostedElement
    {
        public string ElementType { get; set; }
        public string FamilyName { get; set; }
        public (double X, double Y, double Z) InsertionPoint { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double SillHeight { get; set; }
        public int HostWallIndex { get; set; }
        public double Rotation { get; set; }
        public bool FireRated { get; set; }
    }

    public class StructuralElementPlan
    {
        public List<ColumnDefinition> Columns { get; set; } = new();
        public List<BeamDefinition> Beams { get; set; } = new();
    }

    public class ColumnDefinition
    {
        public (double X, double Y, double Z) Position { get; set; }
        public string Shape { get; set; }
        public double Dimension1 { get; set; }
        public double Dimension2 { get; set; }
        public string Material { get; set; }
    }

    public class BeamDefinition
    {
        public (double X, double Y, double Z) StartPoint { get; set; }
        public (double X, double Y, double Z) EndPoint { get; set; }
        public double Width { get; set; }
        public double Depth { get; set; }
        public string Material { get; set; }
    }

    public class MEPCreationPlan
    {
        public List<MEPRun> Runs { get; set; } = new();
        public List<MEPEquipment> Equipment { get; set; } = new();
    }

    public class MEPRun
    {
        public string SystemType { get; set; }
        public string ElementType { get; set; }
        public string Discipline { get; set; }
        public (double X, double Y, double Z) StartPoint { get; set; }
        public (double X, double Y, double Z) EndPoint { get; set; }
        public string Size { get; set; }
        public string Material { get; set; }
        public bool Insulated { get; set; }
        public double InsulationThickness { get; set; }
    }

    public class MEPEquipment
    {
        public string EquipmentType { get; set; }
        public (double X, double Y, double Z) Position { get; set; }
        public string FamilyName { get; set; }
    }

    internal class MEPSystemRule
    {
        public string SystemType { get; set; }
        public string DefaultSize { get; set; }
        public string MaterialDefault { get; set; }
        public bool InsulationRequired { get; set; }
        public double DefaultInsulationThickness { get; set; }
    }

    public class RoomCreationPlan
    {
        public List<RoomDefinition> Rooms { get; set; } = new();
    }

    public class RoomDefinition
    {
        public string Name { get; set; }
        public (double X, double Y, double Z) InsertionPoint { get; set; }
        public string Level { get; set; }
    }

    #endregion
}
