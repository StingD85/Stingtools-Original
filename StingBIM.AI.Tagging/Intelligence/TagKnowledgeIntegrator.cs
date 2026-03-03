// StingBIM.AI.Tagging - SuperIntelligent Tagging System
// TagKnowledgeIntegrator.cs - Knowledge graph integration for semantic tagging intelligence
// Uses StingBIM.AI.Knowledge's graph infrastructure to provide deep contextual understanding
// of what tags mean, how elements relate, and what information is most important to show.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NLog;
using StingBIM.AI.Tagging.Data;
using StingBIM.AI.Tagging.Models;

namespace StingBIM.AI.Tagging.Intelligence
{
    #region Enumerations

    /// <summary>
    /// Importance level for an element within the project context.
    /// </summary>
    public enum ImportanceLevel { Critical, Standard, Minor }

    /// <summary>
    /// How important a piece of information is for display in a tag.
    /// </summary>
    public enum ContentImportance { Critical, Required, Useful, Optional }

    /// <summary>
    /// Type of semantic validation issue found.
    /// </summary>
    public enum SemanticIssueType
    {
        NamingInconsistency, SystemMismatch, FireRatingInconsistency,
        RoomNamingInconsistency, EquipmentNamingMismatch, ImplausibleValue,
        MissingMandatoryInfo, BrokenReference
    }

    /// <summary>
    /// Part type for constructing intelligent element names.
    /// </summary>
    public enum NamingPart { Level, Room, Category, System, Sequence, Zone, Literal }

    #endregion

    #region Inner Types

    public class ElementSemantics
    {
        public int ElementId { get; set; }
        public string PrimaryFunction { get; set; }
        public List<string> SystemMembership { get; set; } = new List<string>();
        public SpatialContext SpatialContext { get; set; } = new SpatialContext();
        public List<int> ConnectedElements { get; set; } = new List<int>();
        public List<string> ApplicableStandards { get; set; } = new List<string>();
        public ImportanceLevel Importance { get; set; }
        public string CategoryName { get; set; }
        public string FamilyName { get; set; }
        public string TypeName { get; set; }
        public bool IsFireSafetyElement { get; set; }
        public bool IsEgressElement { get; set; }
        public bool IsStructural { get; set; }
        public string ImportanceRationale { get; set; }
    }

    public class SpatialContext
    {
        public string RoomName { get; set; }
        public string RoomNumber { get; set; }
        public string LevelName { get; set; }
        public string LevelAbbreviation { get; set; }
        public string FireZone { get; set; }
        public string AreaName { get; set; }
        public string ZoneId { get; set; }
    }

    public class ContentRecommendation
    {
        public string ParameterName { get; set; }
        public ContentImportance Importance { get; set; }
        public string Rationale { get; set; }
        public int DisplayOrder { get; set; }
        public string FormatSpecifier { get; set; }
        public bool IsViewTypeSpecific { get; set; }
        public TagViewType? ApplicableViewType { get; set; }
    }

    public class RelationshipChain
    {
        public int RootElementId { get; set; }
        public string RootCategory { get; set; }
        public List<RelationshipStep> Steps { get; set; } = new List<RelationshipStep>();
        public List<int> TerminalElementIds { get; set; } = new List<int>();
        public List<int> AllElementIds { get; set; } = new List<int>();
        public int MaxDepth { get; set; }
        public List<string> DisciplinesCrossed { get; set; } = new List<string>();
    }

    public class RelationshipStep
    {
        public int FromElementId { get; set; }
        public int ToElementId { get; set; }
        public string RelationType { get; set; }
        public string TargetCategory { get; set; }
        public int Depth { get; set; }
        public double Strength { get; set; }
    }

    public class NamingScheme
    {
        public string SchemeName { get; set; }
        public List<NamingPartDefinition> Parts { get; set; } = new List<NamingPartDefinition>();
        public string Separator { get; set; } = "-";
        public string CaseFormat { get; set; } = "upper";
        public int SequenceDigits { get; set; } = 2;
        public int SequenceStart { get; set; } = 1;
        public string ApplicableCategory { get; set; }
    }

    public class NamingPartDefinition
    {
        public NamingPart PartType { get; set; }
        public string LiteralValue { get; set; }
        public int MaxLength { get; set; }
        public string SourceParameter { get; set; }
    }

    public class PriorityScore
    {
        public int ElementId { get; set; }
        public double Score { get; set; }
        public List<PriorityFactor> Factors { get; set; } = new List<PriorityFactor>();
        public string Rationale { get; set; }
        public string RecommendedAction { get; set; }
    }

    public class PriorityFactor
    {
        public string FactorName { get; set; }
        public double Weight { get; set; }
        public double RawScore { get; set; }
        public double WeightedScore { get; set; }
        public string Description { get; set; }
    }

    public class SemanticValidationIssue
    {
        public string IssueId { get; set; }
        public SemanticIssueType IssueType { get; set; }
        public IssueSeverity Severity { get; set; }
        public string TagId { get; set; }
        public int ElementId { get; set; }
        public string Description { get; set; }
        public string ExpectedValue { get; set; }
        public string ActualValue { get; set; }
        public string SuggestedCorrection { get; set; }
        public bool IsAutoFixable { get; set; }
        public string KnowledgeSource { get; set; }
    }

    #endregion

    /// <summary>
    /// Knowledge graph integration for semantic tagging intelligence. Builds deep contextual
    /// understanding of what elements mean, how they relate across disciplines, and what
    /// information is most important to display. Bridges StingBIM.AI.Knowledge's graph
    /// infrastructure with the tagging system for semantic profiling, content recommendations,
    /// cross-discipline mapping, intelligent naming, priority calculation, and tag validation.
    /// </summary>
    public class TagKnowledgeIntegrator
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly TagRepository _repository;
        private readonly object _cacheLock = new object();
        private readonly object _sequenceLock = new object();

        private readonly Dictionary<int, ElementSemantics> _semanticsCache;
        private readonly int _maxCacheSize = 2000;
        private readonly Dictionary<string, int> _sequenceCounters;
        private readonly HashSet<string> _generatedNames;

        private static readonly string[] FireKeywords =
            { "fire", "rated", "smoke", "egress", "exit", "sprinkler", "alarm", "damper", "emergency", "evacuation", "refuge" };

        private static readonly string[] StructuralKeywords =
            { "structural", "load bearing", "load-bearing", "column", "beam", "foundation", "footing", "shear wall", "truss", "joist" };

        private static readonly Dictionary<string, string> CategoryDiscipline =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Walls", "Architectural" }, { "Doors", "Architectural" }, { "Windows", "Architectural" },
                { "Floors", "Architectural" }, { "Ceilings", "Architectural" }, { "Roofs", "Architectural" },
                { "Rooms", "Architectural" }, { "Furniture", "Architectural" }, { "Stairs", "Architectural" },
                { "Structural Columns", "Structural" }, { "Structural Framing", "Structural" },
                { "Structural Foundations", "Structural" },
                { "Mechanical Equipment", "Mechanical" }, { "Ducts", "Mechanical" },
                { "Air Terminals", "Mechanical" }, { "Flex Ducts", "Mechanical" },
                { "Electrical Equipment", "Electrical" }, { "Lighting Fixtures", "Electrical" },
                { "Cable Trays", "Electrical" }, { "Fire Alarm Devices", "Electrical" },
                { "Plumbing Fixtures", "Plumbing" }, { "Pipes", "Plumbing" },
                { "Sprinklers", "Fire Protection" }
            };

        private static readonly Dictionary<string, string> CategoryAbbrev =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Doors", "D" }, { "Windows", "W" }, { "Walls", "WL" }, { "Rooms", "RM" },
                { "Floors", "FL" }, { "Stairs", "ST" }, { "Structural Columns", "COL" },
                { "Structural Framing", "BM" }, { "Mechanical Equipment", "MEQ" },
                { "Ducts", "DT" }, { "Air Terminals", "AT" }, { "Electrical Equipment", "EP" },
                { "Lighting Fixtures", "LT" }, { "Fire Alarm Devices", "FA" },
                { "Plumbing Fixtures", "PF" }, { "Pipes", "PP" }, { "Sprinklers", "SPK" },
                { "Furniture", "FRN" }, { "Ceilings", "CLG" }, { "Casework", "CW" }
            };

        private static readonly Dictionary<string, string> RoomAbbrev =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Office", "OFF" }, { "Conference", "CONF" }, { "Meeting", "MTG" },
                { "Corridor", "CORR" }, { "Hallway", "HALL" }, { "Lobby", "LBY" },
                { "Bathroom", "BTH" }, { "Restroom", "WC" }, { "Kitchen", "KIT" },
                { "Storage", "STOR" }, { "Mechanical", "MECH" }, { "Electrical", "ELEC" },
                { "Stairwell", "STR" }, { "Elevator", "ELEV" }, { "Laboratory", "LAB" },
                { "Classroom", "CLS" }, { "Cafeteria", "CAF" }, { "Parking", "PKG" },
                { "Utility", "UTL" }, { "Bedroom", "BED" }, { "Living Room", "LVG" },
                { "Dining", "DIN" }, { "Laundry", "LDR" }, { "Garage", "GAR" }
            };

        private static readonly Dictionary<string, string> SystemAbbrev =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Air Handling Unit", "AHU" }, { "Fan Coil Unit", "FCU" },
                { "Variable Air Volume", "VAV" }, { "Rooftop Unit", "RTU" },
                { "Chiller", "CH" }, { "Boiler", "BLR" }, { "Cooling Tower", "CT" },
                { "Pump", "PMP" }, { "Exhaust Fan", "EF" }, { "Supply Fan", "SF" },
                { "Lighting Panel", "LP" }, { "Distribution Panel", "DP" },
                { "Switchboard", "SWB" }, { "Transformer", "TX" }, { "Generator", "GEN" },
                { "Fire Alarm Control Panel", "FACP" }, { "Smoke Detector", "SD" },
                { "Sprinkler Head", "SH" }, { "Water Heater", "WH" }
            };

        private static readonly List<(string Src, string Rel, string Tgt)> CrossDisciplineLinks =
            new List<(string, string, string)>
            {
                ("Electrical Equipment", "feedsCircuit", "Lighting Fixtures"),
                ("Electrical Equipment", "feedsCircuit", "Mechanical Equipment"),
                ("Electrical Equipment", "feedsPanel", "Electrical Equipment"),
                ("Mechanical Equipment", "suppliesAir", "Ducts"),
                ("Ducts", "terminatesAt", "Air Terminals"),
                ("Air Terminals", "serves", "Rooms"),
                ("Pipes", "connectsTo", "Plumbing Fixtures"),
                ("Plumbing Fixtures", "serves", "Rooms"),
                ("Structural Columns", "supports", "Structural Framing"),
                ("Structural Framing", "supports", "Floors"),
                ("Floors", "encloses", "Rooms"),
                ("Walls", "encloses", "Rooms"),
                ("Doors", "accessTo", "Rooms"),
                ("Sprinklers", "protects", "Rooms"),
                ("Lighting Fixtures", "illuminates", "Rooms")
            };

        /// <summary>Content profiles: category -> (param, importance, rationale, order).</summary>
        private static readonly Dictionary<string, List<(string P, ContentImportance I, string R, int O)>>
            ContentProfiles = new Dictionary<string, List<(string, ContentImportance, string, int)>>(StringComparer.OrdinalIgnoreCase)
            {
                { "Doors", new List<(string, ContentImportance, string, int)> {
                    ("Fire_Rating", ContentImportance.Critical, "Fire rating is life-safety critical", 1),
                    ("Door_Number", ContentImportance.Critical, "Primary identifier for door schedules", 2),
                    ("Mark", ContentImportance.Required, "Mark for cross-referencing", 3),
                    ("Swing_Direction", ContentImportance.Required, "Needed for egress compliance", 4),
                    ("Hardware_Set", ContentImportance.Useful, "Needed for procurement", 5),
                    ("Width", ContentImportance.Optional, "Available from schedule", 6) }},
                { "Windows", new List<(string, ContentImportance, string, int)> {
                    ("Mark", ContentImportance.Critical, "Primary window identifier", 1),
                    ("Type_Mark", ContentImportance.Required, "Schedule cross-referencing", 2),
                    ("Sill_Height", ContentImportance.Required, "Code compliance", 3),
                    ("Glass_Type", ContentImportance.Useful, "Thermal and safety compliance", 4) }},
                { "Rooms", new List<(string, ContentImportance, string, int)> {
                    ("Name", ContentImportance.Critical, "Primary spatial identifier", 1),
                    ("Number", ContentImportance.Critical, "Cross-referencing and wayfinding", 2),
                    ("Area", ContentImportance.Required, "Code-required for occupancy", 3),
                    ("Department", ContentImportance.Useful, "Space planning and reporting", 4),
                    ("Finish_Floor", ContentImportance.Optional, "Specification coordination", 5) }},
                { "Mechanical Equipment", new List<(string, ContentImportance, string, int)> {
                    ("Mark", ContentImportance.Critical, "Primary equipment identifier", 1),
                    ("Type_Mark", ContentImportance.Required, "Schedule cross-referencing", 2),
                    ("System_Type", ContentImportance.Required, "MEP coordination", 3),
                    ("Capacity", ContentImportance.Useful, "Load calculations", 4) }},
                { "Lighting Fixtures", new List<(string, ContentImportance, string, int)> {
                    ("Type_Mark", ContentImportance.Critical, "Primary lighting identifier", 1),
                    ("Circuit_Number", ContentImportance.Required, "Electrical coordination", 2),
                    ("Mounting_Height", ContentImportance.Required, "Installation coordination", 3),
                    ("Wattage", ContentImportance.Useful, "Load calculations", 4) }},
                { "Electrical Equipment", new List<(string, ContentImportance, string, int)> {
                    ("Mark", ContentImportance.Critical, "Primary equipment identifier", 1),
                    ("Panel_Name", ContentImportance.Critical, "Single-line diagram reference", 2),
                    ("Voltage", ContentImportance.Required, "Safety and coordination", 3),
                    ("Amperage", ContentImportance.Required, "Load calculations", 4) }},
                { "Structural Columns", new List<(string, ContentImportance, string, int)> {
                    ("Mark", ContentImportance.Critical, "Primary structural identifier", 1),
                    ("Grid_Intersection", ContentImportance.Required, "Grid location", 2),
                    ("Size", ContentImportance.Required, "Structural analysis", 3),
                    ("Material", ContentImportance.Useful, "Specification coordination", 4) }},
                { "Walls", new List<(string, ContentImportance, string, int)> {
                    ("Type_Mark", ContentImportance.Critical, "Partition schedule reference", 1),
                    ("Fire_Rating", ContentImportance.Critical, "Life-safety critical", 2),
                    ("STC_Rating", ContentImportance.Useful, "Acoustic compliance", 3) }},
                { "Sprinklers", new List<(string, ContentImportance, string, int)> {
                    ("Mark", ContentImportance.Critical, "Fire protection coordination", 1),
                    ("Coverage_Area", ContentImportance.Required, "Code compliance", 2),
                    ("Temperature_Rating", ContentImportance.Required, "Fire protection design", 3) }},
                { "Fire Alarm Devices", new List<(string, ContentImportance, string, int)> {
                    ("Mark", ContentImportance.Critical, "Device identifier", 1),
                    ("Device_Type", ContentImportance.Critical, "System programming", 2),
                    ("Zone", ContentImportance.Required, "Emergency response", 3),
                    ("Loop", ContentImportance.Required, "System wiring", 4) }}
            };

        #region Constructor

        public TagKnowledgeIntegrator(TagRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _semanticsCache = new Dictionary<int, ElementSemantics>();
            _sequenceCounters = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            _generatedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Logger.Info("TagKnowledgeIntegrator initialized");
        }

        #endregion

        #region 1. Semantic Element Understanding

        /// <summary>
        /// Builds a comprehensive semantic profile for an element by analyzing its parameters,
        /// category, relationships, and knowledge graph context.
        /// </summary>
        public ElementSemantics BuildElementSemantics(int elementId, Dictionary<string, object> parameters)
        {
            if (parameters == null) throw new ArgumentNullException(nameof(parameters));

            lock (_cacheLock)
            {
                if (_semanticsCache.TryGetValue(elementId, out var cached)) return cached;
            }

            var sem = new ElementSemantics { ElementId = elementId };
            sem.CategoryName = Param(parameters, "Category") ?? "";
            sem.FamilyName = Param(parameters, "Family") ?? "";
            sem.TypeName = Param(parameters, "Type") ?? "";
            sem.SpatialContext = BuildSpatialContext(parameters);
            sem.SystemMembership = DetermineSystemMembership(sem.CategoryName, sem.FamilyName, parameters);
            sem.PrimaryFunction = DeterminePrimaryFunction(sem.CategoryName, sem.FamilyName, sem.TypeName, parameters);
            sem.IsFireSafetyElement = DetectFireSafety(sem.CategoryName, sem.FamilyName, sem.TypeName, parameters);
            sem.IsEgressElement = DetectEgress(sem.CategoryName, sem.FamilyName, parameters);
            sem.IsStructural = DetectStructural(sem.CategoryName, sem.FamilyName, parameters);
            sem.ConnectedElements = DiscoverConnected(elementId, parameters);
            sem.ApplicableStandards = DetermineStandards(sem.CategoryName, sem.IsFireSafetyElement, sem.IsStructural);
            var (imp, rationale) = CalculateImportance(sem);
            sem.Importance = imp;
            sem.ImportanceRationale = rationale;

            lock (_cacheLock)
            {
                if (_semanticsCache.Count >= _maxCacheSize)
                {
                    var remove = _semanticsCache.Keys.Take(_maxCacheSize / 4).ToList();
                    foreach (var k in remove) _semanticsCache.Remove(k);
                }
                _semanticsCache[elementId] = sem;
            }

            Logger.Debug("Built semantics for element {0}: function={1}, importance={2}",
                elementId, sem.PrimaryFunction, sem.Importance);
            return sem;
        }

        private SpatialContext BuildSpatialContext(Dictionary<string, object> p)
        {
            var ctx = new SpatialContext
            {
                RoomName = Param(p, "Room_Name") ?? Param(p, "Room Name") ?? Param(p, "Room"),
                RoomNumber = Param(p, "Room_Number") ?? Param(p, "Room Number"),
                LevelName = Param(p, "Level") ?? Param(p, "Base_Level") ?? Param(p, "Reference_Level"),
                FireZone = Param(p, "Fire_Zone") ?? Param(p, "Fire Zone"),
                AreaName = Param(p, "Department") ?? Param(p, "Area_Name"),
                ZoneId = Param(p, "Zone") ?? Param(p, "Zone_Id") ?? Param(p, "Mechanical_Zone")
            };
            ctx.LevelAbbreviation = GenerateLevelAbbreviation(ctx.LevelName);
            return ctx;
        }

        private string GenerateLevelAbbreviation(string levelName)
        {
            if (string.IsNullOrEmpty(levelName)) return "";
            string u = levelName.Trim().ToUpperInvariant();
            var m = Regex.Match(u, @"(?:LEVEL|FLOOR|LVL|L)\s*(\d+)");
            if (m.Success) return "L" + m.Groups[1].Value;
            m = Regex.Match(u, @"(?:BASEMENT|BSMT|B)\s*(\d+)?");
            if (m.Success) return "B" + (m.Groups[1].Success ? m.Groups[1].Value : "1");
            if (u.Contains("GROUND") || u == "GF") return "GF";
            if (u.Contains("ROOF") || u == "RF") return "RF";
            if (u.Contains("MEZZ")) return "MZ";
            return u.Length <= 3 ? u : u.Substring(0, 3);
        }

        private List<string> DetermineSystemMembership(string cat, string family, Dictionary<string, object> p)
        {
            var sys = new List<string>();
            string st = Param(p, "System_Type") ?? Param(p, "System Type");
            if (!string.IsNullOrEmpty(st)) sys.Add(st);
            string sn = Param(p, "System_Name") ?? Param(p, "System Name");
            if (!string.IsNullOrEmpty(sn) && !sys.Contains(sn)) sys.Add(sn);
            if (sys.Count == 0 && CategoryDiscipline.TryGetValue(cat, out string disc)) sys.Add(disc);
            if (HasFireRating(p) && !sys.Any(s => s.Contains("Fire", StringComparison.OrdinalIgnoreCase)))
                sys.Add("Fire Protection");
            string circ = Param(p, "Circuit_Number") ?? Param(p, "Electrical_Circuit");
            if (!string.IsNullOrEmpty(circ) && !sys.Any(s => s.Contains("Electrical", StringComparison.OrdinalIgnoreCase)))
                sys.Add("Electrical");
            return sys;
        }

        private string DeterminePrimaryFunction(string cat, string fam, string typ, Dictionary<string, object> p)
        {
            string combined = $"{cat} {fam} {typ}".ToLowerInvariant();
            if (ContainsAny(combined, FireKeywords)) return "Fire Separation";
            if (ContainsAny(combined, StructuralKeywords)) return "Load Bearing";
            switch (cat?.ToLowerInvariant())
            {
                case "doors": return HasFireRating(p) ? "Fire Separation" : "Access";
                case "windows": return "Daylighting";
                case "walls": return HasFireRating(p) ? "Fire Separation" : "Space Division";
                case "rooms": return "Space Definition";
                case "floors": return "Horizontal Enclosure";
                case "stairs": return "Vertical Circulation";
                case "structural columns": return "Vertical Load Path";
                case "structural framing": return "Horizontal Load Path";
                case "structural foundations": return "Foundation Support";
                case "mechanical equipment": return InferMEPFunction(fam, typ);
                case "ducts": case "air terminals": return "Air Distribution";
                case "electrical equipment": return "Power Distribution";
                case "lighting fixtures": return "Illumination";
                case "fire alarm devices": return "Fire Detection";
                case "plumbing fixtures": return "Plumbing Service";
                case "pipes": return "Fluid Distribution";
                case "sprinklers": return "Fire Suppression";
                default: return "General";
            }
        }

        private string InferMEPFunction(string fam, string typ)
        {
            string c = $"{fam} {typ}".ToLowerInvariant();
            if (c.Contains("ahu") || c.Contains("air handling")) return "Air Handling";
            if (c.Contains("fcu") || c.Contains("fan coil")) return "Terminal Conditioning";
            if (c.Contains("vav")) return "Air Volume Control";
            if (c.Contains("chiller")) return "Chilled Water Production";
            if (c.Contains("boiler")) return "Hot Water Production";
            if (c.Contains("pump")) return "Fluid Circulation";
            if (c.Contains("fan") || c.Contains("exhaust")) return "Air Movement";
            return "HVAC Equipment";
        }

        private bool DetectFireSafety(string cat, string fam, string typ, Dictionary<string, object> p)
        {
            if (HasFireRating(p)) return true;
            string cl = cat?.ToLowerInvariant() ?? "";
            if (cl == "sprinklers" || cl == "fire alarm devices") return true;
            return ContainsAny($"{cat} {fam} {typ}".ToLowerInvariant(), FireKeywords);
        }

        private bool DetectEgress(string cat, string fam, Dictionary<string, object> p)
        {
            string cl = cat?.ToLowerInvariant() ?? "";
            if (cl == "stairs") return true;
            if (cl == "doors")
            {
                string func = Param(p, "Function") ?? "";
                if (func.ToLowerInvariant().Contains("exit")) return true;
                if (fam != null && (fam.ToLowerInvariant().Contains("exit") || fam.ToLowerInvariant().Contains("egress")))
                    return true;
            }
            if (cl == "rooms")
            {
                string name = (Param(p, "Name") ?? Param(p, "Room_Name") ?? "").ToLowerInvariant();
                if (name.Contains("corridor") || name.Contains("hallway")) return true;
            }
            return false;
        }

        private bool DetectStructural(string cat, string fam, Dictionary<string, object> p)
        {
            if ((cat?.ToLowerInvariant() ?? "").StartsWith("structural")) return true;
            if (cat?.ToLowerInvariant() == "walls")
            {
                string s = Param(p, "Structural") ?? Param(p, "Structural_Usage") ?? "";
                if (s.ToLowerInvariant() == "true" || s == "1" || s.ToLowerInvariant().Contains("bearing"))
                    return true;
            }
            return ContainsAny($"{cat} {fam}".ToLowerInvariant(), StructuralKeywords);
        }

        private List<int> DiscoverConnected(int elementId, Dictionary<string, object> p)
        {
            var connected = new List<int>();
            string[] keys = { "Connected_To", "Feeds", "Fed_From", "Host_Id", "Room_Id", "From_Room", "To_Room" };
            foreach (string key in keys)
            {
                string val = Param(p, key);
                if (string.IsNullOrEmpty(val)) continue;
                foreach (string part in val.Split(',', ';', '|'))
                    if (int.TryParse(part.Trim(), out int id) && id > 0 && id != elementId && !connected.Contains(id))
                        connected.Add(id);
            }
            return connected;
        }

        private List<string> DetermineStandards(string cat, bool fire, bool structural)
        {
            var s = new List<string> { "ISO 19650" };
            if (fire) { s.Add("NFPA 80"); s.Add("NFPA 101"); s.Add("IBC Chapter 7"); }
            if (structural) { s.Add("ASCE 7"); s.Add("ACI 318"); }
            string cl = cat?.ToLowerInvariant() ?? "";
            if (cl.Contains("mechanical") || cl == "ducts" || cl == "air terminals")
            { s.Add("ASHRAE 90.1"); s.Add("ASHRAE 62.1"); }
            else if (cl.Contains("electrical") || cl.Contains("lighting"))
            { s.Add("NEC 2023"); s.Add("NFPA 70"); }
            else if (cl.Contains("plumbing") || cl == "pipes") s.Add("IPC");
            else if (cl == "sprinklers") s.Add("NFPA 13");
            else if (cl == "fire alarm devices") s.Add("NFPA 72");
            else if (cl == "doors" || cl == "windows" || cl == "walls" || cl == "rooms") s.Add("IBC 2021");
            return s.Distinct().ToList();
        }

        private (ImportanceLevel, string) CalculateImportance(ElementSemantics sem)
        {
            if (sem.IsFireSafetyElement) return (ImportanceLevel.Critical, "Fire-safety element");
            if (sem.IsEgressElement) return (ImportanceLevel.Critical, "Egress element");
            if (sem.IsStructural) return (ImportanceLevel.Standard, "Structural element");
            string cl = sem.CategoryName?.ToLowerInvariant() ?? "";
            if (cl.Contains("mechanical equipment") || cl.Contains("electrical equipment"))
                return (ImportanceLevel.Standard, "Major MEP equipment");
            if (cl == "rooms" || cl == "doors" || cl == "windows" || cl == "walls" || cl == "stairs")
                return (ImportanceLevel.Standard, "Primary architectural element");
            if (cl.Contains("lighting") || cl.Contains("plumbing"))
                return (ImportanceLevel.Standard, "Building service fixture");
            return (ImportanceLevel.Minor, "Supplementary element");
        }

        #endregion

        #region 2. Tag Content Recommender

        /// <summary>
        /// Recommends what information a tag should display based on the element's semantic
        /// profile and the view type. Returns recommendations prioritized by importance.
        /// </summary>
        public List<ContentRecommendation> RecommendTagContent(int elementId, TagViewType viewType)
        {
            var recs = new List<ContentRecommendation>();
            var existingTags = _repository.GetTagsByHostElement(elementId);
            string category = existingTags.FirstOrDefault()?.CategoryName ?? "";

            ElementSemantics sem;
            lock (_cacheLock) { _semanticsCache.TryGetValue(elementId, out sem); }

            // Base recommendations from category profile
            if (ContentProfiles.TryGetValue(category, out var profile))
                foreach (var e in profile)
                    recs.Add(new ContentRecommendation
                    { ParameterName = e.P, Importance = e.I, Rationale = e.R, DisplayOrder = e.O });
            else
            {
                recs.Add(new ContentRecommendation
                { ParameterName = "Mark", Importance = ContentImportance.Critical, Rationale = "Universal identifier", DisplayOrder = 1 });
                recs.Add(new ContentRecommendation
                { ParameterName = "Type_Mark", Importance = ContentImportance.Required, Rationale = "Schedule reference", DisplayOrder = 2 });
            }

            // Augment with fire-safety recommendations
            if (sem?.IsFireSafetyElement == true)
            {
                bool hasFR = recs.Any(r => r.ParameterName.Contains("Fire_Rating", StringComparison.OrdinalIgnoreCase));
                if (!hasFR)
                    recs.Insert(0, new ContentRecommendation
                    { ParameterName = "Fire_Rating", Importance = ContentImportance.Critical,
                      Rationale = "Mandatory for fire-rated elements per NFPA 80/IBC", DisplayOrder = 0 });
                else
                    recs.First(r => r.ParameterName.Contains("Fire_Rating", StringComparison.OrdinalIgnoreCase))
                        .Importance = ContentImportance.Critical;
            }

            // Augment egress doors with swing direction
            if (sem?.IsEgressElement == true && category.Equals("Doors", StringComparison.OrdinalIgnoreCase))
            {
                var swing = recs.FirstOrDefault(r => r.ParameterName.Contains("Swing", StringComparison.OrdinalIgnoreCase));
                if (swing != null) { swing.Importance = ContentImportance.Critical; swing.Rationale = "Critical for egress compliance per IBC 1010.1.2"; }
                else recs.Add(new ContentRecommendation
                { ParameterName = "Swing_Direction", Importance = ContentImportance.Critical,
                  Rationale = "Egress door swing must be in direction of travel", DisplayOrder = 3 });
            }

            // Filter by view type
            recs = FilterByViewType(recs, viewType);
            recs = recs.OrderBy(r => r.Importance).ThenBy(r => r.DisplayOrder).ToList();
            Logger.Debug("Recommended {0} content items for element {1} in {2} view", recs.Count, elementId, viewType);
            return recs;
        }

        private List<ContentRecommendation> FilterByViewType(List<ContentRecommendation> recs, TagViewType vt)
        {
            return recs.Where(r =>
            {
                if (r.Importance == ContentImportance.Critical) return true;
                if (vt == TagViewType.ThreeDimensional) return r.Importance == ContentImportance.Required;
                if (vt == TagViewType.Section || vt == TagViewType.Elevation) return r.Importance != ContentImportance.Optional;
                return true;
            }).ToList();
        }

        #endregion

        #region 3. Cross-Discipline Relationship Mapper

        /// <summary>
        /// Maps the relationship chain from a root element across disciplines to the specified
        /// depth. Enables operations like "tag all devices on Circuit A" or "tag all elements
        /// in Fire Zone 2".
        /// </summary>
        public RelationshipChain MapRelationships(int elementId, int depth = 2)
        {
            depth = Math.Clamp(depth, 1, 5);
            var chain = new RelationshipChain { RootElementId = elementId };
            chain.AllElementIds.Add(elementId);

            string rootCat = GetElementCategory(elementId);
            chain.RootCategory = rootCat;
            if (CategoryDiscipline.TryGetValue(rootCat, out string rootDisc))
                chain.DisciplinesCrossed.Add(rootDisc);

            var visited = new HashSet<int> { elementId };
            var frontier = new Queue<(int Id, string Cat, int Depth)>();
            frontier.Enqueue((elementId, rootCat, 0));

            while (frontier.Count > 0)
            {
                var (curId, curCat, curDepth) = frontier.Dequeue();
                if (curDepth >= depth) continue;

                // Forward: outgoing relationships from this category
                foreach (var (src, rel, tgt) in CrossDisciplineLinks.Where(l =>
                    string.Equals(l.Src, curCat, StringComparison.OrdinalIgnoreCase)))
                {
                    foreach (int targetId in FindConnectedByCategory(curId, tgt))
                    {
                        if (!visited.Add(targetId)) continue;
                        chain.Steps.Add(new RelationshipStep
                        {
                            FromElementId = curId, ToElementId = targetId, RelationType = rel,
                            TargetCategory = tgt, Depth = curDepth + 1, Strength = 1.0
                        });
                        chain.AllElementIds.Add(targetId);
                        if (CategoryDiscipline.TryGetValue(tgt, out string d) && !chain.DisciplinesCrossed.Contains(d))
                            chain.DisciplinesCrossed.Add(d);
                        frontier.Enqueue((targetId, tgt, curDepth + 1));
                    }
                }

                // Reverse: incoming relationships to this category
                foreach (var (src, rel, tgt) in CrossDisciplineLinks.Where(l =>
                    string.Equals(l.Tgt, curCat, StringComparison.OrdinalIgnoreCase)))
                {
                    foreach (int sourceId in FindConnectedByCategory(curId, src))
                    {
                        if (!visited.Add(sourceId)) continue;
                        chain.Steps.Add(new RelationshipStep
                        {
                            FromElementId = sourceId, ToElementId = curId, RelationType = rel,
                            TargetCategory = curCat, Depth = curDepth + 1, Strength = 0.8
                        });
                        chain.AllElementIds.Add(sourceId);
                        if (CategoryDiscipline.TryGetValue(src, out string d) && !chain.DisciplinesCrossed.Contains(d))
                            chain.DisciplinesCrossed.Add(d);
                        frontier.Enqueue((sourceId, src, curDepth + 1));
                    }
                }
            }

            var withOutgoing = new HashSet<int>(chain.Steps.Select(s => s.FromElementId));
            chain.TerminalElementIds = chain.AllElementIds
                .Where(id => id != elementId && !withOutgoing.Contains(id)).ToList();
            chain.MaxDepth = chain.Steps.Count > 0 ? chain.Steps.Max(s => s.Depth) : 0;

            Logger.Debug("Mapped relationships for element {0}: {1} steps, {2} elements, depth {3}",
                elementId, chain.Steps.Count, chain.AllElementIds.Count, chain.MaxDepth);
            return chain;
        }

        private string GetElementCategory(int elementId)
        {
            lock (_cacheLock)
            {
                if (_semanticsCache.TryGetValue(elementId, out var sem)) return sem.CategoryName;
            }
            return _repository.GetTagsByHostElement(elementId).FirstOrDefault()?.CategoryName ?? "";
        }

        private List<int> FindConnectedByCategory(int sourceId, string targetCategory)
        {
            var results = new List<int>();
            lock (_cacheLock)
            {
                // Check if source has connections to target-category elements
                if (_semanticsCache.TryGetValue(sourceId, out var srcSem))
                {
                    foreach (int cid in srcSem.ConnectedElements)
                        if (_semanticsCache.TryGetValue(cid, out var cSem) &&
                            string.Equals(cSem.CategoryName, targetCategory, StringComparison.OrdinalIgnoreCase))
                            results.Add(cid);
                }
                // Check if target-category elements have connections to source
                foreach (var kvp in _semanticsCache)
                    if (kvp.Key != sourceId && !results.Contains(kvp.Key) &&
                        string.Equals(kvp.Value.CategoryName, targetCategory, StringComparison.OrdinalIgnoreCase) &&
                        kvp.Value.ConnectedElements.Contains(sourceId))
                        results.Add(kvp.Key);
            }
            // Also check repository for co-located elements
            var srcTags = _repository.GetTagsByHostElement(sourceId);
            if (srcTags.Count > 0)
            {
                int viewId = srcTags.First().ViewId;
                foreach (var t in _repository.GetTagsByView(viewId))
                    if (t.HostElementId != sourceId && !results.Contains(t.HostElementId) &&
                        string.Equals(t.CategoryName, targetCategory, StringComparison.OrdinalIgnoreCase))
                        results.Add(t.HostElementId);
            }
            return results;
        }

        #endregion

        #region 4. Tag Naming Intelligence

        /// <summary>
        /// Generates an intelligent, context-aware name for an element using its semantic profile
        /// and the provided naming scheme. Produces identifiers like "L2-OFF-D01" or "AHU-01".
        /// </summary>
        public string GenerateIntelligentName(int elementId, NamingScheme scheme)
        {
            if (scheme == null) throw new ArgumentNullException(nameof(scheme));
            ElementSemantics sem;
            lock (_cacheLock)
            {
                if (!_semanticsCache.TryGetValue(elementId, out sem))
                    sem = BuildMinimalSemantics(elementId);
            }

            var parts = new List<string>();
            foreach (var pd in scheme.Parts)
            {
                string v = ResolvePart(pd, sem, scheme);
                if (!string.IsNullOrEmpty(v)) parts.Add(v);
            }

            string baseName = string.Join(scheme.Separator, parts);
            if (scheme.CaseFormat == "upper") baseName = baseName.ToUpperInvariant();
            else if (scheme.CaseFormat == "lower") baseName = baseName.ToLowerInvariant();

            string unique = EnsureUnique(baseName, scheme);
            Logger.Debug("Generated name for element {0}: {1}", elementId, unique);
            return unique;
        }

        /// <summary>
        /// Creates a standard naming scheme for the specified category.
        /// </summary>
        public NamingScheme CreateStandardScheme(string category)
        {
            string cl = category?.ToLowerInvariant() ?? "";
            switch (cl)
            {
                case "doors":
                    return MakeScheme("Door Naming", category, new[] { NamingPart.Level, NamingPart.Room, NamingPart.Category, NamingPart.Sequence }, 2);
                case "windows":
                    return MakeScheme("Window Naming", category, new[] { NamingPart.Level, NamingPart.Category, NamingPart.Sequence }, 2);
                case "mechanical equipment": case "electrical equipment":
                    return MakeScheme("Equipment Naming", category, new[] { NamingPart.System, NamingPart.Sequence }, 2);
                case "rooms":
                    return new NamingScheme
                    { SchemeName = "Room Naming", Parts = new List<NamingPartDefinition>
                      { new NamingPartDefinition { PartType = NamingPart.Level },
                        new NamingPartDefinition { PartType = NamingPart.Sequence } },
                      Separator = "", SequenceDigits = 2, ApplicableCategory = "Rooms" };
                case "lighting fixtures":
                    return MakeScheme("Lighting Naming", category, new[] { NamingPart.Category, NamingPart.Sequence }, 3);
                default:
                    return MakeScheme("Generic Naming", category, new[] { NamingPart.Category, NamingPart.Sequence }, 3);
            }
        }

        /// <summary>Resets all sequence counters and name registry.</summary>
        public void ResetNamingState()
        {
            lock (_sequenceLock) { _sequenceCounters.Clear(); _generatedNames.Clear(); }
            Logger.Info("Naming state reset");
        }

        /// <summary>Registers an existing name to prevent future collisions.</summary>
        public void RegisterExistingName(string existingName)
        {
            if (!string.IsNullOrEmpty(existingName))
                lock (_sequenceLock) { _generatedNames.Add(existingName); }
        }

        private NamingScheme MakeScheme(string name, string cat, NamingPart[] parts, int digits)
        {
            return new NamingScheme
            {
                SchemeName = name,
                Parts = parts.Select(p => new NamingPartDefinition { PartType = p, MaxLength = p == NamingPart.Room ? 4 : 0 }).ToList(),
                Separator = "-", SequenceDigits = digits, ApplicableCategory = cat
            };
        }

        private ElementSemantics BuildMinimalSemantics(int elementId)
        {
            var tag = _repository.GetTagsByHostElement(elementId).FirstOrDefault();
            return new ElementSemantics
            {
                ElementId = elementId, CategoryName = tag?.CategoryName ?? "",
                FamilyName = tag?.FamilyName ?? "", TypeName = tag?.TypeName ?? "",
                SpatialContext = new SpatialContext(), PrimaryFunction = "General"
            };
        }

        private string ResolvePart(NamingPartDefinition pd, ElementSemantics sem, NamingScheme scheme)
        {
            string v;
            switch (pd.PartType)
            {
                case NamingPart.Level: v = sem.SpatialContext?.LevelAbbreviation ?? ""; break;
                case NamingPart.Room:
                    v = ResolveRoomAbbrev(sem.SpatialContext?.RoomName); break;
                case NamingPart.Category:
                    v = CategoryAbbrev.TryGetValue(sem.CategoryName, out string ca) ? ca
                        : (sem.CategoryName?.Length >= 2 ? sem.CategoryName.Substring(0, 2).ToUpperInvariant() : ""); break;
                case NamingPart.System:
                    v = ResolveSystemAbbrev(sem.FamilyName, sem.TypeName, sem.SystemMembership); break;
                case NamingPart.Sequence: v = NextSequence(sem, scheme); break;
                case NamingPart.Zone: v = sem.SpatialContext?.ZoneId ?? sem.SpatialContext?.FireZone ?? ""; break;
                case NamingPart.Literal: v = pd.LiteralValue ?? ""; break;
                default: v = ""; break;
            }
            if (pd.MaxLength > 0 && v.Length > pd.MaxLength) v = v.Substring(0, pd.MaxLength);
            return v;
        }

        private string ResolveRoomAbbrev(string roomName)
        {
            if (string.IsNullOrEmpty(roomName)) return "";
            foreach (var kvp in RoomAbbrev)
                if (roomName.IndexOf(kvp.Key, StringComparison.OrdinalIgnoreCase) >= 0) return kvp.Value;
            string u = roomName.ToUpperInvariant();
            var sb = new StringBuilder();
            sb.Append(u[0]);
            for (int i = 1; i < u.Length && sb.Length < 4; i++)
                if (!"AEIOU".Contains(u[i]) && char.IsLetter(u[i])) sb.Append(u[i]);
            return sb.ToString();
        }

        private string ResolveSystemAbbrev(string fam, string typ, List<string> sys)
        {
            string combined = $"{fam} {typ}";
            foreach (var kvp in SystemAbbrev)
                if (combined.IndexOf(kvp.Key, StringComparison.OrdinalIgnoreCase) >= 0) return kvp.Value;
            if (sys != null)
                foreach (string s in sys)
                    foreach (var kvp in SystemAbbrev)
                        if (s.IndexOf(kvp.Key, StringComparison.OrdinalIgnoreCase) >= 0) return kvp.Value;
            if (!string.IsNullOrEmpty(fam))
            { string clean = Regex.Replace(fam, @"[^A-Za-z]", ""); return clean.Length >= 3 ? clean.Substring(0, 3).ToUpperInvariant() : clean.ToUpperInvariant(); }
            return "";
        }

        private string NextSequence(ElementSemantics sem, NamingScheme scheme)
        {
            string scope = $"{sem.SpatialContext?.LevelAbbreviation ?? "XX"}_{sem.CategoryName ?? "Gen"}";
            int next;
            lock (_sequenceLock)
            {
                if (!_sequenceCounters.TryGetValue(scope, out int cur)) cur = scheme.SequenceStart - 1;
                _sequenceCounters[scope] = ++cur;
                next = cur;
            }
            return next.ToString(CultureInfo.InvariantCulture).PadLeft(scheme.SequenceDigits, '0');
        }

        private string EnsureUnique(string baseName, NamingScheme scheme)
        {
            lock (_sequenceLock)
            {
                if (_generatedNames.Add(baseName)) return baseName;
                for (int i = 2; i < 1000; i++)
                {
                    string candidate = $"{baseName}{scheme.Separator}{i.ToString().PadLeft(scheme.SequenceDigits, '0')}";
                    if (_generatedNames.Add(candidate))
                    { Logger.Warn("Name collision resolved: {0} -> {1}", baseName, candidate); return candidate; }
                }
                string fallback = $"{baseName}{scheme.Separator}{Guid.NewGuid().ToString("N").Substring(0, 4)}";
                _generatedNames.Add(fallback);
                return fallback;
            }
        }

        #endregion

        #region 5. Contextual Priority Calculator

        /// <summary>
        /// Calculates tagging priority for each element based on safety importance, code
        /// requirements, category importance, coverage gaps, connectivity, and user history.
        /// Returns a list sorted highest-priority-first.
        /// </summary>
        public List<PriorityScore> CalculateTaggingPriority(List<int> elementIds)
        {
            if (elementIds == null || elementIds.Count == 0) return new List<PriorityScore>();
            var scores = elementIds.Select(CalculateSinglePriority).OrderByDescending(s => s.Score).ToList();
            Logger.Debug("Calculated priority for {0} elements, top={1:F1}", scores.Count, scores.FirstOrDefault()?.Score ?? 0);
            return scores;
        }

        private PriorityScore CalculateSinglePriority(int elementId)
        {
            ElementSemantics sem;
            lock (_cacheLock)
            { if (!_semanticsCache.TryGetValue(elementId, out sem)) sem = BuildMinimalSemantics(elementId); }

            var factors = new List<PriorityFactor>
            {
                MakeFactor("SafetyImportance", 0.35,
                    sem.IsFireSafetyElement ? 100 : sem.IsEgressElement ? 90 : sem.IsStructural ? 70 : 10,
                    sem.IsFireSafetyElement ? "Fire-safety element" : sem.IsEgressElement ? "Egress element" :
                    sem.IsStructural ? "Structural element" : "Non-safety element"),
                MakeFactor("CodeRequirement", 0.25,
                    sem.IsFireSafetyElement ? 85 : Math.Min(90, 20 + (sem.ApplicableStandards?.Count ?? 0) * 15),
                    $"Regulated by {sem.ApplicableStandards?.Count ?? 0} standards"),
                MakeFactor("CategoryImportance", 0.15,
                    sem.Importance == ImportanceLevel.Critical ? 100 : sem.Importance == ImportanceLevel.Standard ? 60 : 20,
                    $"{sem.Importance} importance category"),
                MakeFactor("CoverageGap", 0.10,
                    _repository.GetTagsByHostElement(elementId).Any(t => t.State == TagState.Active) ? 10 : 80,
                    _repository.GetTagsByHostElement(elementId).Any(t => t.State == TagState.Active) ? "Already tagged" : "Untagged element"),
                MakeFactor("ConnectivityImportance", 0.10,
                    (sem.ConnectedElements?.Count ?? 0) >= 5 ? 90 : (sem.ConnectedElements?.Count ?? 0) >= 3 ? 60 :
                    (sem.ConnectedElements?.Count ?? 0) >= 1 ? 40 : 15,
                    $"{sem.ConnectedElements?.Count ?? 0} connections"),
                CalcFrequencyFactor(elementId)
            };

            double total = 0;
            foreach (var f in factors) { f.WeightedScore = f.Weight * f.RawScore; total += f.WeightedScore; }
            total = Math.Clamp(total, 0, 100);

            var topTwo = factors.OrderByDescending(f => f.WeightedScore).Take(2).ToList();
            string rationale = $"{sem.CategoryName ?? "Element"} ({sem.PrimaryFunction}): " +
                               string.Join("; ", topTwo.Select(f => f.Description.ToLowerInvariant()));

            return new PriorityScore
            {
                ElementId = elementId, Score = total, Factors = factors, Rationale = rationale,
                RecommendedAction = total >= 70 ? "TagImmediately" : total >= 40 ? "TagWhenConvenient" : "TagOptional"
            };
        }

        private PriorityFactor CalcFrequencyFactor(int elementId)
        {
            var ops = _repository.GetRecentOperations(200);
            int count = ops.Count(o =>
                (o.NewState != null && o.NewState.HostElementId == elementId) ||
                (o.PreviousState != null && o.PreviousState.HostElementId == elementId));
            return MakeFactor("UserInteractionFrequency", 0.05,
                count >= 5 ? 80 : count >= 2 ? 50 : 20,
                $"{count} recent interactions");
        }

        private static PriorityFactor MakeFactor(string name, double weight, double raw, string desc)
            => new PriorityFactor { FactorName = name, Weight = weight, RawScore = raw, Description = desc };

        #endregion

        #region 6. Knowledge-Based Tag Validation

        /// <summary>
        /// Validates tag content against the knowledge graph and project conventions. Checks
        /// naming consistency, system membership, fire rating compliance, and semantic plausibility.
        /// </summary>
        public List<SemanticValidationIssue> ValidateTagSemantics(TagInstance tag)
        {
            if (tag == null) throw new ArgumentNullException(nameof(tag));
            var issues = new List<SemanticValidationIssue>();
            ElementSemantics sem;
            lock (_cacheLock) { _semanticsCache.TryGetValue(tag.HostElementId, out sem); }

            ValidateNaming(tag, issues);
            ValidateSystemMatch(tag, sem, issues);
            ValidateFireRating(tag, sem, issues);
            ValidateRoomNaming(tag, issues);
            ValidateEquipmentNaming(tag, issues);
            ValidatePlausibility(tag, issues);
            ValidateMandatoryInfo(tag, sem, issues);

            Logger.Debug("Semantic validation for tag {0}: {1} issues", tag.TagId, issues.Count);
            return issues;
        }

        private void ValidateNaming(TagInstance tag, List<SemanticValidationIssue> issues)
        {
            if (string.IsNullOrEmpty(tag.DisplayText)) return;
            string cl = tag.CategoryName?.ToLowerInvariant() ?? "";
            if (cl == "doors" && !Regex.IsMatch(tag.DisplayText, @"\d+"))
                issues.Add(MakeIssue(SemanticIssueType.NamingInconsistency, IssueSeverity.Warning, tag,
                    "Door tag has no numeric identifier", tag.DisplayText,
                    "Pattern with numeric ID (e.g., D-101)", "Add numeric door identifier",
                    "Door naming convention: ISO 19650"));
            if (cl == "rooms" && tag.DisplayText.Length > 50)
                issues.Add(MakeIssue(SemanticIssueType.NamingInconsistency, IssueSeverity.Warning, tag,
                    "Room tag text unusually long", tag.DisplayText, "Under 50 characters",
                    "Abbreviate room name", "Room naming convention"));
        }

        private void ValidateSystemMatch(TagInstance tag, ElementSemantics sem, List<SemanticValidationIssue> issues)
        {
            if (sem == null || string.IsNullOrEmpty(tag.DisplayText)) return;
            foreach (var kvp in SystemAbbrev)
            {
                if (!tag.DisplayText.Contains(kvp.Value, StringComparison.OrdinalIgnoreCase)) continue;
                bool belongs = sem.SystemMembership.Any(s => s.IndexOf(kvp.Key, StringComparison.OrdinalIgnoreCase) >= 0);
                bool familyMatch = $"{sem.FamilyName} {sem.TypeName}".ToLowerInvariant().Contains(kvp.Key.ToLowerInvariant());
                if (!belongs && !familyMatch)
                    issues.Add(MakeIssue(SemanticIssueType.SystemMismatch, IssueSeverity.Critical, tag,
                        $"Tag contains '{kvp.Value}' ({kvp.Key}) but element is not in that system",
                        tag.DisplayText, $"Systems: {string.Join(", ", sem.SystemMembership)}",
                        "Update tag to reflect correct system",
                        $"Element systems: [{string.Join(", ", sem.SystemMembership)}]"));
            }
        }

        private void ValidateFireRating(TagInstance tag, ElementSemantics sem, List<SemanticValidationIssue> issues)
        {
            if (sem == null || !sem.IsFireSafetyElement || string.IsNullOrEmpty(tag.DisplayText)) return;
            if (sem.Importance != ImportanceLevel.Critical) return;
            bool hasFR = Regex.IsMatch(tag.DisplayText, @"(?:FRL|FRR|FR|fire|rated|\d+[/-]\d+[/-]\d+|\d+\s*(?:hr|hour|min))", RegexOptions.IgnoreCase);
            if (!hasFR)
                issues.Add(MakeIssue(SemanticIssueType.FireRatingInconsistency, IssueSeverity.Warning, tag,
                    "Critical fire-safety element tag does not show fire rating", tag.DisplayText,
                    "Include fire rating", "Add fire rating parameter to content expression",
                    "NFPA 80, IBC Chapter 7"));
        }

        private void ValidateRoomNaming(TagInstance tag, List<SemanticValidationIssue> issues)
        {
            if (string.IsNullOrEmpty(tag.DisplayText) || tag.CategoryName?.ToLowerInvariant() != "rooms") return;
            string t = tag.DisplayText.Trim();
            if (!Regex.IsMatch(t, @"\d+") && !Regex.IsMatch(t, @"[A-Za-z]{2,}"))
                issues.Add(MakeIssue(SemanticIssueType.RoomNamingInconsistency, IssueSeverity.Warning, tag,
                    "Room tag has no recognizable name or number", t,
                    "Room name or number", "Verify room parameters are populated", "ISO 19650"));
        }

        private void ValidateEquipmentNaming(TagInstance tag, List<SemanticValidationIssue> issues)
        {
            if (string.IsNullOrEmpty(tag.DisplayText)) return;
            string cl = tag.CategoryName?.ToLowerInvariant() ?? "";
            if (cl != "mechanical equipment" && cl != "electrical equipment") return;
            if (!Regex.IsMatch(tag.DisplayText.Trim(), @"^[A-Z]{2,5}[-_\s]?\d"))
                issues.Add(MakeIssue(SemanticIssueType.EquipmentNamingMismatch, IssueSeverity.Info, tag,
                    "Equipment tag does not follow SYSTEM-NUMBER convention", tag.DisplayText,
                    "ABBREV-NN (e.g., AHU-01)", "Use system abbreviation + number", "ASHRAE conventions"));
        }

        private void ValidatePlausibility(TagInstance tag, List<SemanticValidationIssue> issues)
        {
            if (string.IsNullOrEmpty(tag.DisplayText)) return;
            string t = tag.DisplayText.Trim();
            string[] placeholders = { "???", "TBD", "N/A", "UNKNOWN", "DEFAULT", "SAMPLE", "TEST" };
            if (placeholders.Any(p => string.Equals(t, p, StringComparison.OrdinalIgnoreCase)))
                issues.Add(MakeIssue(SemanticIssueType.ImplausibleValue, IssueSeverity.Warning, tag,
                    $"Tag displays placeholder '{t}'", t, "Actual parameter value",
                    "Update source parameter", "Data quality"));
            if (Regex.IsMatch(t, @"^0+(\.0+)?$"))
                issues.Add(MakeIssue(SemanticIssueType.ImplausibleValue, IssueSeverity.Info, tag,
                    "Tag displays zero value", t, "Non-zero value", "Verify parameter data", "Data quality"));
        }

        private void ValidateMandatoryInfo(TagInstance tag, ElementSemantics sem, List<SemanticValidationIssue> issues)
        {
            if (sem == null || string.IsNullOrEmpty(tag.CategoryName) || string.IsNullOrEmpty(tag.DisplayText)) return;
            if (!ContentProfiles.TryGetValue(tag.CategoryName, out var profile)) return;
            var critical = profile.Where(p => p.I == ContentImportance.Critical).Select(p => p.P).ToList();
            if (critical.Count >= 2 && tag.DisplayText.Trim().Length < 3)
                issues.Add(MakeIssue(SemanticIssueType.MissingMandatoryInfo, IssueSeverity.Warning, tag,
                    $"Tag too short for {tag.CategoryName} with {critical.Count} critical params",
                    tag.DisplayText, $"Include: {string.Join(", ", critical)}",
                    "Expand content expression", $"Profile: {tag.CategoryName}"));
        }

        private SemanticValidationIssue MakeIssue(SemanticIssueType type, IssueSeverity sev,
            TagInstance tag, string desc, string actual, string expected, string fix, string source)
        {
            return new SemanticValidationIssue
            {
                IssueId = Guid.NewGuid().ToString("N"), IssueType = type, Severity = sev,
                TagId = tag.TagId, ElementId = tag.HostElementId, Description = desc,
                ActualValue = actual, ExpectedValue = expected, SuggestedCorrection = fix,
                IsAutoFixable = false, KnowledgeSource = source
            };
        }

        #endregion

        #region Cache Management

        /// <summary>Clears all cached element semantic profiles.</summary>
        public void ClearSemanticsCache()
        {
            lock (_cacheLock) { _semanticsCache.Clear(); }
            Logger.Info("Element semantics cache cleared");
        }

        /// <summary>Invalidates a single cached element profile.</summary>
        public void InvalidateElementCache(int elementId)
        {
            lock (_cacheLock) { _semanticsCache.Remove(elementId); }
        }

        /// <summary>Number of cached element semantic profiles.</summary>
        public int CachedElementCount
        {
            get { lock (_cacheLock) { return _semanticsCache.Count; } }
        }

        #endregion

        #region Utilities

        private static string Param(Dictionary<string, object> p, string key)
        {
            if (p == null) return null;
            if (p.TryGetValue(key, out object v) && v != null)
            { string s = v.ToString(); return string.IsNullOrWhiteSpace(s) ? null : s; }
            foreach (var kvp in p)
                if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase) && kvp.Value != null)
                { string s = kvp.Value.ToString(); return string.IsNullOrWhiteSpace(s) ? null : s; }
            return null;
        }

        private static bool HasFireRating(Dictionary<string, object> p)
        {
            string fr = Param(p, "Fire_Rating") ?? Param(p, "Fire Rating") ?? Param(p, "FireRating");
            if (string.IsNullOrEmpty(fr)) return false;
            string u = fr.ToUpperInvariant().Trim();
            return u != "0" && u != "NONE" && u != "N/A" && u != "-";
        }

        private static bool ContainsAny(string text, string[] keywords)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (string kw in keywords)
                if (text.Contains(kw, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        #endregion
    }
}
