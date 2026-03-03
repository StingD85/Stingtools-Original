// StingBIM.AI.NLP.Pipeline.IntentClassifier
// Classifies user intent from natural language input
// Master Proposal Reference: Part 1.1 Language Understanding - Intent Classifier

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NLog;
using StingBIM.AI.Core.Models;

namespace StingBIM.AI.NLP.Pipeline
{
    /// <summary>
    /// Classifies user intents from natural language commands.
    /// Combines rule-based matching with ML-based classification.
    /// Target: < 200ms command understanding (Part 5.2)
    /// </summary>
    public class IntentClassifier
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly EmbeddingModel _embeddingModel;
        private readonly Tokenizer _tokenizer;

        private Dictionary<string, IntentDefinition> _intents;
        private Dictionary<string, float[]> _intentEmbeddings;
        private List<IntentPattern> _patterns;
        private bool _isLoaded;

        // Classification thresholds
        public float ConfidenceThreshold { get; set; } = 0.6f;
        public float AmbiguityThreshold { get; set; } = 0.15f;

        public IntentClassifier(EmbeddingModel embeddingModel, Tokenizer tokenizer)
        {
            _embeddingModel = embeddingModel;
            _tokenizer = tokenizer;
            _intents = new Dictionary<string, IntentDefinition>(StringComparer.OrdinalIgnoreCase);
            _intentEmbeddings = new Dictionary<string, float[]>();
            _patterns = new List<IntentPattern>();

            // Register built-in patterns so classification works without LoadAsync
            RegisterBuiltInPatterns();
        }

        private void RegisterBuiltInPatterns()
        {
            var builtIn = new[]
            {
                // Design commands
                ("create.*wall", "CREATE_WALL", 0.85f, 10),
                ("add.*wall", "CREATE_WALL", 0.80f, 9),
                ("create.*room|add.*room", "CREATE_ROOM", 0.85f, 10),
                ("create.*bedroom|add.*bedroom", "CREATE_ROOM", 0.85f, 10),
                ("create.*kitchen|add.*kitchen", "CREATE_ROOM", 0.85f, 10),
                ("create.*bathroom|add.*bathroom", "CREATE_ROOM", 0.85f, 10),
                ("create.*living", "CREATE_ROOM", 0.85f, 10),
                ("add.*door|place.*door|create.*door", "CREATE_DOOR", 0.85f, 10),
                ("add.*window|place.*window|create.*window", "CREATE_WINDOW", 0.85f, 10),
                ("create.*floor|add.*floor|place.*floor", "CREATE_FLOOR", 0.85f, 10),
                ("add.*slab|create.*slab|place.*slab", "CREATE_FLOOR", 0.85f, 10),
                ("floor.*slab|concrete.*slab|ground.*slab", "CREATE_FLOOR", 0.80f, 9),
                ("create.*house|build.*house|design.*house", "CREATE_HOUSE", 0.85f, 10),
                ("create.*building|design.*building|build.*building", "CREATE_HOUSE", 0.85f, 10),
                ("create.*apartment|design.*apartment|build.*flat", "CREATE_HOUSE", 0.85f, 10),
                ("create.*office|add.*office", "CREATE_ROOM", 0.85f, 10),

                // Phase 2: Structural + remaining architectural
                ("add.*roof|create.*roof|place.*roof", "CREATE_ROOF", 0.85f, 10),
                ("pitched.*roof|flat.*roof|hip.*roof|gable.*roof|corrugated.*roof", "CREATE_ROOF", 0.85f, 10),
                ("iron.*sheet.*roof|aluminium.*roof|clay.*tile.*roof|thatch.*roof", "CREATE_ROOF", 0.80f, 9),
                ("add.*ceiling|create.*ceiling|place.*ceiling", "CREATE_CEILING", 0.85f, 10),
                ("plasterboard.*ceiling|suspended.*ceiling|drop.*ceiling|timber.*ceiling", "CREATE_CEILING", 0.80f, 9),
                ("add.*stair|create.*stair|place.*stair|build.*stair", "CREATE_STAIRCASE", 0.85f, 10),
                ("staircase|spiral.*stair|dog.*leg|fire.*escape.*stair", "CREATE_STAIRCASE", 0.80f, 9),
                ("add.*column|place.*column|create.*column", "CREATE_COLUMN", 0.85f, 10),
                ("column.*grid|structural.*column|concrete.*column", "CREATE_COLUMN", 0.80f, 9),
                ("add.*beam|create.*beam|place.*beam", "CREATE_BEAM", 0.85f, 10),
                ("structural.*beam|concrete.*beam|steel.*beam|lintel", "CREATE_BEAM", 0.80f, 9),
                ("add.*foundation|create.*foundation|place.*foundation", "CREATE_FOUNDATION", 0.85f, 10),
                ("strip.*foundation|pad.*foundation|raft.*foundation|pile.*foundation", "CREATE_FOUNDATION", 0.85f, 10),
                ("footing|ground.*beam|substructure", "CREATE_FOUNDATION", 0.80f, 9),
                ("add.*ramp|create.*ramp|wheelchair.*ramp|accessible.*ramp", "CREATE_RAMP", 0.85f, 10),
                ("add.*railing|create.*railing|place.*railing|add.*balustrade|handrail", "CREATE_RAILING", 0.85f, 10),
                ("curtain.*wall|glass.*wall|glazed.*wall|add.*curtain", "CREATE_CURTAIN_WALL", 0.85f, 10),
                ("add.*parapet|create.*parapet|parapet.*wall", "CREATE_PARAPET", 0.85f, 10),

                // Phase 3: Electrical MEP
                ("add.*light|place.*light|install.*light|create.*light", "CREATE_LIGHTING", 0.85f, 10),
                ("led.*light|downlight|recessed.*light|pendant.*light|fluorescent", "CREATE_LIGHTING", 0.85f, 10),
                ("light.*room|light.*bedroom|light.*kitchen|light.*office", "CREATE_LIGHTING", 0.80f, 9),
                ("emergency.*light|security.*light|floodlight|outdoor.*light", "CREATE_LIGHTING", 0.80f, 9),
                ("add.*outlet|place.*outlet|power.*outlet|power.*point|socket", "CREATE_OUTLET", 0.85f, 10),
                ("worktop.*outlet|kitchen.*outlet|waterproof.*outlet|external.*outlet", "CREATE_OUTLET", 0.80f, 9),
                ("add.*switch|place.*switch|light.*switch|dimmer", "CREATE_SWITCH", 0.85f, 10),
                ("two.*way.*switch|2.*way.*switch|fan.*controller", "CREATE_SWITCH", 0.80f, 9),
                ("distribution.*board|db\\b|consumer.*unit|add.*db|place.*db", "CREATE_DB", 0.85f, 10),
                ("electrical.*panel|circuit.*board|breaker.*board|mcb", "CREATE_DB", 0.80f, 9),
                ("add.*generator|place.*generator|standby.*power|backup.*power", "CREATE_GENERATOR", 0.85f, 10),
                ("diesel.*generator|genset", "CREATE_GENERATOR", 0.80f, 9),
                ("route.*conduit|add.*conduit|create.*conduit|conduit.*run", "CREATE_CONDUIT", 0.85f, 10),
                ("cable.*tray|add.*cable.*tray|cable.*ladder|trunking|dado.*trunking", "CREATE_CABLE_TRAY", 0.85f, 10),
                ("flexible.*conduit|rigid.*conduit|pvc.*conduit|steel.*conduit", "CREATE_CONDUIT", 0.80f, 9),
                ("wire.*mesh.*tray|perforated.*tray|ladder.*tray", "CREATE_CABLE_TRAY", 0.80f, 9),
                ("solar.*panel|solar.*pv|add.*solar|install.*solar", "CREATE_SOLAR", 0.80f, 9),
                ("ev.*charg|electric.*vehicle.*charg", "CREATE_EV_CHARGER", 0.80f, 9),

                // Phase 4: HVAC, Plumbing, Fire Protection
                ("add.*split.*ac|add.*air.*con|install.*ac|place.*ac.*unit", "CREATE_HVAC_AC", 0.85f, 10),
                ("split.*ac|air.*condition|cooling.*unit|cassette.*unit", "CREATE_HVAC_AC", 0.80f, 9),
                ("add.*ceiling.*fan|place.*ceiling.*fan|install.*fan", "CREATE_HVAC_FAN", 0.85f, 10),
                ("ceiling.*fan|overhead.*fan", "CREATE_HVAC_FAN", 0.80f, 9),
                ("add.*extract.*fan|bathroom.*fan|exhaust.*fan|ventilation.*fan", "CREATE_HVAC_EXTRACT", 0.85f, 10),
                ("add.*kitchen.*hood|range.*hood|cooker.*hood|extract.*hood", "CREATE_HVAC_HOOD", 0.85f, 10),
                ("add.*plumbing|plumbing.*fixture|sanitary.*fixture|toilet|basin|sink", "CREATE_PLUMBING", 0.85f, 10),
                ("route.*cold.*water|cold.*water.*pipe|water.*supply", "CREATE_PLUMBING_CW", 0.85f, 10),
                ("route.*waste|waste.*pipe|soil.*pipe|drainage.*pipe", "CREATE_PLUMBING_WASTE", 0.85f, 10),
                ("rainwater|downpipe|gutter|roof.*drainage|storm.*drain", "CREATE_PLUMBING_RAIN", 0.80f, 9),
                ("septic.*tank|soakaway|borehole|water.*tank|harvesting", "CREATE_PLUMBING", 0.80f, 9),
                ("smoke.*detect|fire.*detect|heat.*detect|add.*detect", "CREATE_FIRE_DETECTOR", 0.85f, 10),
                ("add.*sprinkler|fire.*sprinkler|sprinkler.*head", "CREATE_FIRE_SPRINKLER", 0.85f, 10),
                ("fire.*hose|hose.*reel", "CREATE_FIRE_HOSE", 0.85f, 10),
                ("fire.*extinguish|extinguisher|abc.*powder|co2.*extinguish", "CREATE_FIRE_EXTINGUISHER", 0.85f, 10),
                ("manual.*call.*point|fire.*alarm|alarm.*sounder|call.*point", "CREATE_FIRE_ALARM", 0.85f, 10),

                // Phase 5: Modification Engine
                ("move.*(?:wall|door|window|column|beam|element|room)|reposition|shift.*(?:wall|element)", "MOVE_ELEMENT", 0.85f, 10),
                ("move.*(?:north|south|east|west|left|right|up|down)", "MOVE_ELEMENT", 0.85f, 10),
                ("delete.*(?:wall|door|window|column|beam|element|room)|remove.*(?:wall|door|element)|erase", "DELETE_ELEMENT", 0.85f, 10),
                ("copy.*(?:wall|door|window|column|beam|element|level)|duplicate|clone", "COPY_ELEMENT", 0.85f, 10),
                ("copy.*level.*to|copy.*layout.*to", "COPY_ELEMENT", 0.90f, 11),
                ("rotate.*(?:wall|door|window|column|element)|rotate.*(?:deg|°)", "ROTATE_ELEMENT", 0.85f, 10),
                ("mirror.*(?:wall|floor|plan|element|half)", "MIRROR_ELEMENT", 0.85f, 10),
                ("resize|change.*size|modify.*dimension|set.*dimension", "RESIZE_ELEMENT", 0.85f, 10),
                ("resize.*(?:room|bedroom|kitchen|living)|(?:make|increase|decrease).*(?:bigger|smaller|larger|wider|longer|shorter)", "RESIZE_ELEMENT", 0.85f, 10),
                ("change.*(?:wall|door|window).*(?:to|type)|upgrade.*(?:door|window|wall)|swap.*type", "CHANGE_TYPE", 0.85f, 10),
                ("change.*(?:to|type).*(?:brick|timber|steel|concrete|glass|fire.rated)", "CHANGE_TYPE", 0.85f, 10),
                ("set.*parameter|change.*parameter", "SET_PARAMETER", 0.85f, 10),
                ("set.*(?:fire.*rating|mark|number|name).*(?:to|=)|fire.*rating.*(?:to|=)", "SET_PARAMETER", 0.85f, 10),
                ("number.*(?:door|window|room).*(?:sequen|by.*level)|renumber", "RENUMBER_ELEMENT", 0.85f, 10),
                ("split.*wall|split.*at|break.*wall", "SPLIT_ELEMENT", 0.85f, 10),
                ("extend.*(?:beam|wall|pipe)|extend.*to.*(?:column|wall)", "EXTEND_ELEMENT", 0.85f, 10),
                ("offset.*(?:wall|pipe|duct|element)", "OFFSET_ELEMENT", 0.85f, 10),
                ("raise.*level|lower.*level|adjust.*level.*elevation|level.*(?:up|down)", "LEVEL_ADJUST", 0.85f, 10),
                ("pin.*(?:element|wall|column)|lock.*element", "PIN_ELEMENT", 0.80f, 8),
                ("unpin.*(?:element|wall|column)|unlock.*element", "UNPIN_ELEMENT", 0.80f, 8),

                // Phase 5: Bulk Operations
                ("array.*(?:column|beam|wall|element)|repeat.*(?:column|element).*spacing", "ARRAY_ELEMENT", 0.85f, 10),
                ("align.*(?:window|door|element|wall)|same.*(?:sill|head).*height", "ALIGN_ELEMENT", 0.85f, 10),
                ("distribute.*(?:column|element|evenly)|space.*(?:evenly|equally)", "DISTRIBUTE_ELEMENT", 0.85f, 10),
                ("purge|remove.*unused|clean.*unused|delete.*unused.*(?:famil|type)", "PURGE_UNUSED", 0.85f, 10),
                ("value.*engineer|cost.*saving|downgrade.*save|cheaper.*alternative", "VALUE_ENGINEER", 0.85f, 10),
                ("tag.*(?:all|room|door|window)|auto.*tag|add.*tag|add.*mark", "AUTO_TAG", 0.85f, 10),
                ("what.*area|calculate.*area|how.*big|total.*area|area.*total|floor.*area|measure.*area", "QUERY_AREA", 0.80f, 8),
                ("check.*compli|compli.*check|code.*compli|fire.*code|is.*compli|verify.*standard|compliant|validate.*design", "CHECK_COMPLIANCE", 0.85f, 9),
                ("standard.*check|check.*standard|meet.*standard|against.*standard", "CHECK_COMPLIANCE", 0.80f, 8),
                ("fire.*safe|egress|accessibility.*check|structural.*check", "CHECK_COMPLIANCE", 0.80f, 8),

                // Model queries
                ("how many|count|number of|total.*room|total.*wall|total.*element", "QUERY_MODEL", 0.80f, 8),
                ("review.*model|model.*review|analyze.*model|model.*status|model.*health", "QUERY_MODEL", 0.85f, 9),
                ("review the model|check the model|inspect.*model", "QUERY_MODEL", 0.85f, 9),
                ("what.*room|which.*room|room.*list|room.*info|rooms in", "QUERY_MODEL", 0.80f, 8),
                ("what.*level|which.*level|floor.*plan", "QUERY_MODEL", 0.80f, 8),
                ("model.*info|project.*info|building.*info|summary|model.*summary", "QUERY_MODEL", 0.80f, 8),
                ("element.*count|wall.*count|door.*count", "QUERY_MODEL", 0.80f, 8),
                ("list.*furniture|furniture.*list|show.*furniture|what.*furniture", "QUERY_MODEL", 0.85f, 9),
                ("list.*wall|list.*door|list.*window|list.*floor|list.*column", "QUERY_MODEL", 0.85f, 9),
                ("list.*element|list.*all|show.*all.*element|all.*element", "QUERY_MODEL", 0.85f, 9),
                ("what.*in.*model|what.*in.*floor.*plan|what.*in.*project", "QUERY_MODEL", 0.85f, 9),
                ("show.*model|model.*overview|project.*overview", "QUERY_MODEL", 0.80f, 8),

                // General / conversational
                ("hello|hi|hey|good morning|good afternoon|good evening", "GREETING", 0.90f, 10),
                ("help|what can you do|how do i", "HELP", 0.90f, 10),
                ("undo|go back|revert", "UNDO", 0.90f, 10),
                ("redo", "REDO", 0.90f, 10),
                ("thank|thanks|cheers|appreciate", "GREETING", 0.85f, 9),
                ("bye|goodbye|see you|good night", "GREETING", 0.85f, 9),

                // Informational queries
                ("what is|what's|what are|define|explain|tell me about|describe", "INFORMATION", 0.85f, 9),
                ("how does|how do|how to|how can", "INFORMATION", 0.85f, 9),
                ("why is|why does|why do|why should", "INFORMATION", 0.85f, 9),
                ("give me.*standard|give me.*bim|give me.*iso|give me.*info", "INFORMATION", 0.85f, 9),
                ("about bim|about revit|about iso|about ifc|about lod|about cde", "INFORMATION", 0.85f, 9),
                ("bep|bim execution plan|common data environment", "INFORMATION", 0.85f, 9),
                ("what.*standard|which.*standard|applicable.*standard|major.*standard", "INFORMATION", 0.85f, 9),
                ("bim.*standard|iso.*standard|building.*standard|international.*standard", "INFORMATION", 0.85f, 9),
                ("bim.*process|bim.*workflow|process.*bim|workflow.*bim|bim.*methodology", "INFORMATION", 0.85f, 9),
                ("bim.*lifecycle|bim.*stage|bim.*phase|project.*lifecycle", "INFORMATION", 0.85f, 9),
                ("clash detect|4d bim|5d bim|6d bim|7d bim|digital twin", "INFORMATION", 0.85f, 9),
                ("parametric|generative design|quantity takeoff", "INFORMATION", 0.80f, 8),
                ("shared parameter|mep\\b|facility manage", "INFORMATION", 0.80f, 7),

                // BOQ, Material Takeoff, Materials, Parameters
                ("bill.*quantit|boq\\b|quantity.*survey|generate.*boq|produce.*boq", "GENERATE_BOQ", 0.90f, 10),
                ("material.*takeoff|material.*take.off|takeoff|take.off.*material", "MATERIAL_TAKEOFF", 0.90f, 10),
                ("what.*material|which.*material|material.*list|material.*used|show.*material|list.*material", "QUERY_MATERIALS", 0.85f, 9),
                ("material.*quantit|quantit.*material|how much.*material", "MATERIAL_TAKEOFF", 0.85f, 9),
                ("material.*info|material.*detail|material.*propert|material.*data", "QUERY_MATERIALS", 0.80f, 8),
                ("parameter.*value|show.*parameter|list.*parameter|what.*parameter|element.*parameter", "QUERY_PARAMETERS", 0.85f, 9),
                ("parameter.*detail|parameter.*info|get.*parameter", "QUERY_PARAMETERS", 0.80f, 8),
                ("cost.*estimat|estimat.*cost|project.*cost|construction.*cost", "GENERATE_BOQ", 0.80f, 8),
                ("quantities|quantit.*report|quantit.*schedule", "GENERATE_BOQ", 0.80f, 8),

                // Facilities Management / Maintenance
                ("maintenance.*schedule|generate.*maintenance|maintenance.*plan", "GENERATE_MAINTENANCE_SCHEDULE", 0.85f, 10),
                ("predict.*failure|equipment.*failure|failure.*risk|failure.*predict", "PREDICT_FAILURES", 0.85f, 10),
                ("maintenance.*cost|optimize.*maintenance|maintenance.*strategy|maintenance.*budget", "OPTIMIZE_MAINTENANCE", 0.80f, 8),
                ("equipment.*condition|failure.*analysis|failure.*pattern|equipment.*health", "ANALYZE_FAILURES", 0.80f, 8),
                ("facilities|facility.*manage|fm\\b|building.*operation", "FM_GENERAL", 0.80f, 8),
                ("replacement.*cycle|spare.*part|asset.*lifecycle", "FM_GENERAL", 0.80f, 8),

                // Collaboration / Multi-agent
                ("negotiate|consensus|discuss.*design|design.*discussion", "NEGOTIATE_DESIGN", 0.85f, 10),
                ("agent.*recommend|specialist.*opinion|what.*agent.*think", "GET_RECOMMENDATIONS", 0.80f, 8),
                ("design.*conflict|disagreement|resolve.*design.*conflict", "RESOLVE_CONFLICT", 0.80f, 8),
                ("collaborate|coordination|cross.*discipline|team.*review", "COLLABORATE", 0.80f, 8),

                // Phase 6: LAN Collaboration
                ("setup.*workshar|enable.*workshar|start.*workshar|initialise.*workshar|initialize.*workshar", "SETUP_WORKSHARING", 0.90f, 11),
                ("create.*central.*model|save.*as.*central|make.*central", "SETUP_WORKSHARING", 0.85f, 10),
                ("sync.*(?:to|with).*central|synchroni[sz]e|sync.*model|stc\\b", "SYNC_MODEL", 0.90f, 11),
                ("reload.*latest|get.*latest|pull.*latest|update.*from.*central", "SYNC_MODEL", 0.85f, 10),
                ("check.*(?:worksharing|sync).*conflict|conflict.*check|pre.?sync.*check", "CHECK_WORKSHARING_CONFLICTS", 0.85f, 10),
                ("who.*(?:owns?|has|checked.*out|editing).*(?:wall|door|element|beam|column|room)", "DIAGNOSE_EDIT", 0.85f, 10),
                ("why.*can.?t.*(?:i|I).*edit|can.?t.*edit|unable.*edit|locked.*element", "DIAGNOSE_EDIT", 0.90f, 11),
                ("edit.*status|checkout.*status|element.*ownership", "DIAGNOSE_EDIT", 0.80f, 9),
                ("generate.*bep|create.*bep|bim.*execution.*plan|produce.*bep", "GENERATE_BEP", 0.90f, 11),
                ("iso.*19650.*plan|information.*management.*plan|bep\\b", "GENERATE_BEP", 0.85f, 10),
                ("model.*health|health.*check|check.*model.*health|worksharing.*health", "MODEL_HEALTH_CHECK", 0.85f, 10),
                ("show.*changelog|view.*changelog|recent.*changes|what.*changed|change.*log", "VIEW_CHANGELOG", 0.85f, 10),
                ("who.*synced|last.*sync|sync.*history|activity.*log", "VIEW_CHANGELOG", 0.80f, 9),
                ("who.*online|team.*status|team.*member|who.*working", "VIEW_TEAM", 0.85f, 10),
                ("create.*backup|backup.*model|manual.*backup|save.*backup", "CREATE_BACKUP", 0.85f, 10),
                ("restore.*backup|restore.*from|recover.*model|rollback.*model", "RESTORE_BACKUP", 0.85f, 10),
                ("list.*backup|show.*backup|available.*backup", "LIST_BACKUPS", 0.85f, 10),
                ("start.*auto.?sync|enable.*auto.?sync|auto.?sync.*on", "START_AUTOSYNC", 0.85f, 10),
                ("stop.*auto.?sync|disable.*auto.?sync|auto.?sync.*off", "STOP_AUTOSYNC", 0.85f, 10),
                ("start.*auto.?backup|enable.*auto.?backup|auto.?backup.*on", "START_AUTOBACKUP", 0.85f, 10),
                ("stop.*auto.?backup|disable.*auto.?backup|auto.?backup.*off", "STOP_AUTOBACKUP", 0.85f, 10),
                ("relinquish|release.*element|give.*up.*element|release.*ownership", "RELINQUISH_ELEMENT", 0.85f, 10),
                ("export.*changelog|changelog.*csv|changelog.*export", "EXPORT_CHANGELOG", 0.80f, 9),

                // Phase 7: Budget Design + Exports
                ("budget.*design|design.*budget|budget.*option|design.*within.*budget", "BUDGET_DESIGN", 0.90f, 11),
                ("how.*much.*cost|what.*cost|estimat.*(?:cost|price)|cost.*estimat|price.*estimat", "ESTIMATE_COST", 0.85f, 10),
                ("budget.*alert|budget.*check|check.*budget|over.*budget|under.*budget", "CHECK_BUDGET", 0.85f, 10),
                ("export.*boq|boq.*(?:excel|csv|export)|export.*bill|download.*boq", "EXPORT_BOQ", 0.90f, 11),
                ("export.*cobie|cobie.*export|fm.*handover|cobie.*2\\.4|handover.*data", "EXPORT_COBIE", 0.90f, 11),
                ("export.*room.*schedule|room.*schedule.*(?:csv|excel|export)", "EXPORT_ROOM_SCHEDULE", 0.85f, 10),
                ("export.*door.*schedule|door.*schedule.*(?:csv|excel|export)", "EXPORT_DOOR_SCHEDULE", 0.85f, 10),
                ("export.*window.*schedule|window.*schedule.*(?:csv|excel|export)", "EXPORT_WINDOW_SCHEDULE", 0.85f, 10),
                ("import.*parameter|parameter.*import|load.*parameter.*(?:csv|file)", "IMPORT_PARAMETERS", 0.85f, 10),
                ("value.*engineer|cost.*saving|cheaper.*alternative|reduce.*cost", "VALUE_ENGINEER_BUDGET", 0.85f, 10),
                ("cost.*per.*(?:m2|meter|sqm|square)|rate.*per.*m2", "ESTIMATE_COST", 0.80f, 9),
                ("compare.*(?:cost|price|budget|option)", "BUDGET_DESIGN", 0.80f, 9),

                // Phase 8: Specialist Systems + Proactive Intelligence
                ("add.*data.*outlet|data.*point|network.*point|rj45", "CREATE_DATA_OUTLET", 0.85f, 10),
                ("structured.*cabling|data.*cabling|cat6|cat.*cable", "CREATE_DATA_OUTLET", 0.80f, 9),
                ("wifi.*access.*point|wifi.*ap|wireless.*access|place.*wifi", "CREATE_WIFI_AP", 0.85f, 10),
                ("wifi.*coverage|wireless.*coverage", "CREATE_WIFI_AP", 0.80f, 9),
                ("server.*room|comms.*room|data.*centre|data.*center|rack.*layout", "CREATE_SERVER_ROOM", 0.85f, 10),
                ("add.*cctv|place.*cctv|install.*cctv|security.*camera|surveillance", "CREATE_CCTV", 0.85f, 10),
                ("cctv.*all.*entr|camera.*entrance|camera.*entry", "CREATE_CCTV", 0.80f, 9),
                ("access.*control|card.*reader|biometric|door.*access", "CREATE_ACCESS_CONTROL", 0.85f, 10),
                ("intruder.*alarm|alarm.*system|burglar.*alarm|pir.*sensor|motion.*detect", "CREATE_ALARM_SYSTEM", 0.85f, 10),
                ("intercom|door.*entry|door.*bell|video.*entry", "CREATE_INTERCOM", 0.85f, 10),
                ("gas.*pip|gas.*line|gas.*supply|route.*gas|lpg.*pip", "CREATE_GAS_PIPING", 0.85f, 10),
                ("gas.*detect|gas.*sensor|gas.*alarm|lpg.*detect", "CREATE_GAS_DETECTOR", 0.85f, 10),
                ("gas.*meter|gas.*shut.*off|gas.*valve|gas.*regulat", "CREATE_GAS_PIPING", 0.80f, 9),
                ("design.*solar|solar.*system|solar.*array|pv.*system|solar.*size", "CREATE_SOLAR", 0.90f, 11),
                ("ev.*ready|ev.*infrastructure|charger.*install|charging.*station", "CREATE_EV_CHARGER", 0.90f, 11),
                ("ev.*parking|charging.*bay|electric.*car", "CREATE_EV_CHARGER", 0.80f, 9),
                ("proactive.*advice|design.*suggest|suggest.*improv|tips|advise", "GET_DESIGN_ADVICE", 0.85f, 10),
                ("model.*audit|audit.*model|full.*audit|run.*audit", "RUN_MODEL_AUDIT", 0.85f, 10),
                ("uganda.*compliance|unbs.*check|kcca.*check|building.*regulation", "CHECK_UGANDA_COMPLIANCE", 0.90f, 11),
                ("check.*fire.*safety|fire.*compliance|fire.*exit.*check", "CHECK_UGANDA_COMPLIANCE", 0.85f, 10),
                ("check.*accessibility|accessibility.*audit|pwd.*compliance|disability.*access", "CHECK_UGANDA_COMPLIANCE", 0.85f, 10),
                ("set.*budget|project.*budget|budget.*is|budget.*of", "SET_BUDGET", 0.85f, 10),
            };

            foreach (var (pattern, intent, confidence, priority) in builtIn)
            {
                _patterns.Add(new IntentPattern
                {
                    Pattern = pattern,
                    IntentName = intent,
                    Confidence = confidence,
                    Priority = priority,
                    Type = PatternType.Regex
                });
            }
        }

        /// <summary>
        /// Loads intent definitions and patterns.
        /// </summary>
        public async Task LoadAsync(string intentsPath, string patternsPath = null)
        {
            Logger.Info("Loading intent classifier...");

            try
            {
                // Load intent definitions
                if (File.Exists(intentsPath))
                {
                    var json = await Task.Run(() => File.ReadAllText(intentsPath));
                    var intents = JsonConvert.DeserializeObject<List<IntentDefinition>>(json);
                    _intents = intents.ToDictionary(i => i.Name, StringComparer.OrdinalIgnoreCase);
                }

                // Load patterns if provided
                if (!string.IsNullOrEmpty(patternsPath) && File.Exists(patternsPath))
                {
                    var json = await Task.Run(() => File.ReadAllText(patternsPath));
                    _patterns = JsonConvert.DeserializeObject<List<IntentPattern>>(json) ?? new List<IntentPattern>();
                }

                // Pre-compute intent embeddings for similarity matching
                await ComputeIntentEmbeddingsAsync();

                _isLoaded = true;
                Logger.Info($"Intent classifier loaded: {_intents.Count} intents, {_patterns.Count} patterns");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to load intent classifier");
                throw;
            }
        }

        /// <summary>
        /// Classifies the intent of a user command.
        /// </summary>
        public async Task<IntentClassificationResult> ClassifyAsync(string text)
        {
            EnsureLoaded();

            var startTime = DateTime.Now;

            // First, try rule-based pattern matching (faster)
            var patternMatch = TryPatternMatch(text);
            if (patternMatch != null && patternMatch.Confidence >= ConfidenceThreshold)
            {
                patternMatch.ProcessingTimeMs = (DateTime.Now - startTime).TotalMilliseconds;
                Logger.Debug($"Intent classified by pattern: {patternMatch.Intent} ({patternMatch.Confidence:P0})");
                return patternMatch;
            }

            // Fall back to semantic similarity matching (only if model is loaded)
            IntentClassificationResult semanticResult = null;
            if (_isLoaded && _intentEmbeddings.Count > 0)
            {
                semanticResult = await ClassifyBySemanticSimilarityAsync(text);
            }

            // Combine results if both have matches
            if (patternMatch != null && semanticResult != null)
            {
                // If same intent, boost confidence
                if (patternMatch.Intent == semanticResult.Intent)
                {
                    semanticResult.Confidence = Math.Min(0.99f, semanticResult.Confidence + 0.1f);
                }
                // If different, check for ambiguity
                else if (Math.Abs(patternMatch.Confidence - semanticResult.Confidence) < AmbiguityThreshold)
                {
                    semanticResult.IsAmbiguous = true;
                    semanticResult.AlternativeIntents.Add(patternMatch.Intent);
                }
            }

            // Use best available result, or return unknown
            var result = semanticResult ?? patternMatch ?? new IntentClassificationResult
            {
                Intent = "UNKNOWN",
                Confidence = 0.3f,
                Source = ClassificationSource.Pattern
            };

            result.ProcessingTimeMs = (DateTime.Now - startTime).TotalMilliseconds;
            Logger.Debug($"Intent classified: {result.Intent} ({result.Confidence:P0}) in {result.ProcessingTimeMs:F0}ms");

            return result;
        }

        /// <summary>
        /// Classifies the intent of a user command with cancellation support.
        /// </summary>
        public async Task<IntentClassificationResult> ClassifyAsync(string text, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await ClassifyAsync(text);
        }

        /// <summary>
        /// Tries to match input against predefined patterns.
        /// </summary>
        private IntentClassificationResult TryPatternMatch(string text)
        {
            var normalizedText = text.ToLowerInvariant().Trim();

            foreach (var pattern in _patterns.OrderByDescending(p => p.Priority))
            {
                if (pattern.Matches(normalizedText))
                {
                    return new IntentClassificationResult
                    {
                        Intent = pattern.IntentName,
                        Confidence = pattern.Confidence,
                        MatchedPattern = pattern.Pattern,
                        Source = ClassificationSource.Pattern
                    };
                }
            }

            return null;
        }

        /// <summary>
        /// Classifies using semantic similarity with intent examples.
        /// </summary>
        private async Task<IntentClassificationResult> ClassifyBySemanticSimilarityAsync(string text)
        {
            // Get embedding for input text
            var tokens = _tokenizer.Encode(text);
            var attention = _tokenizer.CreateAttentionMask(tokens);
            var inputEmbedding = await _embeddingModel.GetEmbeddingAsync(tokens, attention);

            // Find most similar intent
            var similarities = new List<(string Intent, float Similarity)>();

            foreach (var (intentName, embedding) in _intentEmbeddings)
            {
                var similarity = EmbeddingModel.CosineSimilarity(inputEmbedding, embedding);
                similarities.Add((intentName, similarity));
            }

            var sorted = similarities.OrderByDescending(s => s.Similarity).ToList();
            var best = sorted.FirstOrDefault();

            var result = new IntentClassificationResult
            {
                Intent = best.Intent,
                Confidence = best.Similarity,
                Source = ClassificationSource.Semantic,
                AlternativeIntents = sorted.Skip(1)
                    .Take(3)
                    .Where(s => s.Similarity >= ConfidenceThreshold * 0.7f)
                    .Select(s => s.Intent)
                    .ToList()
            };

            // Check for ambiguity
            if (sorted.Count > 1)
            {
                var second = sorted[1];
                if (best.Similarity - second.Similarity < AmbiguityThreshold)
                {
                    result.IsAmbiguous = true;
                }
            }

            return result;
        }

        /// <summary>
        /// Pre-computes embeddings for intent examples.
        /// </summary>
        private async Task ComputeIntentEmbeddingsAsync()
        {
            _intentEmbeddings.Clear();

            foreach (var (intentName, intent) in _intents)
            {
                if (intent.Examples == null || intent.Examples.Count == 0) continue;

                // Compute average embedding across all examples
                var embeddings = new List<float[]>();

                foreach (var example in intent.Examples)
                {
                    var tokens = _tokenizer.Encode(example);
                    var attention = _tokenizer.CreateAttentionMask(tokens);
                    var embedding = await _embeddingModel.GetEmbeddingAsync(tokens, attention);
                    embeddings.Add(embedding);
                }

                // Average the embeddings
                var avgEmbedding = AverageEmbeddings(embeddings);
                _intentEmbeddings[intentName] = avgEmbedding;
            }
        }

        private float[] AverageEmbeddings(List<float[]> embeddings)
        {
            if (embeddings.Count == 0) return Array.Empty<float>();

            var dim = embeddings[0].Length;
            var result = new float[dim];

            foreach (var embedding in embeddings)
            {
                for (int i = 0; i < dim; i++)
                {
                    result[i] += embedding[i];
                }
            }

            for (int i = 0; i < dim; i++)
            {
                result[i] /= embeddings.Count;
            }

            // Normalize
            var norm = (float)Math.Sqrt(result.Sum(x => x * x));
            for (int i = 0; i < dim; i++)
            {
                result[i] /= norm;
            }

            return result;
        }

        private void EnsureLoaded()
        {
            if (!_isLoaded)
            {
                Logger.Debug("Intent classifier not fully loaded; using built-in patterns only.");
            }
        }
    }

    /// <summary>
    /// Result of intent classification.
    /// </summary>
    public class IntentClassificationResult
    {
        public string Intent { get; set; }
        public float Confidence { get; set; }
        public bool IsAmbiguous { get; set; }
        public List<string> AlternativeIntents { get; set; } = new List<string>();
        public string MatchedPattern { get; set; }
        public ClassificationSource Source { get; set; }
        public double ProcessingTimeMs { get; set; }
    }

    public enum ClassificationSource
    {
        Pattern,
        Semantic,
        Combined
    }

    /// <summary>
    /// Definition of an intent with examples.
    /// </summary>
    public class IntentDefinition
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public List<string> Examples { get; set; } = new List<string>();
        public List<string> RequiredEntities { get; set; } = new List<string>();
        public List<string> OptionalEntities { get; set; } = new List<string>();
    }

    /// <summary>
    /// Pattern-based intent matching rule.
    /// </summary>
    public class IntentPattern
    {
        public string IntentName { get; set; }
        public string Pattern { get; set; }
        public PatternType Type { get; set; } = PatternType.Contains;
        public float Confidence { get; set; } = 0.9f;
        public int Priority { get; set; } = 0;

        public bool Matches(string text)
        {
            switch (Type)
            {
                case PatternType.Exact:
                    return text.Equals(Pattern, StringComparison.OrdinalIgnoreCase);
                case PatternType.StartsWith:
                    return text.StartsWith(Pattern, StringComparison.OrdinalIgnoreCase);
                case PatternType.Contains:
                    return text.Contains(Pattern, StringComparison.OrdinalIgnoreCase);
                case PatternType.Regex:
                    return System.Text.RegularExpressions.Regex.IsMatch(text, Pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                default:
                    return false;
            }
        }
    }

    public enum PatternType
    {
        Exact,
        StartsWith,
        Contains,
        Regex
    }
}
