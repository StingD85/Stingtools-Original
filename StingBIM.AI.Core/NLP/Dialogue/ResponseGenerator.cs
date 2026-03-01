// StingBIM.AI.NLP.Dialogue.ResponseGenerator
// Generates natural language responses for user interactions
// Master Proposal Reference: Part 1.1 Response Generation (Action Selector, Explanation Generator, Response Formatter)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using StingBIM.AI.NLP.Domain;

namespace StingBIM.AI.NLP.Dialogue
{
    /// <summary>
    /// Generates natural language responses for the AI assistant.
    /// Provides confirmations, explanations, suggestions, and error messages.
    /// </summary>
    public class ResponseGenerator
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        // Response templates organized by type
        private readonly Dictionary<string, List<string>> _confirmationTemplates;
        private readonly Dictionary<string, List<string>> _explanationTemplates;
        private readonly Dictionary<string, List<string>> _errorTemplates;
        private readonly Dictionary<string, List<string>> _suggestionTemplates;
        private readonly Random _random;

        // User preference for verbosity
        public ResponseVerbosity Verbosity { get; set; } = ResponseVerbosity.Normal;

        /// <summary>
        /// Model query service for answering model-related questions.
        /// Set by the Revit integration layer when a Document is available.
        /// </summary>
        public IModelQueryService ModelQueryService { get; set; }

        /// <summary>
        /// The original user message, set before calling GenerateAsync
        /// so knowledge lookup can use the raw query text.
        /// </summary>
        public string CurrentUserMessage { get; set; }

        public ResponseGenerator()
        {
            _random = new Random();
            _confirmationTemplates = InitializeConfirmationTemplates();
            _explanationTemplates = InitializeExplanationTemplates();
            _errorTemplates = InitializeErrorTemplates();
            _suggestionTemplates = InitializeSuggestionTemplates();
        }

        /// <summary>
        /// Generates a response for a design command.
        /// </summary>
        public async Task<ConversationResponse> GenerateAsync(
            DesignCommand command,
            ConversationContext context,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                var response = new ConversationResponse
                {
                    Action = command,
                    ResponseType = ResponseType.Confirmation
                };

                if (!command.IsExecutable)
                {
                    response.ResponseType = ResponseType.Error;
                    response.Message = GenerateErrorMessage(command, "Command cannot be executed");
                    return response;
                }

                var commandType = command.CommandType?.ToUpperInvariant() ?? "";

                // Handle informational queries with real knowledge
                if (commandType == "INFORMATION")
                {
                    response.ResponseType = ResponseType.Information;
                    response.Message = GenerateInformationResponse(CurrentUserMessage ?? "");
                    response.Suggestions = GenerateSuggestions(command, context);
                    return response;
                }

                // Handle model queries with Revit data
                if (commandType == "QUERY_MODEL" || commandType == "QUERY_AREA" || commandType == "CHECK_COMPLIANCE")
                {
                    response.ResponseType = ResponseType.Information;
                    response.Message = GenerateModelQueryResponse(commandType, CurrentUserMessage ?? "");
                    response.Suggestions = GenerateSuggestions(command, context);
                    return response;
                }

                // Handle BOQ, Material Takeoff, Materials, Parameters with structured detail sections
                if (commandType == "GENERATE_BOQ" || commandType == "MATERIAL_TAKEOFF" ||
                    commandType == "QUERY_MATERIALS" || commandType == "QUERY_PARAMETERS")
                {
                    response.ResponseType = ResponseType.Information;
                    var detailedResult = GenerateDetailedQueryResponse(commandType);
                    response.Message = detailedResult.Message;
                    response.DetailSections = detailedResult.Sections;
                    response.Suggestions = GenerateSuggestions(command, context);
                    return response;
                }

                // Generate confirmation message
                response.Message = GenerateConfirmationMessage(command, context);

                // Add suggestions if appropriate
                response.Suggestions = GenerateSuggestions(command, context);

                return response;
            }, cancellationToken);
        }

        /// <summary>
        /// Generates a response for informational queries using the BIM knowledge base.
        /// </summary>
        private string GenerateInformationResponse(string userMessage)
        {
            // Try knowledge base lookup first
            var knowledge = BIMKnowledgeBase.LookupKnowledge(userMessage);
            if (!string.IsNullOrEmpty(knowledge))
            {
                return knowledge;
            }

            // Fallback to template
            return "I can help with BIM concepts, standards (ISO 19650, ASHRAE, IBC, Eurocodes, NFPA), design workflows, and project management. Could you be more specific about what you'd like to know? For example, ask 'What is BIM?', 'Explain ISO 19650', or 'Tell me about clash detection'.";
        }

        /// <summary>
        /// Generates a detailed response with expandable sections for BOQ, materials, etc.
        /// </summary>
        private (string Message, List<QueryDetailSection> Sections) GenerateDetailedQueryResponse(string commandType)
        {
            if (ModelQueryService == null || !ModelQueryService.IsModelAvailable)
            {
                var fallbackMsg = commandType switch
                {
                    "GENERATE_BOQ" => "I need an active Revit model to generate a Bill of Quantities. Please open a project with elements placed, and I'll produce a detailed BOQ broken down by category.",
                    "MATERIAL_TAKEOFF" => "I need an active Revit model to generate a material takeoff. Please open a project so I can calculate material quantities across all elements.",
                    "QUERY_MATERIALS" => "I need an active Revit model to list materials. Please open a project and I'll show you all materials used, their areas, volumes, and which elements they're assigned to.",
                    "QUERY_PARAMETERS" => "I need an active Revit model to show parameter values. Please open a project and I'll display parameter data for walls, doors, windows, rooms, and more.",
                    _ => "Please open a Revit project so I can query the model for you."
                };
                return (fallbackMsg, null);
            }

            QueryResult queryResult;
            switch (commandType)
            {
                case "GENERATE_BOQ":
                    queryResult = ModelQueryService.GetBOQDetailed();
                    break;
                case "MATERIAL_TAKEOFF":
                    queryResult = ModelQueryService.GetMaterialTakeoffDetailed();
                    break;
                case "QUERY_MATERIALS":
                    queryResult = ModelQueryService.GetMaterialsDetailed();
                    break;
                case "QUERY_PARAMETERS":
                    queryResult = ModelQueryService.GetParameterDetails();
                    break;
                default:
                    queryResult = new QueryResult { Summary = "Query type not recognized." };
                    break;
            }

            var message = queryResult.Summary;
            if (queryResult.Sections != null && queryResult.Sections.Count > 0)
            {
                message += "\n\nClick on each section below to expand the details.";
            }

            return (message, queryResult.Sections);
        }

        /// <summary>
        /// Generates a response for model queries using the Revit model service.
        /// </summary>
        private string GenerateModelQueryResponse(string commandType, string userMessage)
        {
            if (ModelQueryService != null && ModelQueryService.IsModelAvailable)
            {
                return ModelQueryService.AnswerQuery(userMessage, commandType);
            }

            // No model available — provide helpful guidance
            switch (commandType)
            {
                case "QUERY_MODEL":
                    return "I don't currently have access to an active Revit model. Please open a Revit project and I'll be able to review it for you — counting rooms, walls, doors, windows, and providing a full model summary.";
                case "QUERY_AREA":
                    return "I need an active Revit model to calculate areas. Please open a project with rooms placed, and I'll calculate the total floor area for you.";
                case "CHECK_COMPLIANCE":
                    return "I need an active Revit model to check compliance. Once you open a project, I can verify it against IBC 2021, ISO 19650, ASHRAE, Eurocodes, and 32 other building standards that StingBIM supports.";
                default:
                    return "Please open a Revit project so I can query the model for you.";
            }
        }

        /// <summary>
        /// Generates a confirmation message for a command.
        /// </summary>
        public string GenerateConfirmationMessage(DesignCommand command, ConversationContext context)
        {
            var commandType = command.CommandType?.ToUpperInvariant() ?? "UNKNOWN";

            // Get base template
            string template = GetTemplate(_confirmationTemplates, commandType);

            // Fill in parameters
            var message = FillTemplate(template, command.Parameters);

            // Add context-aware details if verbose
            if (Verbosity == ResponseVerbosity.Detailed)
            {
                message += GenerateDetailedInfo(command, context);
            }

            return message;
        }

        /// <summary>
        /// Generates an explanation for why something was done.
        /// </summary>
        public string GenerateExplanation(DesignCommand command, string reason, ConversationContext context)
        {
            var commandType = command.CommandType?.ToUpperInvariant() ?? "UNKNOWN";

            string template = GetTemplate(_explanationTemplates, commandType);
            var explanation = FillTemplate(template, command.Parameters);

            if (!string.IsNullOrEmpty(reason))
            {
                explanation += $" {reason}";
            }

            return explanation;
        }

        /// <summary>
        /// Generates an error message.
        /// </summary>
        public string GenerateErrorMessage(DesignCommand command, string error)
        {
            var commandType = command?.CommandType?.ToUpperInvariant() ?? "UNKNOWN";

            // Check for specific error types
            if (error.Contains("constraint", StringComparison.OrdinalIgnoreCase))
            {
                return $"I couldn't complete that because of a constraint violation: {error}. Would you like to try different parameters?";
            }

            if (error.Contains("overlap", StringComparison.OrdinalIgnoreCase) ||
                error.Contains("collision", StringComparison.OrdinalIgnoreCase))
            {
                return $"The element would overlap with existing geometry. {GetCollisionSuggestion(command)}";
            }

            if (error.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                return $"I couldn't find the element you referenced. It may have been deleted or moved.";
            }

            if (error.Contains("permission", StringComparison.OrdinalIgnoreCase))
            {
                return "I don't have permission to modify that element. It may be owned by another user or locked.";
            }

            // Generic error
            string template = GetTemplate(_errorTemplates, commandType);
            return FillTemplate(template, new Dictionary<string, object> { { "ERROR", error } });
        }

        /// <summary>
        /// Generates follow-up suggestions based on the command.
        /// </summary>
        public List<string> GenerateSuggestions(DesignCommand command, ConversationContext context)
        {
            var suggestions = new List<string>();
            var commandType = command.CommandType?.ToUpperInvariant() ?? "";

            switch (commandType)
            {
                case "CREATE_WALL":
                    suggestions.Add("Add a door to this wall");
                    suggestions.Add("Add a window");
                    suggestions.Add("Check wall fire rating");
                    suggestions.Add("Create a room on the other side");
                    break;

                case "CREATE_ROOM":
                    var roomType = command.Parameters.GetValueOrDefault("ROOM_TYPE")?.ToString();
                    suggestions.AddRange(GetRoomSuggestions(roomType));
                    break;

                case "CREATE_FLOOR":
                    suggestions.Add("Create walls on this floor");
                    suggestions.Add("Add rooms to this level");
                    suggestions.Add("Check structural loading");
                    suggestions.Add("Add a floor opening for stairs");
                    break;

                case "CREATE_DOOR":
                case "ADD_DOOR":
                    suggestions.Add("Align with other openings");
                    suggestions.Add("Check ADA door width compliance");
                    suggestions.Add("Create the room this door leads to");
                    break;

                case "CREATE_WINDOW":
                case "ADD_WINDOW":
                    suggestions.Add("Add more windows for cross-ventilation");
                    suggestions.Add("Check glazing area per IBC");
                    suggestions.Add("Analyze daylighting");
                    break;

                case "CREATE_ROOF":
                    suggestions.Add("Add a ceiling");
                    suggestions.Add("Add a parapet");
                    suggestions.Add("Check roof drainage");
                    break;

                case "CREATE_CEILING":
                    suggestions.Add("Add lighting");
                    suggestions.Add("Create another ceiling");
                    suggestions.Add("Check ceiling height compliance");
                    break;

                case "CREATE_STAIRCASE":
                    suggestions.Add("Add handrails");
                    suggestions.Add("Add a railing");
                    suggestions.Add("Check stair compliance");
                    break;

                case "CREATE_COLUMN":
                    suggestions.Add("Add beams between columns");
                    suggestions.Add("Add more columns");
                    suggestions.Add("Add foundations");
                    break;

                case "CREATE_BEAM":
                    suggestions.Add("Add columns");
                    suggestions.Add("Add another beam");
                    suggestions.Add("Check structural loading");
                    break;

                case "CREATE_FOUNDATION":
                    suggestions.Add("Add columns above");
                    suggestions.Add("Add ground beams");
                    suggestions.Add("Check soil conditions");
                    break;

                case "CREATE_RAMP":
                    suggestions.Add("Add a railing");
                    suggestions.Add("Check accessibility compliance");
                    suggestions.Add("Add another ramp");
                    break;

                case "CREATE_RAILING":
                    suggestions.Add("Check railing height compliance");
                    suggestions.Add("Add another railing");
                    break;

                case "CREATE_CURTAIN_WALL":
                    suggestions.Add("Add mullions");
                    suggestions.Add("Check solar heat gain");
                    suggestions.Add("Add another curtain wall");
                    break;

                case "CREATE_PARAPET":
                    suggestions.Add("Add coping");
                    suggestions.Add("Add roof flashing");
                    suggestions.Add("Check fall protection");
                    break;

                // Phase 3: Electrical MEP
                case "CREATE_LIGHTING":
                    suggestions.Add("Add outlets to all rooms");
                    suggestions.Add("Add light switches");
                    suggestions.Add("Add emergency lighting");
                    break;

                case "CREATE_OUTLET":
                    suggestions.Add("Add light switches");
                    suggestions.Add("Add worktop outlets");
                    suggestions.Add("Route conduits");
                    break;

                case "CREATE_SWITCH":
                    suggestions.Add("Add dimmer switches");
                    suggestions.Add("Add 2-way switching");
                    suggestions.Add("Add outlets");
                    break;

                case "CREATE_DB":
                    suggestions.Add("Route conduits from DB");
                    suggestions.Add("Add a generator");
                    suggestions.Add("Add circuit protection");
                    break;

                case "CREATE_GENERATOR":
                    suggestions.Add("Route generator cable to DB");
                    suggestions.Add("Add a fuel tank");
                    suggestions.Add("Add exhaust ducting");
                    break;

                case "CREATE_CONDUIT":
                    suggestions.Add("Add cable tray");
                    suggestions.Add("Place lights and outlets");
                    suggestions.Add("Add conduit fittings");
                    break;

                case "CREATE_CABLE_TRAY":
                    suggestions.Add("Route conduits from tray");
                    suggestions.Add("Add cable ladder");
                    suggestions.Add("Add tray supports");
                    break;

                // Phase 4: HVAC
                case "CREATE_HVAC_AC":
                    suggestions.Add("Add ceiling fans");
                    suggestions.Add("Add extract fans");
                    suggestions.Add("Check cooling load");
                    break;

                case "CREATE_HVAC_FAN":
                    suggestions.Add("Add split AC");
                    suggestions.Add("Add extract fans");
                    suggestions.Add("Add lighting");
                    break;

                case "CREATE_HVAC_EXTRACT":
                    suggestions.Add("Add kitchen hood");
                    suggestions.Add("Route ductwork");
                    suggestions.Add("Check ventilation rates");
                    break;

                case "CREATE_HVAC_HOOD":
                    suggestions.Add("Add extract fans");
                    suggestions.Add("Add kitchen outlets");
                    suggestions.Add("Add kitchen lighting");
                    break;

                // Phase 4: Plumbing
                case "CREATE_PLUMBING":
                    suggestions.Add("Route cold water");
                    suggestions.Add("Route waste pipes");
                    suggestions.Add("Add bathroom extract fan");
                    break;

                case "CREATE_PLUMBING_CW":
                    suggestions.Add("Route waste pipes");
                    suggestions.Add("Add water heater");
                    suggestions.Add("Size the storage tank");
                    break;

                case "CREATE_PLUMBING_WASTE":
                    suggestions.Add("Plan rainwater drainage");
                    suggestions.Add("Route cold water");
                    suggestions.Add("Check pipe gradients");
                    break;

                case "CREATE_PLUMBING_RAIN":
                    suggestions.Add("Add gutters");
                    suggestions.Add("Add rainwater harvesting");
                    suggestions.Add("Check roof drainage");
                    break;

                // Phase 4: Fire Protection
                case "CREATE_FIRE_DETECTOR":
                    suggestions.Add("Add sprinklers");
                    suggestions.Add("Add fire alarm call points");
                    suggestions.Add("Add emergency lighting");
                    break;

                case "CREATE_FIRE_SPRINKLER":
                    suggestions.Add("Add fire detectors");
                    suggestions.Add("Add fire hose reels");
                    suggestions.Add("Route sprinkler piping");
                    break;

                case "CREATE_FIRE_HOSE":
                    suggestions.Add("Add fire extinguishers");
                    suggestions.Add("Add fire alarm call points");
                    suggestions.Add("Check travel distances");
                    break;

                case "CREATE_FIRE_EXTINGUISHER":
                    suggestions.Add("Add fire hose reels");
                    suggestions.Add("Add fire detectors");
                    suggestions.Add("Check fire rating");
                    break;

                case "CREATE_FIRE_ALARM":
                    suggestions.Add("Add fire detectors");
                    suggestions.Add("Add emergency lighting");
                    suggestions.Add("Route alarm cabling");
                    break;

                case "MODIFY_DIMENSION":
                case "RESIZE_ELEMENT":
                    suggestions.Add("Check compliance");
                    suggestions.Add("Resize another room");
                    suggestions.Add("Undo");
                    break;

                case "MODIFY_ELEMENT":
                    suggestions.Add("Validate compliance after changes");
                    suggestions.Add("Analyze updated design");
                    suggestions.Add("Modify other dimensions");
                    break;

                // Phase 5: Modification Engine
                case "MOVE_ELEMENT":
                    suggestions.Add("Move further");
                    suggestions.Add("Copy instead");
                    suggestions.Add("Undo");
                    break;

                case "COPY_ELEMENT":
                    suggestions.Add("Copy to another level");
                    suggestions.Add("Array elements");
                    suggestions.Add("Delete originals");
                    break;

                case "DELETE_ELEMENT":
                    suggestions.Add("Undo");
                    suggestions.Add("Review model");
                    suggestions.Add("Purge unused");
                    break;

                case "ROTATE_ELEMENT":
                    suggestions.Add("Rotate more");
                    suggestions.Add("Mirror instead");
                    suggestions.Add("Undo");
                    break;

                case "MIRROR_ELEMENT":
                    suggestions.Add("Delete originals");
                    suggestions.Add("Mirror other direction");
                    suggestions.Add("Undo");
                    break;

                case "CHANGE_TYPE":
                    suggestions.Add("Check costs");
                    suggestions.Add("Value engineer");
                    suggestions.Add("Undo");
                    break;

                case "SET_PARAMETER":
                    suggestions.Add("Set another parameter");
                    suggestions.Add("Check compliance");
                    suggestions.Add("Generate schedule");
                    break;

                case "SPLIT_ELEMENT":
                    suggestions.Add("Add a door at split");
                    suggestions.Add("Delete a segment");
                    suggestions.Add("Undo");
                    break;

                case "EXTEND_ELEMENT":
                    suggestions.Add("Trim instead");
                    suggestions.Add("Check connections");
                    suggestions.Add("Undo");
                    break;

                case "LEVEL_ADJUST":
                    suggestions.Add("Check stair compliance");
                    suggestions.Add("Adjust another level");
                    suggestions.Add("Undo");
                    break;

                // Phase 5: Bulk Operations
                case "ARRAY_ELEMENT":
                    suggestions.Add("Number them sequentially");
                    suggestions.Add("Change spacing");
                    suggestions.Add("Align elements");
                    break;

                case "ALIGN_ELEMENT":
                    suggestions.Add("Distribute evenly");
                    suggestions.Add("Align differently");
                    suggestions.Add("Undo");
                    break;

                case "DISTRIBUTE_ELEMENT":
                    suggestions.Add("Align to grid");
                    suggestions.Add("Number them");
                    suggestions.Add("Undo");
                    break;

                case "PURGE_UNUSED":
                    suggestions.Add("Review model health");
                    suggestions.Add("Generate BOQ");
                    suggestions.Add("Save project");
                    break;

                case "VALUE_ENGINEER":
                    suggestions.Add("Apply all changes");
                    suggestions.Add("Generate full BOQ");
                    suggestions.Add("Review one by one");
                    break;

                case "AUTO_TAG":
                    suggestions.Add("Tag another category");
                    suggestions.Add("Generate schedule");
                    suggestions.Add("Renumber elements");
                    break;

                case "RENUMBER_ELEMENT":
                    suggestions.Add("Generate door schedule");
                    suggestions.Add("Tag elements");
                    suggestions.Add("Renumber another category");
                    break;

                case "GREETING":
                    suggestions.Add("Create a room");
                    suggestions.Add("Generate maintenance schedule");
                    suggestions.Add("Help");
                    break;

                case "HELP":
                    suggestions.Add("Create a 4x5 meter bedroom");
                    suggestions.Add("Check compliance");
                    suggestions.Add("Generate maintenance schedule");
                    break;

                case "INFORMATION":
                    suggestions.Add("What is ISO 19650?");
                    suggestions.Add("Check compliance");
                    suggestions.Add("Help");
                    break;

                case "QUERY_MODEL":
                    suggestions.Add("What's the total area?");
                    suggestions.Add("Check compliance");
                    suggestions.Add("Review model health");
                    break;

                case "QUERY_AREA":
                    suggestions.Add("Review the model");
                    suggestions.Add("Check compliance");
                    suggestions.Add("How many rooms?");
                    break;

                case "CHECK_COMPLIANCE":
                    suggestions.Add("What standards apply?");
                    suggestions.Add("Review the model");
                    suggestions.Add("Generate maintenance schedule");
                    break;

                case "GENERATE_BOQ":
                    suggestions.Add("Material takeoff");
                    suggestions.Add("What materials are used?");
                    suggestions.Add("Check compliance");
                    break;

                case "MATERIAL_TAKEOFF":
                    suggestions.Add("Generate BOQ");
                    suggestions.Add("Show material details");
                    suggestions.Add("Review the model");
                    break;

                case "QUERY_MATERIALS":
                    suggestions.Add("Material takeoff");
                    suggestions.Add("Generate BOQ");
                    suggestions.Add("Show parameters");
                    break;

                case "QUERY_PARAMETERS":
                    suggestions.Add("What materials are used?");
                    suggestions.Add("Generate BOQ");
                    suggestions.Add("Review the model");
                    break;

                case "CREATE_HOUSE":
                    suggestions.Add("Review the model");
                    suggestions.Add("Check compliance");
                    suggestions.Add("What's the total area?");
                    break;

                case "GENERATE_MAINTENANCE_SCHEDULE":
                case "PREDICT_FAILURES":
                case "OPTIMIZE_MAINTENANCE":
                case "ANALYZE_FAILURES":
                case "FM_GENERAL":
                    suggestions.Add("Generate maintenance schedule");
                    suggestions.Add("Predict equipment failures");
                    suggestions.Add("Optimize maintenance strategy");
                    break;

                case "NEGOTIATE_DESIGN":
                case "GET_RECOMMENDATIONS":
                case "RESOLVE_CONFLICT":
                case "COLLABORATE":
                    suggestions.Add("Get agent recommendations");
                    suggestions.Add("Resolve design conflicts");
                    suggestions.Add("Cross-discipline review");
                    break;

                // Phase 6: LAN Collaboration
                case "SETUP_WORKSHARING":
                    suggestions.Add("Sync to central");
                    suggestions.Add("View team status");
                    suggestions.Add("Generate BEP");
                    break;

                case "SYNC_MODEL":
                    suggestions.Add("View changelog");
                    suggestions.Add("Check conflicts");
                    suggestions.Add("Model health check");
                    break;

                case "CHECK_WORKSHARING_CONFLICTS":
                    suggestions.Add("Sync to central");
                    suggestions.Add("Relinquish elements");
                    suggestions.Add("View team status");
                    break;

                case "DIAGNOSE_EDIT":
                    suggestions.Add("Sync to central");
                    suggestions.Add("Check conflicts");
                    suggestions.Add("View team status");
                    break;

                case "GENERATE_BEP":
                    suggestions.Add("View changelog");
                    suggestions.Add("Model health check");
                    suggestions.Add("Sync to central");
                    break;

                case "MODEL_HEALTH_CHECK":
                    suggestions.Add("Create backup");
                    suggestions.Add("Sync to central");
                    suggestions.Add("Check conflicts");
                    break;

                case "VIEW_CHANGELOG":
                    suggestions.Add("Export changelog to CSV");
                    suggestions.Add("View team status");
                    suggestions.Add("Sync to central");
                    break;

                case "VIEW_TEAM":
                    suggestions.Add("View changelog");
                    suggestions.Add("Check conflicts");
                    suggestions.Add("Sync to central");
                    break;

                case "CREATE_BACKUP":
                case "RESTORE_BACKUP":
                case "LIST_BACKUPS":
                    suggestions.Add("List backups");
                    suggestions.Add("Model health check");
                    suggestions.Add("Sync to central");
                    break;

                case "START_AUTOSYNC":
                case "STOP_AUTOSYNC":
                    suggestions.Add("Sync to central");
                    suggestions.Add("View team status");
                    suggestions.Add("Start auto-backup");
                    break;

                case "START_AUTOBACKUP":
                case "STOP_AUTOBACKUP":
                    suggestions.Add("Create backup");
                    suggestions.Add("List backups");
                    suggestions.Add("Model health check");
                    break;

                case "RELINQUISH_ELEMENT":
                    suggestions.Add("Sync to central");
                    suggestions.Add("Check conflicts");
                    suggestions.Add("View team status");
                    break;

                case "EXPORT_CHANGELOG":
                    suggestions.Add("View changelog");
                    suggestions.Add("View team status");
                    suggestions.Add("Sync to central");
                    break;

                // Phase 7: Budget & Exports
                case "BUDGET_DESIGN":
                    suggestions.Add("Export BOQ");
                    suggestions.Add("Estimate cost");
                    suggestions.Add("Value engineer");
                    break;

                case "ESTIMATE_COST":
                case "CHECK_BUDGET":
                    suggestions.Add("Budget design");
                    suggestions.Add("Export BOQ");
                    suggestions.Add("Value engineer");
                    break;

                case "EXPORT_BOQ":
                    suggestions.Add("Export COBie");
                    suggestions.Add("Export room schedule");
                    suggestions.Add("Estimate cost");
                    break;

                case "EXPORT_COBIE":
                    suggestions.Add("Export BOQ");
                    suggestions.Add("Export room schedule");
                    suggestions.Add("Model health check");
                    break;

                case "EXPORT_ROOM_SCHEDULE":
                    suggestions.Add("Export door schedule");
                    suggestions.Add("Export window schedule");
                    suggestions.Add("Export BOQ");
                    break;

                case "EXPORT_DOOR_SCHEDULE":
                case "EXPORT_WINDOW_SCHEDULE":
                    suggestions.Add("Export room schedule");
                    suggestions.Add("Export BOQ");
                    suggestions.Add("Export COBie");
                    break;

                case "IMPORT_PARAMETERS":
                    suggestions.Add("Show parameters");
                    suggestions.Add("Export room schedule");
                    suggestions.Add("Generate BOQ");
                    break;

                case "VALUE_ENGINEER_BUDGET":
                    suggestions.Add("Budget design");
                    suggestions.Add("Export BOQ");
                    suggestions.Add("Estimate cost");
                    break;

                // Phase 8: Specialist Systems
                case "CREATE_DATA_OUTLET":
                    suggestions.Add("Plan WiFi coverage");
                    suggestions.Add("Design server room");
                    suggestions.Add("Add CCTV");
                    break;

                case "CREATE_WIFI_AP":
                    suggestions.Add("Add data outlets");
                    suggestions.Add("Design server room");
                    suggestions.Add("Estimate cost");
                    break;

                case "CREATE_SERVER_ROOM":
                    suggestions.Add("Add data outlets");
                    suggestions.Add("Add CCTV");
                    suggestions.Add("Design solar PV");
                    break;

                case "CREATE_CCTV":
                    suggestions.Add("Set up access control");
                    suggestions.Add("Design alarm system");
                    suggestions.Add("Add intercom");
                    break;

                case "CREATE_ACCESS_CONTROL":
                    suggestions.Add("Add CCTV");
                    suggestions.Add("Design alarm system");
                    suggestions.Add("Add intercom");
                    break;

                case "CREATE_ALARM_SYSTEM":
                    suggestions.Add("Add CCTV");
                    suggestions.Add("Set up access control");
                    suggestions.Add("Add gas detectors");
                    break;

                case "CREATE_INTERCOM":
                    suggestions.Add("Set up access control");
                    suggestions.Add("Add CCTV");
                    suggestions.Add("Design alarm system");
                    break;

                case "CREATE_GAS_PIPING":
                case "CREATE_GAS_DETECTOR":
                    suggestions.Add("Add gas detectors");
                    suggestions.Add("Design alarm system");
                    suggestions.Add("Check Uganda compliance");
                    break;

                case "GET_DESIGN_ADVICE":
                case "RUN_MODEL_AUDIT":
                    suggestions.Add("Check Uganda compliance");
                    suggestions.Add("Design solar PV");
                    suggestions.Add("Estimate cost");
                    break;

                case "CHECK_UGANDA_COMPLIANCE":
                    suggestions.Add("Run model audit");
                    suggestions.Add("Get design advice");
                    suggestions.Add("Export BOQ");
                    break;

                case "SET_BUDGET":
                    suggestions.Add("Budget design");
                    suggestions.Add("Estimate cost");
                    suggestions.Add("Run model audit");
                    break;

                // Consulting intent follow-ups
                case "CONSULT_STRUCTURAL":
                    suggestions.Add("Check another structural element");
                    suggestions.Add("Review load combinations");
                    suggestions.Add("Compare design options");
                    break;

                case "CONSULT_MEP":
                    suggestions.Add("Size another system");
                    suggestions.Add("Check ventilation rates");
                    suggestions.Add("Review equipment schedule");
                    break;

                case "CONSULT_COMPLIANCE":
                    suggestions.Add("Check another code requirement");
                    suggestions.Add("Review accessibility");
                    suggestions.Add("Check fire safety");
                    break;

                case "CONSULT_MATERIALS":
                    suggestions.Add("Compare material options");
                    suggestions.Add("Check material cost");
                    suggestions.Add("Review thermal properties");
                    break;

                case "CONSULT_COST":
                    suggestions.Add("Break down by element");
                    suggestions.Add("Compare alternatives");
                    suggestions.Add("Value engineering options");
                    break;

                case "CONSULT_SUSTAINABILITY":
                    suggestions.Add("Check energy credits");
                    suggestions.Add("Review water strategy");
                    suggestions.Add("Passive design options");
                    break;

                case "CONSULT_FIRE_SAFETY":
                    suggestions.Add("Check fire ratings");
                    suggestions.Add("Review sprinkler design");
                    suggestions.Add("Verify egress routes");
                    break;

                case "CONSULT_ACCESSIBILITY":
                    suggestions.Add("Check ramp compliance");
                    suggestions.Add("Review door clearances");
                    suggestions.Add("Accessible toilet design");
                    break;

                case "CONSULT_ENERGY":
                    suggestions.Add("Check insulation values");
                    suggestions.Add("Review glazing performance");
                    suggestions.Add("HVAC energy options");
                    break;

                case "CONSULT_ACOUSTICS":
                    suggestions.Add("Wall build-up options");
                    suggestions.Add("Floor impact ratings");
                    suggestions.Add("Background noise criteria");
                    break;

                case "CONSULT_DAYLIGHTING":
                    suggestions.Add("Optimize window sizing");
                    suggestions.Add("Glare control options");
                    suggestions.Add("Light shelf design");
                    break;

                case "CONSULT_SITE_PLANNING":
                    suggestions.Add("Parking layout options");
                    suggestions.Add("Orientation analysis");
                    suggestions.Add("Setback requirements");
                    break;

                // BIM management intent follow-ups
                case "MANAGE_DESIGN_ANALYSIS":
                    suggestions.Add("Analyze spatial quality");
                    suggestions.Add("Check circulation efficiency");
                    suggestions.Add("Review design completeness");
                    break;

                case "MANAGE_OPTIMIZATION":
                    suggestions.Add("Optimize room placement");
                    suggestions.Add("Improve layout efficiency");
                    suggestions.Add("Find improvement opportunities");
                    break;

                case "MANAGE_DECISION_SUPPORT":
                    suggestions.Add("Compare structural systems");
                    suggestions.Add("Analyze trade-offs");
                    suggestions.Add("Assess project risks");
                    break;

                case "MANAGE_VALIDATION":
                    suggestions.Add("Check for collisions");
                    suggestions.Add("Run compliance validation");
                    suggestions.Add("Verify adjacency requirements");
                    break;

                case "MANAGE_DESIGN_PATTERNS":
                    suggestions.Add("Show residential patterns");
                    suggestions.Add("Suggest patterns for my project");
                    suggestions.Add("Check pattern compliance");
                    break;

                case "MANAGE_PREDICTIVE":
                    suggestions.Add("What should I do next?");
                    suggestions.Add("Show design workflow");
                    suggestions.Add("Get proactive suggestions");
                    break;

                case "UNKNOWN":
                    suggestions.Add("Help");
                    suggestions.Add("Create a room");
                    suggestions.Add("Generate maintenance schedule");
                    break;
            }

            // Add contextual suggestions
            if (context.RecentActions.Count > 0 &&
                context.RecentActions[0].Success)
            {
                suggestions.Add("Undo last action");
            }

            return suggestions.Take(3).ToList();
        }

        /// <summary>
        /// Generates a summary of multiple actions.
        /// </summary>
        public string GenerateActionSummary(IEnumerable<DesignCommand> commands)
        {
            var commandList = commands.ToList();

            if (commandList.Count == 0)
                return "No actions performed.";

            if (commandList.Count == 1)
                return GenerateConfirmationMessage(commandList[0], null);

            var sb = new StringBuilder();
            sb.AppendLine($"Completed {commandList.Count} actions:");

            // Group by type
            var grouped = commandList.GroupBy(c => c.CommandType);
            foreach (var group in grouped)
            {
                sb.AppendLine($"  • {group.Count()}x {FormatCommandType(group.Key)}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Generates a warning message.
        /// </summary>
        public string GenerateWarning(string warningType, Dictionary<string, object> details)
        {
            switch (warningType.ToUpperInvariant())
            {
                case "COMPLIANCE":
                    var standard = details.GetValueOrDefault("STANDARD")?.ToString() ?? "building code";
                    var issue = details.GetValueOrDefault("ISSUE")?.ToString() ?? "potential issue";
                    return $"Warning: This may not comply with {standard}. {issue}";

                case "PERFORMANCE":
                    return "This action may take longer than usual due to the complexity of the model.";

                case "STRUCTURAL":
                    return "This modification may affect structural integrity. Please consult with a structural engineer.";

                case "UNDO_LIMIT":
                    return "You've reached the undo limit. Some earlier changes cannot be reverted.";

                default:
                    return $"Warning: {details.GetValueOrDefault("MESSAGE")?.ToString() ?? "Please review this action."}";
            }
        }

        #region Private Methods

        private string GetTemplate(Dictionary<string, List<string>> templates, string key)
        {
            if (templates.TryGetValue(key, out var options) && options.Count > 0)
            {
                return options[_random.Next(options.Count)];
            }

            // Fallback to generic
            if (templates.TryGetValue("GENERIC", out var generic) && generic.Count > 0)
            {
                return generic[_random.Next(generic.Count)];
            }

            return "Done.";
        }

        private string FillTemplate(string template, Dictionary<string, object> parameters)
        {
            if (string.IsNullOrEmpty(template) || parameters == null)
                return template;

            var result = template;

            foreach (var (key, value) in parameters)
            {
                var placeholder = $"{{{key}}}";
                var formattedValue = FormatValue(key, value);
                result = result.Replace(placeholder, formattedValue, StringComparison.OrdinalIgnoreCase);
            }

            // Remove any unfilled placeholders
            result = System.Text.RegularExpressions.Regex.Replace(result, @"\{[A-Z_]+\}", "");
            result = System.Text.RegularExpressions.Regex.Replace(result, @"\s+", " ").Trim();

            return result;
        }

        private string FormatValue(string key, object value)
        {
            if (value == null) return "";

            // Format dimensions nicely
            if (key.Contains("DIMENSION") || key.Contains("WIDTH") || key.Contains("HEIGHT") || key.Contains("LENGTH"))
            {
                if (double.TryParse(value.ToString().Replace("mm", ""), out var mm))
                {
                    if (mm >= 1000)
                        return $"{mm / 1000:F1}m";
                    return $"{mm:F0}mm";
                }
            }

            return value.ToString();
        }

        private string FormatCommandType(string commandType)
        {
            return commandType?.ToLowerInvariant().Replace("_", " ") ?? "action";
        }

        private string GenerateDetailedInfo(DesignCommand command, ConversationContext context)
        {
            var sb = new StringBuilder();

            // Add level info
            if (!string.IsNullOrEmpty(context?.CurrentLevel))
            {
                sb.Append($" on {context.CurrentLevel}");
            }

            // Add parameter details
            if (command.Parameters.Count > 0)
            {
                var details = command.Parameters
                    .Where(p => !p.Key.StartsWith("_"))
                    .Select(p => $"{FormatParameterName(p.Key)}: {FormatValue(p.Key, p.Value)}");

                if (details.Any())
                {
                    sb.Append($" ({string.Join(", ", details)})");
                }
            }

            return sb.ToString();
        }

        private string FormatParameterName(string key)
        {
            return key.ToLowerInvariant().Replace("_", " ");
        }

        private string GetCollisionSuggestion(DesignCommand command)
        {
            return command?.CommandType?.ToUpperInvariant() switch
            {
                "CREATE_WALL" => "Try adjusting the wall position or shortening it.",
                "CREATE_DOOR" => "Try moving the door to a different location on the wall.",
                "CREATE_WINDOW" => "The window may be too close to another opening. Try a different position.",
                _ => "Try adjusting the position or dimensions."
            };
        }

        private IEnumerable<string> GetRoomSuggestions(string roomType)
        {
            roomType = roomType?.ToLowerInvariant() ?? "";

            if (roomType.Contains("bedroom") || roomType.Contains("master"))
            {
                return new[] { "Create an en-suite bathroom", "Add a walk-in closet", "Add a window for ventilation" };
            }

            if (roomType.Contains("kitchen"))
            {
                return new[] { "Create adjacent dining room", "Add a pantry", "Check ventilation per ASHRAE 62.1" };
            }

            if (roomType.Contains("living"))
            {
                return new[] { "Create adjacent dining area", "Add a balcony", "Analyze daylighting quality" };
            }

            if (roomType.Contains("bathroom") || roomType.Contains("toilet"))
            {
                return new[] { "Check ADA accessibility compliance", "Add ventilation", "Create adjacent bedroom" };
            }

            if (roomType.Contains("office"))
            {
                return new[] { "Add a meeting room nearby", "Create reception area", "Check lighting per CIBSE" };
            }

            if (roomType.Contains("conference") || roomType.Contains("meeting"))
            {
                return new[] { "Check acoustic rating STC 50+", "Add adjacent breakout space", "Verify occupancy capacity" };
            }

            if (roomType.Contains("corridor") || roomType.Contains("hallway"))
            {
                return new[] { "Verify ADA corridor width (1220mm min)", "Check egress travel distance", "Add emergency lighting" };
            }

            if (roomType.Contains("server") || roomType.Contains("data"))
            {
                return new[] { "Check structural loading capacity", "Verify cooling requirements", "Add raised access floor" };
            }

            if (roomType.Contains("garage") || roomType.Contains("parking"))
            {
                return new[] { "Check ramp slope compliance", "Add ventilation system", "Verify structural capacity" };
            }

            return new[] { "Add a door", "Add a window", "Validate compliance for this room" };
        }

        #endregion

        #region Template Initialization

        private Dictionary<string, List<string>> InitializeConfirmationTemplates()
        {
            return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["CREATE_WALL"] = new List<string>
                {
                    "Created a {DIMENSION} wall.",
                    "Wall created with {DIMENSION} length.",
                    "I've added a {DIMENSION} {WALL_TYPE} wall."
                },
                ["CREATE_ROOM"] = new List<string>
                {
                    "Created a {ROOM_TYPE}.",
                    "I've added a {ROOM_TYPE} with {DIMENSION} dimensions.",
                    "{ROOM_TYPE} created successfully."
                },
                ["CREATE_HOUSE"] = new List<string>
                {
                    "I'll create the house layout for you. This will include generating the rooms, walls, doors, and windows based on your specifications. I'll use standard room sizes and spatial relationships to create an optimal layout.",
                    "Starting house design. I'll set up the floor plan with the specified rooms, automatically placing walls, doors, and windows following building standards and best practices.",
                    "Generating the house layout now. I'll create each room with appropriate dimensions, proper adjacencies (kitchen near dining, bedrooms with ensuite access), and required elements (doors, windows, ventilation)."
                },
                ["CREATE_FLOOR"] = new List<string>
                {
                    "Creating a floor slab with the specified dimensions.",
                    "I'll add a floor slab for you.",
                    "Floor creation in progress."
                },
                ["CREATE_DOOR"] = new List<string>
                {
                    "Door placed.",
                    "Added a {DOOR_TYPE} door.",
                    "I've placed a door in the wall."
                },
                ["CREATE_WINDOW"] = new List<string>
                {
                    "Window added.",
                    "Placed a {WINDOW_TYPE} window.",
                    "I've added a window to the wall."
                },
                ["CREATE_ROOF"] = new List<string>
                {
                    "Creating a roof with the specified profile.",
                    "I'll add a roof for you.",
                    "Roof creation in progress."
                },
                ["CREATE_CEILING"] = new List<string>
                {
                    "Adding a ceiling to the room.",
                    "I'll create a ceiling for you.",
                    "Ceiling placement in progress."
                },
                ["CREATE_STAIRCASE"] = new List<string>
                {
                    "Creating a staircase between levels.",
                    "I'll build the staircase for you with compliant riser/going dimensions.",
                    "Staircase creation in progress."
                },
                ["CREATE_COLUMN"] = new List<string>
                {
                    "Placing a structural column.",
                    "I'll create the column for you.",
                    "Column placement in progress."
                },
                ["CREATE_BEAM"] = new List<string>
                {
                    "Creating a structural beam.",
                    "I'll add the beam for you.",
                    "Beam creation in progress."
                },
                ["CREATE_FOUNDATION"] = new List<string>
                {
                    "Creating the foundation.",
                    "I'll add the foundation for you.",
                    "Foundation creation in progress."
                },
                ["CREATE_RAMP"] = new List<string>
                {
                    "Creating an accessible ramp.",
                    "I'll add the ramp for you.",
                    "Ramp creation in progress."
                },
                ["CREATE_RAILING"] = new List<string>
                {
                    "Placing a railing.",
                    "I'll add the railing for you.",
                    "Railing creation in progress."
                },
                ["CREATE_CURTAIN_WALL"] = new List<string>
                {
                    "Creating a curtain wall.",
                    "I'll add the curtain wall for you.",
                    "Curtain wall creation in progress."
                },
                ["CREATE_PARAPET"] = new List<string>
                {
                    "Creating a parapet wall.",
                    "I'll add the parapet for you.",
                    "Parapet creation in progress."
                },
                // Phase 3: Electrical MEP
                ["CREATE_LIGHTING"] = new List<string>
                {
                    "Placing lighting fixtures using the LUX calculation algorithm.",
                    "I'll calculate the required fixtures and place them on a grid.",
                    "Lighting design in progress — calculating fixture count from room area and LUX target."
                },
                ["CREATE_OUTLET"] = new List<string>
                {
                    "Placing power outlets in the room.",
                    "I'll add outlets along each wall face.",
                    "Power outlet placement in progress."
                },
                ["CREATE_SWITCH"] = new List<string>
                {
                    "Placing light switches at door positions.",
                    "I'll add switches at 1200mm AFF next to each door.",
                    "Switch placement in progress."
                },
                ["CREATE_DB"] = new List<string>
                {
                    "Placing a distribution board.",
                    "I'll size and place the DB based on the circuit count.",
                    "Distribution board installation in progress."
                },
                ["CREATE_GENERATOR"] = new List<string>
                {
                    "Placing a standby generator with load assessment.",
                    "I'll size the generator based on the building's connected load.",
                    "Generator placement in progress."
                },
                ["CREATE_CONDUIT"] = new List<string>
                {
                    "Routing conduits using spine-and-branch pattern.",
                    "I'll route conduit runs from the DB to rooms.",
                    "Conduit routing in progress."
                },
                ["CREATE_CABLE_TRAY"] = new List<string>
                {
                    "Creating cable tray run.",
                    "I'll add cable tray at ceiling level.",
                    "Cable tray installation in progress."
                },
                // Phase 4: HVAC
                ["CREATE_HVAC_AC"] = new List<string>
                {
                    "Placing split AC units based on room cooling load (125 W/m²).",
                    "I'll size and place the AC unit for the room area.",
                    "Split AC placement in progress — calculating cooling capacity."
                },
                ["CREATE_HVAC_FAN"] = new List<string>
                {
                    "Placing ceiling fans with 2100mm blade-to-floor clearance.",
                    "I'll add ceiling fans to the room.",
                    "Ceiling fan placement in progress."
                },
                ["CREATE_HVAC_EXTRACT"] = new List<string>
                {
                    "Placing extract fans based on ACH requirements.",
                    "I'll add extract ventilation — kitchen 15 ACH, bathroom 10 ACH.",
                    "Extract fan placement in progress."
                },
                ["CREATE_HVAC_HOOD"] = new List<string>
                {
                    "Placing kitchen hood 650-700mm above cooker level.",
                    "I'll add a kitchen extract hood for you.",
                    "Kitchen hood installation in progress."
                },
                // Phase 4: Plumbing
                ["CREATE_PLUMBING"] = new List<string>
                {
                    "Placing plumbing fixtures in the room.",
                    "I'll add the plumbing fixtures — WC, basin, shower as required.",
                    "Plumbing fixture placement in progress."
                },
                ["CREATE_PLUMBING_CW"] = new List<string>
                {
                    "Routing cold water pipes — 22mm main, 15mm branches.",
                    "I'll route cold water from the storage tank to fixtures.",
                    "Cold water pipe routing in progress."
                },
                ["CREATE_PLUMBING_WASTE"] = new List<string>
                {
                    "Routing waste pipes — 100mm soil stack, sized branches per fixture.",
                    "I'll route waste pipes from fixtures to the soil stack.",
                    "Waste pipe routing in progress."
                },
                ["CREATE_PLUMBING_RAIN"] = new List<string>
                {
                    "Planning rainwater drainage — 1 downpipe per 20m² of roof.",
                    "I'll calculate and place rainwater drainage based on roof area.",
                    "Rainwater drainage design in progress."
                },
                // Phase 4: Fire Protection
                ["CREATE_FIRE_DETECTOR"] = new List<string>
                {
                    "Placing smoke/heat detectors per BS 5839 — max 80m² per detector.",
                    "I'll add detectors to each room within coverage requirements.",
                    "Fire detector placement in progress."
                },
                ["CREATE_FIRE_SPRINKLER"] = new List<string>
                {
                    "Placing sprinkler heads per NFPA 13 — max 12m² per head.",
                    "I'll design the sprinkler layout for the coverage area.",
                    "Sprinkler head placement in progress."
                },
                ["CREATE_FIRE_HOSE"] = new List<string>
                {
                    "Placing fire hose reels — 36m coverage radius, 1 per 500m².",
                    "I'll position fire hose reels for complete floor coverage.",
                    "Fire hose reel placement in progress."
                },
                ["CREATE_FIRE_EXTINGUISHER"] = new List<string>
                {
                    "Placing fire extinguishers — powder, CO2, wet chemical as required.",
                    "I'll place extinguishers based on room type and fire risk.",
                    "Fire extinguisher placement in progress."
                },
                ["CREATE_FIRE_ALARM"] = new List<string>
                {
                    "Placing manual call points at 1400mm AFF, max 30m travel distance.",
                    "I'll position manual call points near exits and escape routes.",
                    "Fire alarm call point placement in progress."
                },
                ["MODIFY_DIMENSION"] = new List<string>
                {
                    "Dimension updated to {DIMENSION}.",
                    "Changed to {DIMENSION}.",
                    "I've adjusted the size to {DIMENSION}."
                },
                ["MOVE_ELEMENT"] = new List<string>
                {
                    "Moved {DIRECTION}.",
                    "Element repositioned.",
                    "I've moved it {DIRECTION}."
                },
                ["DELETE_ELEMENT"] = new List<string>
                {
                    "Element deleted.",
                    "Removed successfully.",
                    "I've deleted that element."
                },
                ["COPY_ELEMENT"] = new List<string>
                {
                    "Element copied.",
                    "Copy created.",
                    "I've duplicated the element."
                },
                // Phase 5: Modification Engine
                ["ROTATE_ELEMENT"] = new List<string>
                {
                    "Element rotated.",
                    "Rotation applied.",
                    "I've rotated the element."
                },
                ["MIRROR_ELEMENT"] = new List<string>
                {
                    "Elements mirrored.",
                    "Mirror operation complete.",
                    "I've mirrored the selected elements."
                },
                ["RESIZE_ELEMENT"] = new List<string>
                {
                    "Element resized to {DIMENSION}.",
                    "Size updated.",
                    "I've adjusted the dimensions."
                },
                ["CHANGE_TYPE"] = new List<string>
                {
                    "Type changed successfully.",
                    "I've updated the element type.",
                    "Element type changed."
                },
                ["SET_PARAMETER"] = new List<string>
                {
                    "Parameter set successfully.",
                    "I've updated the parameter value.",
                    "Parameter value changed."
                },
                ["SPLIT_ELEMENT"] = new List<string>
                {
                    "Element split into segments.",
                    "Split operation complete.",
                    "I've split the element."
                },
                ["EXTEND_ELEMENT"] = new List<string>
                {
                    "Element extended.",
                    "Extension complete.",
                    "I've extended the element."
                },
                ["OFFSET_ELEMENT"] = new List<string>
                {
                    "Element offset applied.",
                    "Offset complete.",
                    "I've offset the element."
                },
                ["LEVEL_ADJUST"] = new List<string>
                {
                    "Level elevation adjusted.",
                    "Level height updated.",
                    "I've adjusted the level elevation."
                },
                ["PIN_ELEMENT"] = new List<string>
                {
                    "Elements pinned.",
                    "I've pinned the elements to prevent accidental movement."
                },
                ["UNPIN_ELEMENT"] = new List<string>
                {
                    "Elements unpinned.",
                    "I've unlocked the elements for editing."
                },
                // Phase 5: Bulk Operations
                ["ARRAY_ELEMENT"] = new List<string>
                {
                    "Array created.",
                    "I've arrayed the elements at the specified spacing.",
                    "Element array complete."
                },
                ["ALIGN_ELEMENT"] = new List<string>
                {
                    "Elements aligned.",
                    "Alignment complete.",
                    "I've aligned the selected elements."
                },
                ["DISTRIBUTE_ELEMENT"] = new List<string>
                {
                    "Elements distributed evenly.",
                    "Even distribution applied.",
                    "I've spaced the elements equally."
                },
                ["PURGE_UNUSED"] = new List<string>
                {
                    "Unused types purged.",
                    "I've removed unused families and types.",
                    "Purge complete — project cleaned up."
                },
                ["VALUE_ENGINEER"] = new List<string>
                {
                    "Value engineering analysis complete.",
                    "I've found potential cost savings.",
                    "Cost-saving alternatives identified."
                },
                ["AUTO_TAG"] = new List<string>
                {
                    "Elements tagged.",
                    "Auto-tagging complete.",
                    "I've tagged and numbered the elements."
                },
                ["RENUMBER_ELEMENT"] = new List<string>
                {
                    "Elements renumbered.",
                    "Sequential numbering applied.",
                    "I've numbered the elements by level."
                },
                ["QUERY_AREA"] = new List<string>
                {
                    "The area is {DIMENSION}.",
                    "Calculating area...",
                    "I'll calculate the area for you."
                },
                ["GREETING"] = new List<string>
                {
                    "Hello! How can I help you with your Revit model today?",
                    "Hi there! What would you like to design or modify?",
                    "Hey! I'm ready to help with your BIM project. What do you need?"
                },
                ["HELP"] = new List<string>
                {
                    "I can help you design in Revit using natural language. Try commands like 'Create a 4x5 meter bedroom', 'Add a window to the south wall', or 'Check fire code compliance'.",
                    "Here's what I can do: create walls, rooms, doors, windows; move, copy, or delete elements; check compliance; and much more. Just describe what you need!",
                    "I'm your BIM assistant! I can create elements, check standards compliance, manage parameters, and automate tasks. Just tell me what you need."
                },
                ["CHECK_COMPLIANCE"] = new List<string>
                {
                    "Checking compliance against applicable building codes...",
                    "Running compliance check. I'll review the relevant standards.",
                    "Initiating compliance verification against building standards."
                },
                ["QUERY_MODEL"] = new List<string>
                {
                    "I'll analyze the model for you. Let me review the current project information including rooms, elements, and levels.",
                    "Reviewing the model now. I'll gather information about the elements, rooms, and overall project status.",
                    "Analyzing the model. I'll check the current project data and provide a summary."
                },
                // Informational responses
                ["INFORMATION"] = new List<string>
                {
                    "That's a great question! In the context of BIM and building design, I can provide guidance on standards, parameters, materials, and design best practices. Could you tell me more about what you'd like to know?",
                    "I'd be happy to help explain that. I have knowledge of ISO 19650, ASHRAE, IBC, Eurocodes, and 32 other building standards. What specific topic would you like to explore?",
                    "Good question! I can provide information about BIM workflows, building standards, materials, structural design, MEP systems, and more. What area interests you?"
                },

                // Facilities Management responses
                ["GENERATE_MAINTENANCE_SCHEDULE"] = new List<string>
                {
                    "I'll generate a predictive maintenance schedule for your building systems. This covers AHUs, chillers, boilers, pumps, lifts, generators, and transformers with a 12-month lookahead.",
                    "Generating a maintenance schedule. I'll analyze equipment types, failure patterns, and optimize task intervals for cost-effective maintenance.",
                    "Starting maintenance schedule generation. I'll create a comprehensive plan covering preventive, predictive, and condition-based maintenance tasks."
                },
                ["PREDICT_FAILURES"] = new List<string>
                {
                    "I'll analyze equipment data to predict upcoming failures over the next 6 months. This includes risk scoring, confidence levels, and recommended preventive actions.",
                    "Running failure prediction analysis. I'll identify equipment at risk and provide early warning recommendations.",
                    "Initiating predictive failure analysis for building equipment. I'll assess failure probabilities and suggest interventions."
                },
                ["OPTIMIZE_MAINTENANCE"] = new List<string>
                {
                    "I'll analyze your maintenance strategy and recommend optimizations. I can compare Preventive, Predictive, Reactive, and Hybrid approaches for cost savings.",
                    "Optimizing maintenance strategy. I'll evaluate current costs, downtime patterns, and suggest the most cost-effective approach.",
                    "Starting maintenance optimization analysis. I'll balance cost, reliability, and equipment lifecycle for the best strategy."
                },
                ["ANALYZE_FAILURES"] = new List<string>
                {
                    "I'll analyze historical failure patterns to identify root causes, recurring issues, and trends across your equipment.",
                    "Running failure pattern analysis. I'll look at historical data to identify systemic issues and improvement opportunities.",
                    "Analyzing equipment failure patterns. I'll examine failure modes, frequencies, and contributing factors."
                },
                ["FM_GENERAL"] = new List<string>
                {
                    "I can help with facilities management tasks including maintenance scheduling, failure prediction, asset lifecycle management, and spare parts tracking. What would you like to do?",
                    "For facilities management, I offer predictive maintenance, equipment health monitoring, replacement cycle planning, and cost optimization. How can I help?",
                    "I have full FM capabilities including maintenance schedules, failure analysis, spare parts inventory, and building operations optimization. What do you need?"
                },

                // BOQ, Material, Parameters responses
                ["GENERATE_BOQ"] = new List<string>
                {
                    "I've generated a Bill of Quantities from your model. The BOQ is organized by element category — click on each section to see the detailed breakdown including quantities, areas, and volumes.",
                    "Here's your Bill of Quantities. It covers walls, doors, windows, floors, roofs, columns, and rooms. Expand each section for type-by-type details.",
                    "BOQ generated from the active model. Each section below shows element counts, types, and computed quantities. Click to expand."
                },
                ["MATERIAL_TAKEOFF"] = new List<string>
                {
                    "Here's the material takeoff from your model. Materials are grouped by class with area and volume quantities. Click each section to see individual material details.",
                    "Material takeoff generated. I've calculated areas and volumes for each material across all building elements. Expand the sections for details.",
                    "I've extracted material quantities from the model. The takeoff shows each material's area, volume, and element count. Click sections to expand."
                },
                ["QUERY_MATERIALS"] = new List<string>
                {
                    "Here are the materials used in your model, grouped by material class. Click each section to see area, volume, and which element categories use each material.",
                    "I found the following materials in the model. They're organized by class — expand each section for quantities and usage details.",
                    "Materials overview from the active model. Each section groups materials by class with detailed quantities. Click to expand."
                },
                ["QUERY_PARAMETERS"] = new List<string>
                {
                    "Here are the parameter values from your model elements. I've sampled elements from each category — click to expand and see their parameters.",
                    "Parameter details extracted from the model. Each section shows a sample of elements per category with their parameter values. Click to expand.",
                    "Element parameters from the active model. Expand each category to see parameter names and values for individual elements."
                },

                // Collaboration responses
                ["NEGOTIATE_DESIGN"] = new List<string>
                {
                    "I'll coordinate a design negotiation across specialist agents (Architectural, Structural, MEP, Cost, Sustainability, Safety) to reach consensus on the best approach.",
                    "Starting multi-agent design negotiation. I'll gather input from all specialist agents and work toward a balanced solution.",
                    "Initiating design discussion. The specialist agents will evaluate options and negotiate the optimal design approach."
                },
                ["GET_RECOMMENDATIONS"] = new List<string>
                {
                    "I'll gather recommendations from the specialist agents. Each agent (Architectural, Structural, MEP, Cost, Sustainability, Safety) will provide their expert perspective.",
                    "Requesting specialist recommendations. I'll compile insights from all discipline agents for a comprehensive view.",
                    "Getting agent recommendations. Each specialist will analyze the situation from their domain expertise."
                },
                ["RESOLVE_CONFLICT"] = new List<string>
                {
                    "I'll analyze the design conflict and propose resolution strategies. This may involve trade-offs between disciplines to find the best compromise.",
                    "Working on conflict resolution. I'll evaluate each discipline's constraints and find a balanced solution.",
                    "Initiating conflict resolution process. I'll identify the root cause and propose strategies to resolve the disagreement."
                },
                ["COLLABORATE"] = new List<string>
                {
                    "I'll coordinate a cross-discipline review. The specialist agents will collaborate to evaluate the design holistically.",
                    "Starting cross-discipline collaboration. I'll bring together architectural, structural, MEP, cost, sustainability, and safety perspectives.",
                    "Initiating team collaboration. All specialist agents will contribute their expertise for a comprehensive review."
                },

                // Phase 6: LAN Collaboration
                ["SETUP_WORKSHARING"] = new List<string>
                {
                    "I'll enable worksharing and create the central model on the LAN server. This sets up 10 default worksets (Architecture, Structure, MEP, Interior, Exterior, Site, FF&E, Shared Levels & Grids, Linked Models, Coordination).",
                    "Setting up worksharing. I'll create the central model on your shared drive and configure worksets for the team.",
                    "Initializing worksharing for the project. I'll enable it, create worksets, and save the central model to the server."
                },
                ["SYNC_MODEL"] = new List<string>
                {
                    "Syncing your local changes to the central model. I'll check for conflicts first and then push your changes.",
                    "I'll synchronize with central. Any conflicts will be flagged before the sync completes.",
                    "Starting sync to central. I'll update the changelog and notify the team when done."
                },
                ["CHECK_WORKSHARING_CONFLICTS"] = new List<string>
                {
                    "I'll check for worksharing conflicts — elements owned by other team members or modified in central since your last sync.",
                    "Running pre-sync conflict analysis. I'll identify any elements that might cause issues when you sync.",
                    "Checking for conflicts with the central model before syncing."
                },
                ["DIAGNOSE_EDIT"] = new List<string>
                {
                    "I'll check why you can't edit that element — it may be owned by another team member or in a closed workset.",
                    "Diagnosing the edit status. I'll check checkout ownership and workset access.",
                    "Let me check the element's ownership and workset status to explain why it can't be edited."
                },
                ["GENERATE_BEP"] = new List<string>
                {
                    "I'll generate an ISO 19650-compliant BIM Execution Plan covering project info, BIM goals, team roles, naming conventions, LOIN matrix, CDE workflow, QA plan, and deliverables.",
                    "Generating BEP (BIM Execution Plan) for the project. This will include 14 sections following ISO 19650 standards.",
                    "Creating the BIM Execution Plan. I'll produce a comprehensive document covering information management, naming conventions, CDE status codes, and quality assurance."
                },
                ["MODEL_HEALTH_CHECK"] = new List<string>
                {
                    "Running a model health check — I'll inspect workset assignments, file size, team sync status, and central model age.",
                    "I'll perform a worksharing diagnostics check on the model to identify any issues.",
                    "Starting model health check. I'll verify worksets, file size, and team synchronization status."
                },
                ["VIEW_CHANGELOG"] = new List<string>
                {
                    "I'll show you the recent changes made by the team.",
                    "Fetching the changelog — here's what the team has been working on.",
                    "Loading recent team activity from the changelog."
                },
                ["VIEW_TEAM"] = new List<string>
                {
                    "I'll show you who's currently online and their last sync status.",
                    "Checking team status — online members and their sync activity.",
                    "Loading team member status from the shared drive."
                },
                ["CREATE_BACKUP"] = new List<string>
                {
                    "I'll create a backup of the central model right now.",
                    "Creating a manual backup of the central model. The last 10 backups are retained.",
                    "Backing up the central model. I'll store it with a timestamp in the _Backup folder."
                },
                ["RESTORE_BACKUP"] = new List<string>
                {
                    "I'll restore the central model from the most recent backup. Warning: all changes since the backup will be lost.",
                    "Restoring from backup. All team members will need to re-sync after the restore completes.",
                    "Starting model restore from backup. This replaces the central model — ensure the team is notified."
                },
                ["LIST_BACKUPS"] = new List<string>
                {
                    "I'll list the available backups of the central model.",
                    "Checking for available backups on the server.",
                    "Loading backup history from the _Backup folder."
                },
                ["START_AUTOSYNC"] = new List<string>
                {
                    "I'll enable auto-sync to keep your local model synchronized with central every 30 minutes.",
                    "Turning on auto-sync. Your model will synchronize with central automatically.",
                    "Auto-sync enabled. I'll sync your changes every 30 minutes."
                },
                ["STOP_AUTOSYNC"] = new List<string>
                {
                    "Auto-sync disabled. Remember to sync manually before leaving.",
                    "I've turned off auto-sync. You'll need to sync to central manually.",
                    "Auto-sync stopped. Don't forget to sync before closing the project."
                },
                ["START_AUTOBACKUP"] = new List<string>
                {
                    "Auto-backup enabled. The central model will be backed up every 2 hours.",
                    "I'll automatically back up the central model every 2 hours, keeping the last 10 backups.",
                    "Auto-backup started. Backups will be created every 2 hours on the server."
                },
                ["STOP_AUTOBACKUP"] = new List<string>
                {
                    "Auto-backup disabled. Create backups manually with 'create backup'.",
                    "Auto-backup stopped. Remember to create manual backups periodically.",
                    "I've turned off auto-backup. You can still create manual backups anytime."
                },
                ["RELINQUISH_ELEMENT"] = new List<string>
                {
                    "I'll release your checked-out elements so other team members can edit them.",
                    "Relinquishing element ownership. Other team members will be able to edit these elements after they sync.",
                    "Releasing your element checkouts. Sync to central to finalize the release."
                },
                ["EXPORT_CHANGELOG"] = new List<string>
                {
                    "I'll export the changelog to a CSV file for your records.",
                    "Exporting the full changelog to CSV format.",
                    "Creating a CSV export of all changelog entries."
                },

                // Phase 7: Budget Design + Exports
                ["BUDGET_DESIGN"] = new List<string>
                {
                    "I'll generate 3 design options (Economy, Standard, Premium) within your budget, each with a full BOQ breakdown and specifications.",
                    "Generating budget-constrained design options. I'll produce Economy, Standard, and Premium tiers with cost comparisons.",
                    "Starting budget design analysis. I'll create 3 options with detailed cost breakdowns and material specifications."
                },
                ["ESTIMATE_COST"] = new List<string>
                {
                    "I'll estimate the construction cost from your model using the regional cost database (East Africa rates).",
                    "Calculating construction cost estimate. I'll analyze all elements and apply regional unit rates.",
                    "Running cost estimation. I'll compute direct costs, preliminaries, contingency, overheads, profit, and VAT."
                },
                ["CHECK_BUDGET"] = new List<string>
                {
                    "I'll check the current model cost against your budget and flag any overruns.",
                    "Running budget check. I'll compare the estimated cost with your target budget.",
                    "Checking budget status. I'll calculate the current cost and remaining budget."
                },
                ["EXPORT_BOQ"] = new List<string>
                {
                    "I'll export a priced Bill of Quantities in CSV format, organized by NRM2 work sections with regional unit rates.",
                    "Generating BOQ export. The file will include element quantities, unit rates, section totals, and a cost summary.",
                    "Exporting the BOQ to CSV. This includes direct costs, preliminaries (12%), contingency (5%), overheads (8%), profit (5%), and VAT (18%)."
                },
                ["EXPORT_COBIE"] = new List<string>
                {
                    "I'll export COBie 2.4 data for FM handover — 18 sheets covering Facility, Floor, Space, Type, Component, System, and more.",
                    "Generating COBie 2.4 export. This produces 18 CSV sheets following NBIMS-US V3 / BS 1192-4:2014.",
                    "Starting COBie export for facilities management handover. I'll validate required fields and flag any missing data."
                },
                ["EXPORT_ROOM_SCHEDULE"] = new List<string>
                {
                    "I'll export a room schedule to CSV with room numbers, names, areas, volumes, and levels.",
                    "Generating room schedule export. The file will include all rooms with their key parameters.",
                    "Exporting room schedule. I'll include room number, name, level, area, perimeter, volume, department, and occupancy."
                },
                ["EXPORT_DOOR_SCHEDULE"] = new List<string>
                {
                    "I'll export a door schedule to CSV with marks, types, dimensions, and fire ratings.",
                    "Generating door schedule export. The file will list all doors with their specifications.",
                    "Exporting door schedule with mark, type, level, width, height, host wall, and fire rating."
                },
                ["EXPORT_WINDOW_SCHEDULE"] = new List<string>
                {
                    "I'll export a window schedule to CSV with marks, types, dimensions, and sill heights.",
                    "Generating window schedule export. The file will list all windows with their specifications.",
                    "Exporting window schedule with mark, type, level, width, height, sill height, and host wall."
                },
                ["IMPORT_PARAMETERS"] = new List<string>
                {
                    "I'll import parameter values from your CSV file. I'll validate the data first and show you a preview before applying.",
                    "Loading parameter import file. I'll check that elements exist and parameters are writable before applying.",
                    "Starting parameter import. The CSV should have an 'ElementId', 'Mark', or 'TypeName' column to identify elements."
                },
                ["VALUE_ENGINEER_BUDGET"] = new List<string>
                {
                    "I'll analyze the model for cost savings — identifying where economy-grade materials or simplified systems can reduce costs.",
                    "Running value engineering analysis. I'll compare current specs against cheaper alternatives and estimate potential savings.",
                    "Starting value engineering. I'll review each category for cost reduction opportunities while maintaining structural integrity."
                },

                // Phase 8: Specialist Systems + Proactive Intelligence
                ["CREATE_DATA_OUTLET"] = new List<string>
                {
                    "I'll specify data outlets for your rooms — Cat6A structured cabling with double faceplates.",
                    "Planning data outlet placement based on room type standards (TIA-568 / ISO 11801).",
                    "Designing structured cabling layout. I'll assign outlet counts per room type."
                },
                ["CREATE_WIFI_AP"] = new List<string>
                {
                    "I'll plan WiFi access point placement for full coverage across the floor.",
                    "Calculating WiFi AP positions based on area, environment type, and coverage radius.",
                    "Designing wireless coverage plan with ceiling-mounted APs and PoE backhaul."
                },
                ["CREATE_SERVER_ROOM"] = new List<string>
                {
                    "I'll design the server room layout with rack placement, power, cooling, and fire suppression.",
                    "Planning server/comms room — I'll calculate power, UPS, cooling, and space requirements.",
                    "Designing server room infrastructure based on rack count and environmental standards."
                },
                ["CREATE_CCTV"] = new List<string>
                {
                    "I'll plan CCTV camera placement with coverage analysis and NVR storage sizing.",
                    "Designing surveillance system — cameras, NVR, cabling, and retention calculations.",
                    "Planning CCTV coverage. I'll recommend camera types, positions, and recording setup."
                },
                ["CREATE_ACCESS_CONTROL"] = new List<string>
                {
                    "I'll specify access control for your doors — reader type, lock, and system integration.",
                    "Designing access control system based on security level requirements.",
                    "Planning door access control with card/biometric readers and audit logging."
                },
                ["CREATE_ALARM_SYSTEM"] = new List<string>
                {
                    "I'll design the intruder alarm system with PIR sensors, door contacts, and alert notifications.",
                    "Planning alarm system — I'll calculate sensor counts and zone layout.",
                    "Designing alarm system with motion detection, keypads, and notification setup."
                },
                ["CREATE_INTERCOM"] = new List<string>
                {
                    "I'll specify the intercom/door entry system with stations and lock release.",
                    "Planning intercom system — external stations, internal monitors, and wiring.",
                    "Designing door entry system with video intercom and electric lock integration."
                },
                ["CREATE_GAS_PIPING"] = new List<string>
                {
                    "I'll design the gas piping layout with pipe sizing, safety valves, and ventilation requirements.",
                    "Planning gas distribution — appliance loads, pipe sizing, and LPG storage requirements.",
                    "Designing gas installation with safety shut-offs, detectors, and compliance checks."
                },
                ["CREATE_GAS_DETECTOR"] = new List<string>
                {
                    "I'll specify gas detection placement based on gas type and room locations.",
                    "Planning gas detector installation — mount heights and linked safety systems.",
                    "Placing gas detectors in kitchens, plant rooms, and gas appliance locations."
                },
                ["GET_DESIGN_ADVICE"] = new List<string>
                {
                    "I'll provide design suggestions tailored to the East African context — passive cooling, solar readiness, water storage, and local materials.",
                    "Generating proactive design advice based on your building type and regional context.",
                    "Here are my recommendations for optimising your design for the tropical climate."
                },
                ["RUN_MODEL_AUDIT"] = new List<string>
                {
                    "I'll run a comprehensive model audit — checking compliance, budget status, and design opportunities.",
                    "Running full model audit across fire safety, accessibility, ventilation, and budget.",
                    "Starting model audit. I'll check Uganda building regulations, UNBS standards, and design best practices."
                },
                ["CHECK_UGANDA_COMPLIANCE"] = new List<string>
                {
                    "I'll check your model against Uganda building regulations — UNBS, fire safety, accessibility, and KCCA requirements.",
                    "Running Uganda compliance check: room sizes, door widths, fire exits, stairs, ventilation, sanitation, and parking.",
                    "Checking compliance with Uganda Building Control Act, UNBS US 319-327, PWD Act, and KCCA regulations."
                },
                ["SET_BUDGET"] = new List<string>
                {
                    "I'll set the project budget and enable budget monitoring with alerts at 80% and 100% thresholds.",
                    "Budget registered. I'll track costs against this target and alert you when thresholds are reached.",
                    "Project budget set. I'll monitor spending and provide warnings before you exceed the limit."
                },

                // Consulting responses
                ["CONSULT_STRUCTURAL"] = new List<string>
                {
                    "Here's structural guidance for your query.",
                    "Based on structural analysis standards, here's my advice."
                },
                ["CONSULT_MEP"] = new List<string>
                {
                    "Here's MEP engineering guidance.",
                    "Based on ASHRAE and mechanical code standards, here's my advice."
                },
                ["CONSULT_COMPLIANCE"] = new List<string>
                {
                    "Here's what the building codes require.",
                    "Based on applicable standards, here's the compliance review."
                },
                ["CONSULT_MATERIALS"] = new List<string>
                {
                    "Here's my material recommendation.",
                    "Based on the materials database and standards, here's my advice."
                },
                ["CONSULT_COST"] = new List<string>
                {
                    "Here's the cost guidance for your query.",
                    "Based on regional cost data, here's the estimate."
                },
                ["CONSULT_SUSTAINABILITY"] = new List<string>
                {
                    "Here's sustainability guidance for your project.",
                    "Based on green building standards, here's my advice."
                },
                ["CONSULT_FIRE_SAFETY"] = new List<string>
                {
                    "Here's the fire safety guidance.",
                    "Based on NFPA and fire code requirements, here's my advice."
                },
                ["CONSULT_ACCESSIBILITY"] = new List<string>
                {
                    "Here's the accessibility guidance.",
                    "Based on ADA and accessibility standards, here's my advice."
                },
                ["CONSULT_ENERGY"] = new List<string>
                {
                    "Here's energy performance guidance.",
                    "Based on ASHRAE 90.1 and thermal standards, here's my advice."
                },
                ["CONSULT_ACOUSTICS"] = new List<string>
                {
                    "Here's acoustics guidance for your design.",
                    "Based on acoustic performance standards, here's my advice."
                },
                ["CONSULT_DAYLIGHTING"] = new List<string>
                {
                    "Here's daylighting guidance.",
                    "Based on daylight design standards, here's my advice."
                },
                ["CONSULT_SITE_PLANNING"] = new List<string>
                {
                    "Here's site planning guidance.",
                    "Based on zoning and site design principles, here's my advice."
                },

                // BIM management responses
                ["MANAGE_DESIGN_ANALYSIS"] = new List<string>
                {
                    "Here's the design analysis with spatial quality and pattern assessment.",
                    "Based on spatial reasoning and pattern recognition, here's my analysis."
                },
                ["MANAGE_OPTIMIZATION"] = new List<string>
                {
                    "Here are the optimization results for your design.",
                    "Using multi-objective optimization, here are the recommendations."
                },
                ["MANAGE_DECISION_SUPPORT"] = new List<string>
                {
                    "Here's the multi-criteria decision analysis.",
                    "Based on weighted evaluation and trade-off analysis, here's the assessment."
                },
                ["MANAGE_VALIDATION"] = new List<string>
                {
                    "Here are the validation results for your design.",
                    "Based on compliance checking and collision detection, here's the report."
                },
                ["MANAGE_DESIGN_PATTERNS"] = new List<string>
                {
                    "Here are the applicable design patterns for your project.",
                    "Based on pattern recognition, here are the best practice recommendations."
                },
                ["MANAGE_PREDICTIVE"] = new List<string>
                {
                    "Here are the predicted next steps based on your workflow.",
                    "Based on design sequence analysis, here are the recommendations."
                },

                ["UNKNOWN"] = new List<string>
                {
                    "I'm not sure how to handle that request. Could you rephrase it? Try commands like 'Create a bedroom', 'Generate a maintenance schedule', or say 'help' to see what I can do.",
                    "I didn't quite understand that. I can help with design, facilities management, compliance, and collaboration. Type 'help' to see all options.",
                    "I'm not sure what you mean. Try something like 'Create a 4x5 meter room', 'Predict equipment failures', or say 'help'."
                },
                ["GENERIC"] = new List<string>
                {
                    "Done.",
                    "Completed.",
                    "Action performed successfully."
                }
            };
        }

        private Dictionary<string, List<string>> InitializeExplanationTemplates()
        {
            return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["CREATE_WALL"] = new List<string>
                {
                    "I created the wall to enclose the space you described.",
                    "This wall defines the boundary for the room."
                },
                ["CREATE_ROOM"] = new List<string>
                {
                    "I set up the room with standard dimensions for a {ROOM_TYPE}.",
                    "The room was created following typical requirements for a {ROOM_TYPE}."
                },
                ["CONSULT_STRUCTURAL"] = new List<string>
                {
                    "This recommendation follows ASCE 7 load criteria and ACI 318 design provisions.",
                    "The structural advice is based on applicable building code standards."
                },
                ["CONSULT_MEP"] = new List<string>
                {
                    "This sizing follows ASHRAE guidelines and applicable mechanical codes.",
                    "The MEP recommendation follows industry standard design criteria."
                },
                ["CONSULT_COMPLIANCE"] = new List<string>
                {
                    "This compliance check references IBC 2021 and applicable local codes.",
                    "The review is based on the building code requirements for this project type."
                },
                ["MANAGE_DESIGN_ANALYSIS"] = new List<string>
                {
                    "This analysis uses spatial reasoning, pattern recognition, and quality metrics.",
                    "The design review covers proportions, circulation, adjacencies, and completeness."
                },
                ["MANAGE_OPTIMIZATION"] = new List<string>
                {
                    "Optimization uses genetic algorithms with 5 objectives and 3 hard constraints.",
                    "Layout improvements are prioritized by impact across efficiency, adjacency, and light."
                },
                ["MANAGE_DECISION_SUPPORT"] = new List<string>
                {
                    "Decisions are analyzed using 18 criteria across cost, performance, sustainability, schedule, quality, and risk.",
                    "Trade-off analysis identifies Pareto-optimal solutions with sensitivity testing."
                },
                ["MANAGE_VALIDATION"] = new List<string>
                {
                    "Validation checks collisions, code compliance (IBC/UKBR/EAC), and adjacency requirements.",
                    "Design validation covers 13 built-in rules across 3 regional code profiles."
                },
                ["MANAGE_DESIGN_PATTERNS"] = new List<string>
                {
                    "11 design patterns are evaluated across residential, circulation, spatial, and efficiency categories.",
                    "Pattern recognition identifies applicable best practices and missing design elements."
                },
                ["MANAGE_PREDICTIVE"] = new List<string>
                {
                    "Predictions use Markov chain modeling of your action sequences and learned patterns.",
                    "Proactive suggestions are based on your workflow history and recognized design sequences."
                },
                ["GENERIC"] = new List<string>
                {
                    "This action was performed as requested.",
                    "I completed this based on your instruction."
                }
            };
        }

        private Dictionary<string, List<string>> InitializeErrorTemplates()
        {
            return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["CREATE_WALL"] = new List<string>
                {
                    "I couldn't create the wall. {ERROR}",
                    "The wall creation failed: {ERROR}"
                },
                ["CREATE_ROOM"] = new List<string>
                {
                    "I couldn't create the room. {ERROR}",
                    "Room creation failed: {ERROR}"
                },
                ["GENERIC"] = new List<string>
                {
                    "I encountered an issue: {ERROR}",
                    "That action couldn't be completed. {ERROR}",
                    "Something went wrong: {ERROR}"
                }
            };
        }

        private Dictionary<string, List<string>> InitializeSuggestionTemplates()
        {
            return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["AFTER_WALL"] = new List<string>
                {
                    "Would you like to add a door or window?",
                    "You can now add openings to this wall."
                },
                ["AFTER_ROOM"] = new List<string>
                {
                    "Would you like to add furniture or fixtures?",
                    "You can now customize this room further."
                }
            };
        }

        #endregion
    }

    /// <summary>
    /// Controls how detailed responses are.
    /// </summary>
    public enum ResponseVerbosity
    {
        Minimal,
        Normal,
        Detailed
    }
}
