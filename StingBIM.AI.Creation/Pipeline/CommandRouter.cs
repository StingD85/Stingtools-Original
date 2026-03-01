// StingBIM.AI.Creation.Pipeline.CommandRouter
// Routes NLP intents to the correct creator via CreationOrchestrator
// v4 Prompt Reference: Section A.0 Architecture — CommandRouter dispatches to CreationOrchestrator

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;
using NLog;
using Elements = StingBIM.AI.Creation.Elements;
using Structural = StingBIM.AI.Creation.Structural;
using Architectural = StingBIM.AI.Creation.Architectural;
using MEP = StingBIM.AI.Creation.MEP;
using StingBIM.AI.Creation.Modification;
using StingBIM.AI.Collaboration.LAN;
using StingBIM.AI.Automation.Budget;
using StingBIM.AI.Automation.Export;
using StingBIM.AI.Automation.Import;
using StingBIM.AI.Automation.Intelligence;
using StingBIM.AI.Automation.Compliance;

namespace StingBIM.AI.Creation.Pipeline
{
    /// <summary>
    /// Routes NLP intents and extracted entities to the correct creation method.
    /// This is the bridge between the NLP pipeline and the Revit creation pipeline.
    ///
    /// NLP Flow:
    /// IntentClassifier → EntityExtractor → ConversationManager → CommandRouter → CreationOrchestrator → Revit
    /// </summary>
    public class CommandRouter
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly CreationOrchestrator _orchestrator;
        private readonly Document _document;
        private readonly ModificationEngine _modificationEngine;
        private readonly BulkOperationsEngine _bulkOpsEngine;

        /// <summary>
        /// Event raised when a creation operation completes — carries the result for the chat panel.
        /// </summary>
        public event EventHandler<CreationCompletedEventArgs> CreationCompleted;

        /// <summary>
        /// Event raised when a modification operation completes.
        /// </summary>
        public event EventHandler<ModificationCompletedEventArgs> ModificationCompleted;

        public CommandRouter(Document document)
        {
            _document = document;
            _orchestrator = new CreationOrchestrator(document);
            _modificationEngine = new ModificationEngine();
            _bulkOpsEngine = new BulkOperationsEngine();
        }

        /// <summary>
        /// Routes an intent with extracted entities to the appropriate creation method.
        /// Returns a formatted result for the chat panel.
        /// </summary>
        public CommandRouterResult Route(string intentType, Dictionary<string, object> entities,
            string originalInput)
        {
            Logger.Info($"Routing intent: {intentType} with {entities?.Count ?? 0} entities");

            try
            {
                switch (intentType?.ToUpperInvariant())
                {
                    case "CREATE_WALL":
                        return HandleCreateWall(entities, originalInput);

                    case "CREATE_FLOOR":
                        return HandleCreateFloor(entities, originalInput);

                    case "CREATE_ROOM":
                        return HandleCreateRoom(entities, originalInput);

                    case "CREATE_HOUSE":
                        return HandleCreateHouse(entities, originalInput);

                    // Phase 2: Structural + remaining architectural
                    case "CREATE_DOOR":
                        return HandleCreateDoor(entities, originalInput);

                    case "CREATE_WINDOW":
                        return HandleCreateWindow(entities, originalInput);

                    case "CREATE_ROOF":
                        return HandleCreateRoof(entities, originalInput);

                    case "CREATE_CEILING":
                        return HandleCreateCeiling(entities, originalInput);

                    case "CREATE_STAIRCASE":
                        return HandleCreateStaircase(entities, originalInput);

                    case "CREATE_COLUMN":
                        return HandleCreateColumn(entities, originalInput);

                    case "CREATE_BEAM":
                        return HandleCreateBeam(entities, originalInput);

                    case "CREATE_FOUNDATION":
                        return HandleCreateFoundation(entities, originalInput);

                    case "CREATE_RAMP":
                        return HandleCreateRamp(entities, originalInput);

                    case "CREATE_RAILING":
                        return HandleCreateRailing(entities, originalInput);

                    case "CREATE_CURTAIN_WALL":
                        return HandleCreateCurtainWall(entities, originalInput);

                    case "CREATE_PARAPET":
                        return HandleCreateParapet(entities, originalInput);

                    // Phase 3: Electrical MEP
                    case "CREATE_LIGHTING":
                        return HandleCreateLighting(entities, originalInput);

                    case "CREATE_OUTLET":
                        return HandleCreateOutlet(entities, originalInput);

                    case "CREATE_SWITCH":
                        return HandleCreateSwitch(entities, originalInput);

                    case "CREATE_DB":
                        return HandleCreateDB(entities, originalInput);

                    case "CREATE_GENERATOR":
                        return HandleCreateGenerator(entities, originalInput);

                    case "CREATE_CONDUIT":
                        return HandleCreateConduit(entities, originalInput);

                    case "CREATE_CABLE_TRAY":
                        return HandleCreateCableTray(entities, originalInput);

                    // Phase 4: HVAC, Plumbing, Fire Protection
                    case "CREATE_HVAC_AC":
                        return HandleCreateHVAC_AC(entities, originalInput);

                    case "CREATE_HVAC_FAN":
                        return HandleCreateHVAC_Fan(entities, originalInput);

                    case "CREATE_HVAC_EXTRACT":
                        return HandleCreateHVAC_Extract(entities, originalInput);

                    case "CREATE_HVAC_HOOD":
                        return HandleCreateHVAC_Hood(entities, originalInput);

                    case "CREATE_PLUMBING":
                        return HandleCreatePlumbing(entities, originalInput);

                    case "CREATE_PLUMBING_CW":
                        return HandleCreatePlumbingCW(entities, originalInput);

                    case "CREATE_PLUMBING_WASTE":
                        return HandleCreatePlumbingWaste(entities, originalInput);

                    case "CREATE_PLUMBING_RAIN":
                        return HandleCreatePlumbingRain(entities, originalInput);

                    case "CREATE_FIRE_DETECTOR":
                        return HandleCreateFireDetector(entities, originalInput);

                    case "CREATE_FIRE_SPRINKLER":
                        return HandleCreateFireSprinkler(entities, originalInput);

                    case "CREATE_FIRE_HOSE":
                        return HandleCreateFireHose(entities, originalInput);

                    case "CREATE_FIRE_EXTINGUISHER":
                        return HandleCreateFireExtinguisher(entities, originalInput);

                    case "CREATE_FIRE_ALARM":
                        return HandleCreateFireAlarm(entities, originalInput);

                    // Phase 5: Modification Engine
                    case "MOVE_ELEMENT":
                    case "COPY_ELEMENT":
                    case "DELETE_ELEMENT":
                    case "ROTATE_ELEMENT":
                    case "MIRROR_ELEMENT":
                    case "RESIZE_ELEMENT":
                    case "MODIFY_DIMENSION":
                    case "CHANGE_TYPE":
                    case "SET_PARAMETER":
                    case "SPLIT_ELEMENT":
                    case "EXTEND_ELEMENT":
                    case "OFFSET_ELEMENT":
                    case "LEVEL_ADJUST":
                    case "PIN_ELEMENT":
                    case "UNPIN_ELEMENT":
                    case "COPY_TO_LEVEL":
                        return HandleModification(intentType, entities, originalInput);

                    // Phase 5: Bulk Operations
                    case "ARRAY_ELEMENT":
                    case "ALIGN_ELEMENT":
                    case "DISTRIBUTE_ELEMENT":
                    case "PURGE_UNUSED":
                    case "VALUE_ENGINEER":
                    case "AUTO_TAG":
                    case "RENUMBER_ELEMENT":
                        return HandleBulkOperation(intentType, entities, originalInput);

                    // Phase 7: Budget Design + Exports
                    case "BUDGET_DESIGN":
                    case "ESTIMATE_COST":
                    case "CHECK_BUDGET":
                    case "EXPORT_BOQ":
                    case "EXPORT_COBIE":
                    case "EXPORT_ROOM_SCHEDULE":
                    case "EXPORT_DOOR_SCHEDULE":
                    case "EXPORT_WINDOW_SCHEDULE":
                    case "IMPORT_PARAMETERS":
                    case "VALUE_ENGINEER_BUDGET":
                        return HandleBudgetExport(intentType, entities, originalInput);

                    // Phase 8: Specialist Systems + Proactive Intelligence
                    case "CREATE_DATA_OUTLET":
                    case "CREATE_WIFI_AP":
                    case "CREATE_SERVER_ROOM":
                    case "CREATE_CCTV":
                    case "CREATE_ACCESS_CONTROL":
                    case "CREATE_ALARM_SYSTEM":
                    case "CREATE_INTERCOM":
                    case "CREATE_GAS_PIPING":
                    case "CREATE_GAS_DETECTOR":
                    case "CREATE_SOLAR":
                    case "CREATE_EV_CHARGER":
                    case "GET_DESIGN_ADVICE":
                    case "RUN_MODEL_AUDIT":
                    case "CHECK_UGANDA_COMPLIANCE":
                    case "SET_BUDGET":
                        return HandleSpecialist(intentType, entities, originalInput);

                    // Phase 6: LAN Collaboration
                    case "SETUP_WORKSHARING":
                    case "SYNC_MODEL":
                    case "CHECK_WORKSHARING_CONFLICTS":
                    case "DIAGNOSE_EDIT":
                    case "GENERATE_BEP":
                    case "MODEL_HEALTH_CHECK":
                    case "VIEW_CHANGELOG":
                    case "VIEW_TEAM":
                    case "CREATE_BACKUP":
                    case "RESTORE_BACKUP":
                    case "LIST_BACKUPS":
                    case "START_AUTOSYNC":
                    case "STOP_AUTOSYNC":
                    case "START_AUTOBACKUP":
                    case "STOP_AUTOBACKUP":
                    case "RELINQUISH_ELEMENT":
                    case "EXPORT_CHANGELOG":
                        return HandleCollaboration(intentType, entities, originalInput);

                    default:
                        return CommandRouterResult.NotHandled(
                            $"Intent '{intentType}' is not a creation or modification command.");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"CommandRouter failed for intent: {intentType}");
                return CommandRouterResult.Failed(
                    ErrorExplainer.FormatCreationError("element", "process", ex));
            }
        }

        /// <summary>
        /// Checks if an intent type is a creation command that this router can handle.
        /// </summary>
        public static bool IsCreationIntent(string intentType)
        {
            var upper = intentType?.ToUpperInvariant() ?? "";
            return upper.StartsWith("CREATE_") ||
                   upper == "CREATE_WALL" || upper == "CREATE_FLOOR" ||
                   upper == "CREATE_ROOM" || upper == "CREATE_HOUSE";
        }

        /// <summary>
        /// Checks if an intent type is a modification or bulk command this router handles.
        /// </summary>
        public static bool IsModificationIntent(string intentType)
        {
            return ModificationEngine.IsModificationIntent(intentType) ||
                   BulkOperationsEngine.IsBulkIntent(intentType);
        }

        /// <summary>
        /// Checks if an intent type is a budget/export command.
        /// </summary>
        public static bool IsBudgetExportIntent(string intentType)
        {
            var upper = intentType?.ToUpperInvariant() ?? "";
            return upper == "BUDGET_DESIGN" || upper == "ESTIMATE_COST" ||
                   upper == "CHECK_BUDGET" || upper == "EXPORT_BOQ" ||
                   upper == "EXPORT_COBIE" || upper == "EXPORT_ROOM_SCHEDULE" ||
                   upper == "EXPORT_DOOR_SCHEDULE" || upper == "EXPORT_WINDOW_SCHEDULE" ||
                   upper == "IMPORT_PARAMETERS" || upper == "VALUE_ENGINEER_BUDGET";
        }

        /// <summary>
        /// Checks if an intent type is a LAN collaboration command.
        /// </summary>
        public static bool IsCollaborationIntent(string intentType)
        {
            var upper = intentType?.ToUpperInvariant() ?? "";
            return upper == "SETUP_WORKSHARING" || upper == "SYNC_MODEL" ||
                   upper == "CHECK_WORKSHARING_CONFLICTS" || upper == "DIAGNOSE_EDIT" ||
                   upper == "GENERATE_BEP" || upper == "MODEL_HEALTH_CHECK" ||
                   upper == "VIEW_CHANGELOG" || upper == "VIEW_TEAM" ||
                   upper == "CREATE_BACKUP" || upper == "RESTORE_BACKUP" ||
                   upper == "LIST_BACKUPS" || upper == "START_AUTOSYNC" ||
                   upper == "STOP_AUTOSYNC" || upper == "START_AUTOBACKUP" ||
                   upper == "STOP_AUTOBACKUP" || upper == "RELINQUISH_ELEMENT" ||
                   upper == "EXPORT_CHANGELOG";
        }

        /// <summary>
        /// Checks if an intent type is a Phase 8 specialist/proactive command.
        /// </summary>
        public static bool IsSpecialistIntent(string intentType)
        {
            var upper = intentType?.ToUpperInvariant() ?? "";
            return upper == "CREATE_DATA_OUTLET" || upper == "CREATE_WIFI_AP" ||
                   upper == "CREATE_SERVER_ROOM" || upper == "CREATE_CCTV" ||
                   upper == "CREATE_ACCESS_CONTROL" || upper == "CREATE_ALARM_SYSTEM" ||
                   upper == "CREATE_INTERCOM" || upper == "CREATE_GAS_PIPING" ||
                   upper == "CREATE_GAS_DETECTOR" || upper == "CREATE_SOLAR" ||
                   upper == "CREATE_EV_CHARGER" || upper == "GET_DESIGN_ADVICE" ||
                   upper == "RUN_MODEL_AUDIT" || upper == "CHECK_UGANDA_COMPLIANCE" ||
                   upper == "SET_BUDGET";
        }

        /// <summary>
        /// Checks if any intent handled by this router (creation, modification, bulk, collaboration, specialist).
        /// </summary>
        public static bool CanHandle(string intentType)
        {
            return IsCreationIntent(intentType) || IsModificationIntent(intentType) ||
                   IsCollaborationIntent(intentType) || IsBudgetExportIntent(intentType) ||
                   IsSpecialistIntent(intentType);
        }

        #region Phase 5: Modification Handlers

        private CommandRouterResult HandleModification(string intentType,
            Dictionary<string, object> entities, string input)
        {
            Logger.Info($"Routing modification: {intentType}");

            var result = _modificationEngine.RouteIntent(_document, intentType, entities, input);
            OnModificationCompleted(result);

            return new CommandRouterResult
            {
                Handled = true,
                Success = result.Success,
                Message = result.FormatForChat(),
                Error = result.Error,
                Suggestions = result.Suggestions,
                ElementsCreated = result.AffectedCount
            };
        }

        private CommandRouterResult HandleBulkOperation(string intentType,
            Dictionary<string, object> entities, string input)
        {
            Logger.Info($"Routing bulk operation: {intentType}");

            var result = _bulkOpsEngine.RouteIntent(_document, intentType, entities, input);
            OnModificationCompleted(result);

            return new CommandRouterResult
            {
                Handled = true,
                Success = result.Success,
                Message = result.FormatForChat(),
                Error = result.Error,
                Suggestions = result.Suggestions,
                ElementsCreated = result.AffectedCount
            };
        }

        private void OnModificationCompleted(ModificationResult result)
        {
            ModificationCompleted?.Invoke(this, new ModificationCompletedEventArgs(result));
        }

        #endregion

        #region Intent Handlers

        private CommandRouterResult HandleCreateWall(Dictionary<string, object> entities, string input)
        {
            // Parse dimensions from entities
            var lengthMm = ExtractDimension(entities, "length", "width");
            var heightMm = ExtractDimension(entities, "height");
            var thicknessMm = ExtractDimension(entities, "thickness");
            var wallType = ExtractString(entities, "wallType", "material");
            var level = ExtractString(entities, "level");
            var isStructural = input.IndexOf("structural", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               input.IndexOf("load bearing", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               input.IndexOf("load-bearing", StringComparison.OrdinalIgnoreCase) >= 0;

            // Check if this is a rectangular room outline request
            var depthMm = ExtractDimension(entities, "depth");
            var roomType = ExtractString(entities, "roomType");

            if (depthMm > 0 || HasRoomDimensions(input))
            {
                // "Create a 5×4m bedroom" → rectangular walls
                var dims = ParseRoomDimensions(input);
                if (dims.HasValue)
                {
                    lengthMm = dims.Value.widthMm;
                    depthMm = dims.Value.depthMm;
                }

                var rectCmd = new RectangularWallCommand
                {
                    WidthMm = lengthMm > 0 ? lengthMm : 4000,
                    DepthMm = depthMm > 0 ? depthMm : 3000,
                    HeightMm = heightMm,
                    ThicknessMm = thicknessMm,
                    WallTypeName = wallType,
                    LevelName = level,
                    RoomType = roomType ?? ExtractRoomType(input),
                    IsStructural = isStructural
                };

                var rectResult = _orchestrator.CreateRectangularWalls(rectCmd);
                OnCreationCompleted(rectResult);
                return CommandRouterResult.FromPipelineResult(rectResult);
            }

            // Single wall
            if (lengthMm <= 0)
            {
                // Try to extract from natural language: "5 meter wall", "3m wall"
                lengthMm = ExtractLengthFromText(input);
            }

            if (lengthMm <= 0)
            {
                return CommandRouterResult.NeedsClarification(
                    "What length should the wall be?",
                    new List<string> { "3 meters", "5 meters", "8 meters" });
            }

            var cmd = new WallCreationCommand
            {
                LengthMm = lengthMm,
                HeightMm = heightMm,
                ThicknessMm = thicknessMm,
                WallTypeName = wallType,
                LevelName = level,
                IsStructural = isStructural
            };

            var result = _orchestrator.CreateWall(cmd);
            OnCreationCompleted(result);
            return CommandRouterResult.FromPipelineResult(result);
        }

        private CommandRouterResult HandleCreateFloor(Dictionary<string, object> entities, string input)
        {
            var widthMm = ExtractDimension(entities, "width", "length");
            var depthMm = ExtractDimension(entities, "depth");
            var floorType = ExtractString(entities, "floorType", "material");
            var level = ExtractString(entities, "level");

            if (widthMm <= 0 || depthMm <= 0)
            {
                var dims = ParseRoomDimensions(input);
                if (dims.HasValue)
                {
                    widthMm = dims.Value.widthMm;
                    depthMm = dims.Value.depthMm;
                }
            }

            if (widthMm <= 0 || depthMm <= 0)
            {
                return CommandRouterResult.NeedsClarification(
                    "What are the floor dimensions?",
                    new List<string> { "4×5 meters", "5×6 meters", "3×4 meters" });
            }

            var cmd = new FloorCreationCommand
            {
                WidthMm = widthMm,
                DepthMm = depthMm,
                FloorTypeName = floorType,
                LevelName = level
            };

            var result = _orchestrator.CreateFloor(cmd);
            OnCreationCompleted(result);
            return CommandRouterResult.FromPipelineResult(result);
        }

        private CommandRouterResult HandleCreateRoom(Dictionary<string, object> entities, string input)
        {
            var widthMm = ExtractDimension(entities, "width", "length");
            var depthMm = ExtractDimension(entities, "depth");
            var roomType = ExtractString(entities, "roomType") ?? ExtractRoomType(input);
            var level = ExtractString(entities, "level");

            if (widthMm <= 0 || depthMm <= 0)
            {
                var dims = ParseRoomDimensions(input);
                if (dims.HasValue)
                {
                    widthMm = dims.Value.widthMm;
                    depthMm = dims.Value.depthMm;
                }
            }

            // Use room type defaults if no dimensions given
            if (widthMm <= 0 || depthMm <= 0)
            {
                var defaults = GetRoomTypeDefaults(roomType);
                if (defaults.HasValue)
                {
                    widthMm = defaults.Value.widthMm;
                    depthMm = defaults.Value.depthMm;
                }
                else
                {
                    return CommandRouterResult.NeedsClarification(
                        $"What size should the {roomType ?? "room"} be?",
                        new List<string> { "3×4 meters", "4×5 meters", "5×6 meters" });
                }
            }

            // Step 1: Create walls
            var wallCmd = new RectangularWallCommand
            {
                WidthMm = widthMm,
                DepthMm = depthMm,
                LevelName = level,
                RoomType = roomType
            };

            var wallResult = _orchestrator.CreateRectangularWalls(wallCmd);
            if (!wallResult.Success)
            {
                OnCreationCompleted(wallResult);
                return CommandRouterResult.FromPipelineResult(wallResult);
            }

            // Step 2: Place room in the enclosed area
            var roomCmd = new RoomPlacementCommand
            {
                RoomName = FormatRoomName(roomType),
                LevelName = level,
                CenterX = widthMm / 2,
                CenterY = depthMm / 2
            };

            var roomResult = _orchestrator.PlaceRoom(roomCmd);

            // Combine results
            var combinedResult = new CreationPipelineResult
            {
                Success = wallResult.Success,
                ElementType = "Room",
                CreatedElementIds = wallResult.CreatedElementIds,
                Message = $"Created {FormatRoomName(roomType)}: " +
                    $"{widthMm / 1000:F1}m x {depthMm / 1000:F1}m " +
                    $"({widthMm / 1000.0 * depthMm / 1000.0:F1}m²)",
                CostEstimate = wallResult.CostEstimate,
                Suggestions = new List<string>
                {
                    "Add a door",
                    "Add windows",
                    "Add a floor",
                    "Create the next room"
                }
            };

            if (roomResult.Success)
            {
                combinedResult.Message += $"\nRoom '{roomCmd.RoomName}' placed and tagged.";
            }

            OnCreationCompleted(combinedResult);
            return CommandRouterResult.FromPipelineResult(combinedResult);
        }

        private CommandRouterResult HandleCreateHouse(Dictionary<string, object> entities, string input)
        {
            // "Create a 3 bedroom house" → create multiple rooms
            var bedroomCount = ExtractNumber(input, @"(\d+)\s*bed");
            if (bedroomCount <= 0) bedroomCount = 3;

            var rooms = new List<string>();
            rooms.Add("Living Room (5×6m)");
            rooms.Add("Kitchen (3×4m)");
            rooms.Add("Dining Room (3.5×4m)");
            for (int i = 1; i <= bedroomCount; i++)
                rooms.Add($"Bedroom {i} (3.5×4m)");
            rooms.Add($"Bathroom (2×2.5m)");
            if (bedroomCount >= 3)
                rooms.Add("En-suite (2×2m)");

            var roomList = string.Join("\n  - ", rooms);
            return CommandRouterResult.NeedsClarification(
                $"I'll create a {bedroomCount}-bedroom house with these rooms:\n  - {roomList}\n\n" +
                "Shall I proceed?",
                new List<string> { "Yes, create all rooms", "Modify the room list", "Start with one room" });
        }

        #endregion

        #region Phase 7: Budget & Export Handlers

        private CommandRouterResult HandleBudgetExport(string intentType,
            Dictionary<string, object> entities, string input)
        {
            Logger.Info($"Routing budget/export intent: {intentType}");

            try
            {
                // Default output directory: user's Documents
                var outputDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "StingBIM_Exports");
                if (!Directory.Exists(outputDir))
                    Directory.CreateDirectory(outputDir);

                var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm");

                switch (intentType.ToUpperInvariant())
                {
                    case "BUDGET_DESIGN":
                    {
                        var budget = ExtractDecimal(entities, "budget", input);
                        if (budget <= 0)
                        {
                            return CommandRouterResult.NeedsClarification(
                                "What is your total construction budget? " +
                                "Please provide an amount, e.g. 'Design within 600 million UGX'.",
                                new List<string> { "600M UGX", "1 billion UGX", "200M UGX" });
                        }

                        var program = ExtractBuildingProgram(entities, input);
                        var engine = new BudgetDesignEngine();
                        var result = engine.GenerateOptions(budget, program);

                        return new CommandRouterResult
                        {
                            Handled = true, Success = result.Success,
                            Message = result.Summary,
                            Suggestions = new List<string>
                            {
                                "Export BOQ", "Estimate cost", "Value engineer"
                            }
                        };
                    }

                    case "ESTIMATE_COST":
                    {
                        var engine = new BudgetDesignEngine();
                        var estimate = engine.EstimateModelCost(_document);
                        return new CommandRouterResult
                        {
                            Handled = true, Success = true,
                            Message = estimate.Summary,
                            Suggestions = new List<string>
                            {
                                "Export BOQ", "Budget design", "Value engineer"
                            }
                        };
                    }

                    case "CHECK_BUDGET":
                    {
                        var budget = ExtractDecimal(entities, "budget", input);
                        var engine = new BudgetDesignEngine();
                        var estimate = engine.EstimateModelCost(_document);
                        if (budget > 0)
                        {
                            var alert = engine.CheckBudgetAlert(estimate.GrandTotal, budget);
                            var msg = alert != null
                                ? alert.Message
                                : $"Within budget. Current estimate: {estimate.Currency} {estimate.GrandTotal:N0} " +
                                  $"of {estimate.Currency} {budget:N0} ({(estimate.GrandTotal / budget * 100):F0}%).";
                            return new CommandRouterResult
                            {
                                Handled = true, Success = true, Message = msg,
                                Suggestions = alert?.Suggestions ?? new List<string>
                                {
                                    "Estimate cost", "Export BOQ", "Value engineer"
                                }
                            };
                        }
                        return new CommandRouterResult
                        {
                            Handled = true, Success = true,
                            Message = estimate.Summary,
                            Suggestions = new List<string> { "Export BOQ", "Budget design" }
                        };
                    }

                    case "EXPORT_BOQ":
                    {
                        var path = Path.Combine(outputDir, $"BOQ_{timestamp}.csv");
                        var exporter = new BOQExcelExporter(region: "Uganda");
                        var result = exporter.ExportBOQ(_document, path);
                        return new CommandRouterResult
                        {
                            Handled = true, Success = result.Success,
                            Message = result.Success
                                ? $"BOQ exported to: {result.OutputPath}\n{result.Message}"
                                : result.Error,
                            Suggestions = new List<string>
                            {
                                "Export COBie", "Export room schedule", "Estimate cost"
                            }
                        };
                    }

                    case "EXPORT_COBIE":
                    {
                        var cobieDir = Path.Combine(outputDir, $"COBie_{timestamp}");
                        var exporter = new COBieExporter();
                        var result = exporter.Export(_document, cobieDir);
                        var msg = result.Summary;
                        if (result.Warnings.Count > 0)
                        {
                            msg += "\n\nWarnings:";
                            foreach (var w in result.Warnings)
                                msg += $"\n  [{w.Sheet}] {w.Message}";
                        }
                        return new CommandRouterResult
                        {
                            Handled = true, Success = result.Success,
                            Message = result.Success ? msg : result.Error,
                            Suggestions = new List<string>
                            {
                                "Export BOQ", "Export room schedule", "Model health check"
                            }
                        };
                    }

                    case "EXPORT_ROOM_SCHEDULE":
                    {
                        var path = Path.Combine(outputDir, $"RoomSchedule_{timestamp}.csv");
                        var exporter = new ScheduleExporter();
                        var result = exporter.ExportRoomSchedule(_document, path);
                        return new CommandRouterResult
                        {
                            Handled = true, Success = result.Success,
                            Message = result.Success
                                ? $"Room schedule exported to: {result.OutputPath}\n{result.Message}"
                                : result.Error,
                            Suggestions = new List<string>
                            {
                                "Export door schedule", "Export window schedule", "Export BOQ"
                            }
                        };
                    }

                    case "EXPORT_DOOR_SCHEDULE":
                    {
                        var path = Path.Combine(outputDir, $"DoorSchedule_{timestamp}.csv");
                        var exporter = new ScheduleExporter();
                        var result = exporter.ExportDoorSchedule(_document, path);
                        return new CommandRouterResult
                        {
                            Handled = true, Success = result.Success,
                            Message = result.Success
                                ? $"Door schedule exported to: {result.OutputPath}\n{result.Message}"
                                : result.Error,
                            Suggestions = new List<string>
                            {
                                "Export window schedule", "Export room schedule", "Export BOQ"
                            }
                        };
                    }

                    case "EXPORT_WINDOW_SCHEDULE":
                    {
                        var path = Path.Combine(outputDir, $"WindowSchedule_{timestamp}.csv");
                        var exporter = new ScheduleExporter();
                        var result = exporter.ExportWindowSchedule(_document, path);
                        return new CommandRouterResult
                        {
                            Handled = true, Success = result.Success,
                            Message = result.Success
                                ? $"Window schedule exported to: {result.OutputPath}\n{result.Message}"
                                : result.Error,
                            Suggestions = new List<string>
                            {
                                "Export door schedule", "Export room schedule", "Export BOQ"
                            }
                        };
                    }

                    case "IMPORT_PARAMETERS":
                    {
                        var csvPath = ExtractString(entities, "filePath");
                        if (string.IsNullOrEmpty(csvPath))
                        {
                            return CommandRouterResult.NeedsClarification(
                                "Please provide the path to the CSV file containing parameter values to import.",
                                new List<string> { "Browse for file", "Show import format" });
                        }

                        var importer = new ParameterImporter();
                        var preview = importer.LoadAndValidate(_document, csvPath);
                        if (!preview.IsValid)
                        {
                            return new CommandRouterResult
                            {
                                Handled = true, Success = false,
                                Message = $"Import validation failed:\n{preview.Summary}\n" +
                                    string.Join("\n", preview.Errors),
                                Suggestions = new List<string> { "Show import format", "Try another file" }
                            };
                        }

                        // Auto-apply if valid
                        var result = importer.Apply(_document, preview);
                        return new CommandRouterResult
                        {
                            Handled = true, Success = result.Success,
                            Message = result.Success ? result.Message : result.Error,
                            Suggestions = new List<string>
                            {
                                "Show parameters", "Export room schedule", "Generate BOQ"
                            }
                        };
                    }

                    case "VALUE_ENGINEER_BUDGET":
                    {
                        var budget = ExtractDecimal(entities, "budget", input);
                        var engine = new BudgetDesignEngine();
                        var estimate = engine.EstimateModelCost(_document);

                        if (budget <= 0)
                            budget = estimate.GrandTotal * 0.85m; // Target 15% reduction

                        var alert = engine.CheckBudgetAlert(estimate.GrandTotal, budget);
                        var msg = $"Current estimate: {estimate.Currency} {estimate.GrandTotal:N0}\n" +
                            $"Target budget: {estimate.Currency} {budget:N0}\n\n";

                        if (estimate.GrandTotal > budget)
                        {
                            var overBy = estimate.GrandTotal - budget;
                            msg += $"Over budget by {estimate.Currency} {overBy:N0}.\n\n" +
                                "Value engineering suggestions:\n" +
                                "  1. Use economy-grade wall finishes (save ~10%)\n" +
                                "  2. Reduce MEP scope — natural ventilation where possible (save ~8%)\n" +
                                "  3. Simplify structural system — reduce column sizes (save ~5%)\n" +
                                "  4. Use local materials over imported (save ~12%)\n" +
                                "  5. Reduce window area — smaller openings (save ~3%)";
                        }
                        else
                        {
                            msg += "Currently within budget. No value engineering needed.";
                        }

                        return new CommandRouterResult
                        {
                            Handled = true, Success = true, Message = msg,
                            Suggestions = new List<string>
                            {
                                "Budget design", "Export BOQ", "Estimate cost"
                            }
                        };
                    }

                    default:
                        return CommandRouterResult.NotHandled(
                            $"Budget/export intent '{intentType}' is not yet supported.");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Budget/export command failed: {intentType}");
                return CommandRouterResult.Failed($"Budget/export operation failed: {ex.Message}");
            }
        }

        private decimal ExtractDecimal(Dictionary<string, object> entities, string key, string input)
        {
            // Try from entities first
            if (entities.TryGetValue(key, out var val))
            {
                if (decimal.TryParse(val?.ToString(), out var d)) return d;
            }

            // Parse from input: "600M", "600 million", "1.2 billion", "500000000"
            var lower = input?.ToLowerInvariant() ?? "";
            var match = System.Text.RegularExpressions.Regex.Match(lower,
                @"(\d+(?:\.\d+)?)\s*(?:m(?:illion)?|mil)\b");
            if (match.Success && decimal.TryParse(match.Groups[1].Value, out var millions))
                return millions * 1_000_000;

            match = System.Text.RegularExpressions.Regex.Match(lower,
                @"(\d+(?:\.\d+)?)\s*(?:b(?:illion)?|bil)\b");
            if (match.Success && decimal.TryParse(match.Groups[1].Value, out var billions))
                return billions * 1_000_000_000;

            // Raw number
            match = System.Text.RegularExpressions.Regex.Match(lower, @"(\d{6,})");
            if (match.Success && decimal.TryParse(match.Groups[1].Value, out var raw))
                return raw;

            return 0;
        }

        private BuildingProgram ExtractBuildingProgram(Dictionary<string, object> entities, string input)
        {
            var program = new BuildingProgram();
            var lower = input?.ToLowerInvariant() ?? "";

            // Try to extract bedroom count
            var match = System.Text.RegularExpressions.Regex.Match(lower, @"(\d+)\s*(?:bed|br)\b");
            int bedrooms = match.Success ? int.Parse(match.Groups[1].Value) : 3;

            // Default residential program
            program.BuildingType = "Residential";
            for (int i = 0; i < bedrooms; i++)
            {
                program.Rooms.Add(new RoomBrief
                {
                    RoomType = i == 0 ? "Master Bedroom" : "Bedroom",
                    Name = i == 0 ? "Master Bedroom" : $"Bedroom {i + 1}"
                });
            }
            program.Rooms.Add(new RoomBrief { RoomType = "Living Room", Name = "Living Room" });
            program.Rooms.Add(new RoomBrief { RoomType = "Kitchen", Name = "Kitchen" });
            program.Rooms.Add(new RoomBrief { RoomType = "Dining Room", Name = "Dining Room" });
            for (int i = 0; i < Math.Max(1, bedrooms - 1); i++)
            {
                program.Rooms.Add(new RoomBrief
                {
                    RoomType = "Bathroom",
                    Name = i == 0 ? "Main Bathroom" : $"Bathroom {i + 1}"
                });
            }

            return program;
        }

        #endregion

        #region Phase 6: LAN Collaboration Handlers

        private CommandRouterResult HandleCollaboration(string intentType,
            Dictionary<string, object> entities, string input)
        {
            Logger.Info($"Routing collaboration intent: {intentType}");

            try
            {
                var serverPath = ExtractString(entities, "serverPath") ?? @"\\SERVER\Projects";
                var projectName = ExtractString(entities, "projectName") ?? "StingBIM";

                switch (intentType.ToUpperInvariant())
                {
                    case "SETUP_WORKSHARING":
                    {
                        var collab = new OfflineLANCollaborationManager();
                        var result = collab.SetupWorksharing(_document, serverPath, projectName);
                        return new CommandRouterResult
                        {
                            Handled = true, Success = result.Success,
                            Message = result.Message, Error = result.Error,
                            Suggestions = result.Suggestions
                        };
                    }

                    case "SYNC_MODEL":
                    {
                        var comment = ExtractString(entities, "comment") ?? "";
                        var collab = new OfflineLANCollaborationManager();
                        var result = collab.SyncToCentral(_document, comment);
                        return new CommandRouterResult
                        {
                            Handled = true, Success = result.Success,
                            Message = result.Message, Error = result.Error,
                            Suggestions = result.Suggestions
                        };
                    }

                    case "CHECK_WORKSHARING_CONFLICTS":
                    {
                        var resolver = new ConflictResolver();
                        var analysis = resolver.AnalyzeConflicts(_document);
                        return new CommandRouterResult
                        {
                            Handled = true, Success = true,
                            Message = resolver.FormatForChat(analysis),
                            Suggestions = new List<string>
                            {
                                "Sync to central", "Relinquish elements", "View team status"
                            }
                        };
                    }

                    case "DIAGNOSE_EDIT":
                    {
                        var diag = new WorksharingDiagnostics(serverPath, projectName);
                        var selection = ExtractString(entities, "elementId");
                        if (!string.IsNullOrEmpty(selection) && int.TryParse(selection, out var id))
                        {
                            var msg = diag.DiagnoseEditStatus(_document, new ElementId(id));
                            return new CommandRouterResult
                            {
                                Handled = true, Success = true, Message = msg,
                                Suggestions = new List<string>
                                {
                                    "Sync to central", "Check conflicts", "View team status"
                                }
                            };
                        }
                        return new CommandRouterResult
                        {
                            Handled = true, Success = true,
                            Message = "Select an element in Revit to diagnose why it can't be edited, " +
                                      "then ask me again with the element selected.",
                            Suggestions = new List<string>
                            {
                                "Check conflicts", "Model health check", "View team status"
                            }
                        };
                    }

                    case "GENERATE_BEP":
                    {
                        var bepGen = new BEPGenerator();
                        var result = bepGen.Generate(_document, serverPath, projectName);
                        return new CommandRouterResult
                        {
                            Handled = true, Success = result.Success,
                            Message = result.Success
                                ? $"BIM Execution Plan generated.\nSaved to: {result.OutputPath}\n" +
                                  $"Sections: {result.SectionCount}\n\n" +
                                  "The BEP covers project info, BIM goals, team roles, naming conventions, " +
                                  "LOIN matrix, CDE workflow, QA plan, and deliverables per ISO 19650."
                                : $"BEP generation failed: {result.Error}",
                            Suggestions = new List<string>
                            {
                                "View changelog", "Model health check", "Sync to central"
                            }
                        };
                    }

                    case "MODEL_HEALTH_CHECK":
                    {
                        var diag = new WorksharingDiagnostics(serverPath, projectName);
                        var report = diag.RunHealthCheck(_document);
                        return new CommandRouterResult
                        {
                            Handled = true, Success = true,
                            Message = diag.FormatHealthReport(report),
                            Suggestions = new List<string>
                            {
                                "Create backup", "Sync to central", "Check conflicts"
                            }
                        };
                    }

                    case "VIEW_CHANGELOG":
                    {
                        var logMgr = new ChangeLogManager(serverPath, projectName);
                        return new CommandRouterResult
                        {
                            Handled = true, Success = true,
                            Message = logMgr.FormatForChat(),
                            Suggestions = new List<string>
                            {
                                "Export changelog to CSV", "View team status", "Sync to central"
                            }
                        };
                    }

                    case "VIEW_TEAM":
                    {
                        var collab = new OfflineLANCollaborationManager();
                        var team = collab.GetTeamMembers(serverPath, projectName);
                        if (team.Count == 0)
                        {
                            return new CommandRouterResult
                            {
                                Handled = true, Success = true,
                                Message = "No team members registered yet. " +
                                          "Team members are registered when they first sync to central.",
                                Suggestions = new List<string>
                                {
                                    "Setup worksharing", "Sync to central", "View changelog"
                                }
                            };
                        }
                        var lines = new List<string> { "Team Status:", "─────────────────────────────────────────" };
                        foreach (var m in team)
                        {
                            var status = m.IsOnline ? "Online" : "Offline";
                            var sync = m.LastSync != DateTime.MinValue
                                ? $"Last sync: {m.LastSync:yyyy-MM-dd HH:mm}" : "Never synced";
                            lines.Add($"  {m.UserName} — {status} | {sync}");
                        }
                        lines.Add("─────────────────────────────────────────");
                        return new CommandRouterResult
                        {
                            Handled = true, Success = true,
                            Message = string.Join("\n", lines),
                            Suggestions = new List<string>
                            {
                                "View changelog", "Check conflicts", "Sync to central"
                            }
                        };
                    }

                    case "CREATE_BACKUP":
                    {
                        var diag = new WorksharingDiagnostics(serverPath, projectName);
                        var result = diag.CreateBackup();
                        return new CommandRouterResult
                        {
                            Handled = true, Success = result.Success,
                            Message = result.Message, Error = result.Error,
                            Suggestions = new List<string>
                            {
                                "List backups", "Model health check", "Sync to central"
                            }
                        };
                    }

                    case "RESTORE_BACKUP":
                    {
                        var diag = new WorksharingDiagnostics(serverPath, projectName);
                        var backups = diag.ListBackups();
                        if (backups.Count == 0)
                        {
                            return new CommandRouterResult
                            {
                                Handled = true, Success = false,
                                Message = "No backups available to restore.",
                                Suggestions = new List<string>
                                {
                                    "Create backup", "Model health check"
                                }
                            };
                        }
                        // Restore the most recent backup
                        var latest = backups[0];
                        var result = diag.RestoreFromBackup(latest.FullPath);
                        return new CommandRouterResult
                        {
                            Handled = true, Success = result.Success,
                            Message = result.Message, Error = result.Error,
                            Suggestions = result.Suggestions
                        };
                    }

                    case "LIST_BACKUPS":
                    {
                        var diag = new WorksharingDiagnostics(serverPath, projectName);
                        var backups = diag.ListBackups();
                        if (backups.Count == 0)
                        {
                            return new CommandRouterResult
                            {
                                Handled = true, Success = true,
                                Message = "No backups found.",
                                Suggestions = new List<string> { "Create backup", "Start auto-backup" }
                            };
                        }
                        var lines = new List<string> { "Available Backups:", "─────────────────────────────────────────" };
                        foreach (var b in backups)
                        {
                            lines.Add($"  {b.FileName}  ({b.SizeMB:F1} MB)  {b.CreatedAt:yyyy-MM-dd HH:mm}");
                        }
                        lines.Add("─────────────────────────────────────────");
                        return new CommandRouterResult
                        {
                            Handled = true, Success = true,
                            Message = string.Join("\n", lines),
                            Suggestions = new List<string>
                            {
                                "Restore backup", "Create backup", "Model health check"
                            }
                        };
                    }

                    case "START_AUTOSYNC":
                    {
                        var collab = new OfflineLANCollaborationManager();
                        collab.StartAutoSync(_document);
                        return new CommandRouterResult
                        {
                            Handled = true, Success = true,
                            Message = "Auto-sync enabled. The model will sync to central every 30 minutes.",
                            Suggestions = new List<string>
                            {
                                "Stop auto-sync", "Sync now", "View team status"
                            }
                        };
                    }

                    case "STOP_AUTOSYNC":
                    {
                        var collab = new OfflineLANCollaborationManager();
                        collab.StopAutoSync();
                        return new CommandRouterResult
                        {
                            Handled = true, Success = true,
                            Message = "Auto-sync disabled. You'll need to sync manually.",
                            Suggestions = new List<string>
                            {
                                "Start auto-sync", "Sync to central", "View changelog"
                            }
                        };
                    }

                    case "START_AUTOBACKUP":
                    {
                        var diag = new WorksharingDiagnostics(serverPath, projectName);
                        diag.StartAutoBackup();
                        return new CommandRouterResult
                        {
                            Handled = true, Success = true,
                            Message = "Auto-backup enabled. The central model will be backed up every 2 hours.",
                            Suggestions = new List<string>
                            {
                                "Stop auto-backup", "List backups", "Model health check"
                            }
                        };
                    }

                    case "STOP_AUTOBACKUP":
                    {
                        var diag = new WorksharingDiagnostics(serverPath, projectName);
                        diag.StopAutoBackup();
                        return new CommandRouterResult
                        {
                            Handled = true, Success = true,
                            Message = "Auto-backup disabled.",
                            Suggestions = new List<string>
                            {
                                "Start auto-backup", "Create backup", "Model health check"
                            }
                        };
                    }

                    case "RELINQUISH_ELEMENT":
                    {
                        var resolver = new ConflictResolver();
                        var analysis = resolver.AnalyzeConflicts(_document);
                        if (analysis.BorrowedByMe.Count == 0)
                        {
                            return new CommandRouterResult
                            {
                                Handled = true, Success = true,
                                Message = "You have no elements checked out to relinquish.",
                                Suggestions = new List<string>
                                {
                                    "Check conflicts", "Sync to central"
                                }
                            };
                        }
                        // Relinquish all borrowed elements
                        var relinquished = 0;
                        foreach (var borrowed in analysis.BorrowedByMe)
                        {
                            var result = resolver.RelinquishElement(_document, borrowed.ElementId);
                            if (result.Success) relinquished++;
                        }
                        return new CommandRouterResult
                        {
                            Handled = true, Success = true,
                            Message = $"Relinquished {relinquished} of {analysis.BorrowedByMe.Count} element(s).",
                            Suggestions = new List<string>
                            {
                                "Sync to central", "Check conflicts", "View team status"
                            }
                        };
                    }

                    case "EXPORT_CHANGELOG":
                    {
                        var logMgr = new ChangeLogManager(serverPath, projectName);
                        var csvPath = logMgr.ExportToCsv();
                        return new CommandRouterResult
                        {
                            Handled = true, Success = csvPath != null,
                            Message = csvPath != null
                                ? $"Changelog exported to: {csvPath}"
                                : "No changelog entries to export.",
                            Suggestions = new List<string>
                            {
                                "View changelog", "View team status", "Sync to central"
                            }
                        };
                    }

                    default:
                        return CommandRouterResult.NotHandled(
                            $"Collaboration intent '{intentType}' is not yet supported.");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Collaboration command failed: {intentType}");
                return CommandRouterResult.Failed($"Collaboration operation failed: {ex.Message}");
            }
        }

        #endregion

        #region Phase 2 Intent Handlers

        private CommandRouterResult HandleCreateDoor(Dictionary<string, object> entities, string input)
        {
            var roomName = ExtractString(entities, "roomName", "room");
            var wallSide = ExtractString(entities, "direction", "side");
            var doorType = ExtractString(entities, "doorType");

            // Detect door type from input
            if (string.IsNullOrEmpty(doorType))
            {
                var lower = input.ToLowerInvariant();
                if (lower.Contains("sliding")) doorType = "sliding";
                else if (lower.Contains("fire")) doorType = "fire";
                else if (lower.Contains("double")) doorType = "double";
                else if (lower.Contains("garage")) doorType = "garage";
                else if (lower.Contains("security")) doorType = "security";
                else if (lower.Contains("entrance") || lower.Contains("main")) doorType = "main entrance";
            }

            // Detect wall side from input
            if (string.IsNullOrEmpty(wallSide))
            {
                var lower = input.ToLowerInvariant();
                if (lower.Contains("north")) wallSide = "north";
                else if (lower.Contains("south")) wallSide = "south";
                else if (lower.Contains("east")) wallSide = "east";
                else if (lower.Contains("west")) wallSide = "west";
            }

            var placer = new Elements.DoorWindowPlacer(_orchestrator.FamilyResolver.Document);
            var cmd = new Elements.DoorPlacementCommand
            {
                RoomName = roomName,
                WallSide = wallSide,
                DoorType = doorType,
                WidthMm = ExtractDimension(entities, "width"),
                HeightMm = ExtractDimension(entities, "height"),
            };

            var result = placer.PlaceDoorInWall(cmd);
            OnCreationCompleted(result);
            return CommandRouterResult.FromPipelineResult(result);
        }

        private CommandRouterResult HandleCreateWindow(Dictionary<string, object> entities, string input)
        {
            var roomName = ExtractString(entities, "roomName", "room");
            var wallSide = ExtractString(entities, "direction", "side");
            var windowType = ExtractString(entities, "windowType");

            if (string.IsNullOrEmpty(wallSide))
            {
                var lower = input.ToLowerInvariant();
                if (lower.Contains("north")) wallSide = "north";
                else if (lower.Contains("south")) wallSide = "south";
                else if (lower.Contains("east")) wallSide = "east";
                else if (lower.Contains("west")) wallSide = "west";
            }

            var windowMode = "standard";
            var lower2 = input.ToLowerInvariant();
            if (lower2.Contains("high") || lower2.Contains("bathroom")) windowMode = "high";
            else if (lower2.Contains("floor-to-ceiling") || lower2.Contains("full-height") ||
                     lower2.Contains("full height")) windowMode = "full-height";

            var placer = new Elements.DoorWindowPlacer(_orchestrator.FamilyResolver.Document);
            var cmd = new Elements.WindowPlacementCommand
            {
                RoomName = roomName,
                WallSide = wallSide,
                WindowType = windowType,
                WindowMode = windowMode,
                RoomType = ExtractRoomType(input),
                WidthMm = ExtractDimension(entities, "width"),
                HeightMm = ExtractDimension(entities, "height"),
                SillHeightMm = ExtractDimension(entities, "sill"),
            };

            var result = placer.PlaceWindowInWall(cmd);
            OnCreationCompleted(result);
            return CommandRouterResult.FromPipelineResult(result);
        }

        private CommandRouterResult HandleCreateRoof(Dictionary<string, object> entities, string input)
        {
            var lower = input.ToLowerInvariant();
            var roofMode = "pitched"; // default
            if (lower.Contains("flat")) roofMode = "flat";
            else if (lower.Contains("hip")) roofMode = "hip";
            else if (lower.Contains("mono") || lower.Contains("shed")) roofMode = "mono-pitch";
            else if (lower.Contains("mansard")) roofMode = "mansard";
            else if (lower.Contains("gable")) roofMode = "gable";

            // Extract material
            string material = null;
            if (lower.Contains("iron") || lower.Contains("corrugated")) material = "corrugated iron";
            else if (lower.Contains("alumin")) material = "aluminium";
            else if (lower.Contains("clay")) material = "clay tile";
            else if (lower.Contains("concrete tile")) material = "concrete tile";
            else if (lower.Contains("thatch") || lower.Contains("makuti")) material = "thatch";

            // Extract pitch
            var pitchMatch = System.Text.RegularExpressions.Regex.Match(input,
                @"(\d+)\s*(?:degree|°|deg)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var pitch = pitchMatch.Success ? double.Parse(pitchMatch.Groups[1].Value) : 0;

            var dims = ParseRoomDimensions(input);
            var creator = new Elements.RoofCreator(_orchestrator.FamilyResolver.Document);
            var cmd = new Elements.RoofCreationCommand
            {
                RoofMode = roofMode,
                RoofMaterial = material,
                PitchDegrees = pitch,
                LevelName = ExtractString(entities, "level"),
                WidthMm = dims?.widthMm ?? 0,
                DepthMm = dims?.depthMm ?? 0,
            };

            var result = creator.CreateRoof(cmd);
            OnCreationCompleted(result);
            return CommandRouterResult.FromPipelineResult(result);
        }

        private CommandRouterResult HandleCreateCeiling(Dictionary<string, object> entities, string input)
        {
            var lower = input.ToLowerInvariant();
            var ceilingMode = "flat";
            if (lower.Contains("suspend") || lower.Contains("drop") || lower.Contains("t-bar"))
                ceilingMode = "suspended";
            else if (lower.Contains("coffer")) ceilingMode = "coffered";
            else if (lower.Contains("timber") || lower.Contains("wood") || lower.Contains("slat"))
                ceilingMode = "timber";
            else if (lower.Contains("vault")) ceilingMode = "vaulted";

            var creator = new Elements.CeilingCreator(_orchestrator.FamilyResolver.Document);
            var cmd = new Elements.CeilingCreationCommand
            {
                RoomName = ExtractString(entities, "roomName", "room"),
                RoomType = ExtractRoomType(input),
                CeilingMode = ceilingMode,
                HeightMm = ExtractDimension(entities, "height"),
                LevelName = ExtractString(entities, "level"),
            };

            var result = creator.CreateCeiling(cmd);
            OnCreationCompleted(result);
            return CommandRouterResult.FromPipelineResult(result);
        }

        private CommandRouterResult HandleCreateStaircase(Dictionary<string, object> entities, string input)
        {
            var lower = input.ToLowerInvariant();
            var stairType = "straight";
            if (lower.Contains("l-shape") || lower.Contains("quarter")) stairType = "l-shaped";
            else if (lower.Contains("dog") || lower.Contains("half") || lower.Contains("u-shape"))
                stairType = "dog-leg";
            else if (lower.Contains("spiral")) stairType = "spiral";
            else if (lower.Contains("fire") || lower.Contains("escape") || lower.Contains("external"))
                stairType = "fire escape";

            var widthMatch = System.Text.RegularExpressions.Regex.Match(input,
                @"(\d+\.?\d*)\s*(?:m|mm)\s*wide", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            var creator = new Elements.StaircaseCreator(_orchestrator.FamilyResolver.Document);
            var cmd = new Elements.StairCreationCommand
            {
                StairType = stairType,
                WidthMm = widthMatch.Success
                    ? double.Parse(widthMatch.Groups[1].Value) * (widthMatch.Value.Contains("mm") ? 1 : 1000)
                    : 0,
                BaseLevelName = ExtractString(entities, "level"),
                Usage = lower.Contains("public") ? "public" : "residential",
            };

            var result = creator.CreateStaircase(cmd);
            OnCreationCompleted(result);
            return CommandRouterResult.FromPipelineResult(result);
        }

        private CommandRouterResult HandleCreateColumn(Dictionary<string, object> entities, string input)
        {
            var lower = input.ToLowerInvariant();

            // Check if it's a grid request
            var gridMatch = System.Text.RegularExpressions.Regex.Match(input,
                @"(\d+)\s*[×xX]\s*(\d+)\s*(?:column)?\s*grid",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (gridMatch.Success || lower.Contains("column grid"))
            {
                var countX = gridMatch.Success ? int.Parse(gridMatch.Groups[1].Value) : 4;
                var countY = gridMatch.Success ? int.Parse(gridMatch.Groups[2].Value) : 3;

                var spacingDims = ParseRoomDimensions(input);
                var creator = new Structural.ColumnCreator(_orchestrator.FamilyResolver.Document);
                var gridCmd = new Structural.ColumnGridCommand
                {
                    CountX = countX,
                    CountY = countY,
                    SpacingXMm = spacingDims?.widthMm ?? 5000,
                    SpacingYMm = spacingDims?.depthMm ?? 6000,
                    ColumnWidthMm = ExtractDimension(entities, "width"),
                    ColumnDepthMm = ExtractDimension(entities, "depth"),
                    ColumnType = lower.Contains("steel") ? "steel" : "concrete",
                    BaseLevelName = ExtractString(entities, "level"),
                };

                var gridResult = creator.CreateColumnGrid(gridCmd);
                OnCreationCompleted(gridResult);
                return CommandRouterResult.FromPipelineResult(gridResult);
            }

            // Single column
            var sizeMatch = System.Text.RegularExpressions.Regex.Match(input,
                @"(\d+)\s*[×xX]\s*(\d+)\s*mm",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            var colCreator = new Structural.ColumnCreator(_orchestrator.FamilyResolver.Document);
            var cmd = new Structural.ColumnPlacementCommand
            {
                WidthMm = sizeMatch.Success ? double.Parse(sizeMatch.Groups[1].Value) : 400,
                DepthMm = sizeMatch.Success ? double.Parse(sizeMatch.Groups[2].Value) : 0,
                ColumnType = lower.Contains("steel") ? "steel" : "concrete",
                BaseLevelName = ExtractString(entities, "level"),
                GridIntersection = ExtractString(entities, "grid"),
            };

            var result = colCreator.PlaceColumn(cmd);
            OnCreationCompleted(result);
            return CommandRouterResult.FromPipelineResult(result);
        }

        private CommandRouterResult HandleCreateBeam(Dictionary<string, object> entities, string input)
        {
            var lower = input.ToLowerInvariant();

            // Check if it's a lintel
            if (lower.Contains("lintel"))
            {
                var beamCreator = new Structural.BeamCreator(_orchestrator.FamilyResolver.Document);
                var lintelCmd = new Structural.LintelCommand
                {
                    BeamType = lower.Contains("steel") ? "steel" : "concrete",
                    LevelName = ExtractString(entities, "level"),
                };
                var lintelResult = beamCreator.CreateLintel(lintelCmd);
                OnCreationCompleted(lintelResult);
                return CommandRouterResult.FromPipelineResult(lintelResult);
            }

            // Check for "beams between columns"
            if (lower.Contains("between") && lower.Contains("column"))
            {
                var beamCreator = new Structural.BeamCreator(_orchestrator.FamilyResolver.Document);
                var result = beamCreator.CreateBeamsBetweenColumns(
                    ExtractString(entities, "level"),
                    ExtractDimension(entities, "width"),
                    ExtractDimension(entities, "depth"),
                    lower.Contains("steel") ? "steel" : "concrete");
                OnCreationCompleted(result);
                return CommandRouterResult.FromPipelineResult(result);
            }

            // Single beam
            var spanMatch = System.Text.RegularExpressions.Regex.Match(input,
                @"(\d+\.?\d*)\s*(?:m|meter)\s*(?:span|long)?",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            var creator = new Structural.BeamCreator(_orchestrator.FamilyResolver.Document);
            var cmd = new Structural.BeamPlacementCommand
            {
                SpanMm = spanMatch.Success ? double.Parse(spanMatch.Groups[1].Value) * 1000 : 0,
                WidthMm = ExtractDimension(entities, "width"),
                DepthMm = ExtractDimension(entities, "depth"),
                BeamType = lower.Contains("steel") ? "steel" : "concrete",
                LevelName = ExtractString(entities, "level"),
            };

            if (cmd.SpanMm <= 0)
            {
                return CommandRouterResult.NeedsClarification(
                    "What span should the beam have?",
                    new List<string> { "4 meters", "6 meters", "8 meters" });
            }

            var beamResult = creator.PlaceBeam(cmd);
            OnCreationCompleted(beamResult);
            return CommandRouterResult.FromPipelineResult(beamResult);
        }

        private CommandRouterResult HandleCreateFoundation(Dictionary<string, object> entities, string input)
        {
            var lower = input.ToLowerInvariant();
            var soilType = ExtractString(entities, "soilType");

            // Detect soil from input
            if (string.IsNullOrEmpty(soilType))
            {
                if (lower.Contains("murram") || lower.Contains("laterite")) soilType = "murram";
                else if (lower.Contains("black cotton") || lower.Contains("clay")) soilType = "clay";
                else if (lower.Contains("sand")) soilType = "sandy";
                else if (lower.Contains("rock")) soilType = "rocky";
            }

            if (lower.Contains("pad") || lower.Contains("column"))
            {
                var creator = new Structural.FoundationCreator(_orchestrator.FamilyResolver.Document);
                var padCmd = new Structural.PadFoundationCommand
                {
                    SoilType = soilType,
                    LevelName = ExtractString(entities, "level"),
                };
                var padResult = creator.CreatePadFoundations(padCmd);
                OnCreationCompleted(padResult);
                return CommandRouterResult.FromPipelineResult(padResult);
            }

            // Default: strip foundation
            var stripCreator = new Structural.FoundationCreator(_orchestrator.FamilyResolver.Document);
            var stripCmd = new Structural.StripFoundationCommand
            {
                SoilType = soilType,
                LevelName = ExtractString(entities, "level"),
                AllLoadBearingWalls = true,
            };
            var stripResult = stripCreator.CreateStripFoundation(stripCmd);
            OnCreationCompleted(stripResult);
            return CommandRouterResult.FromPipelineResult(stripResult);
        }

        private CommandRouterResult HandleCreateRamp(Dictionary<string, object> entities, string input)
        {
            var heightMatch = System.Text.RegularExpressions.Regex.Match(input,
                @"(\d+\.?\d*)\s*(?:mm|m)\s*(?:height|high|change|rise)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var gradientMatch = System.Text.RegularExpressions.Regex.Match(input,
                @"1\s*:\s*(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            var creator = new Architectural.RampCreator(_orchestrator.FamilyResolver.Document);
            var cmd = new Architectural.RampCreationCommand
            {
                HeightChangeMm = heightMatch.Success
                    ? double.Parse(heightMatch.Groups[1].Value) * (heightMatch.Value.Contains("mm") ? 1 : 1000)
                    : 300,
                Gradient = gradientMatch.Success ? 1.0 / double.Parse(gradientMatch.Groups[1].Value) : 0,
                WidthMm = ExtractDimension(entities, "width"),
                LevelName = ExtractString(entities, "level"),
            };

            var result = creator.CreateRamp(cmd);
            OnCreationCompleted(result);
            return CommandRouterResult.FromPipelineResult(result);
        }

        private CommandRouterResult HandleCreateRailing(Dictionary<string, object> entities, string input)
        {
            var lower = input.ToLowerInvariant();
            var location = "default";
            if (lower.Contains("balcony")) location = "balcony";
            else if (lower.Contains("stair")) location = "stair";
            else if (lower.Contains("ramp")) location = "ramp";
            else if (lower.Contains("terrace")) location = "terrace";

            var lengthMatch = System.Text.RegularExpressions.Regex.Match(input,
                @"(\d+\.?\d*)\s*(?:m|meter)\s*(?:long|railing)?",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            var creator = new Architectural.RailingCreator(_orchestrator.FamilyResolver.Document);
            var cmd = new Architectural.RailingCreationCommand
            {
                RailingLocation = location,
                RailingType = ExtractString(entities, "railingType"),
                LengthMm = lengthMatch.Success ? double.Parse(lengthMatch.Groups[1].Value) * 1000 : 0,
                HeightMm = ExtractDimension(entities, "height"),
                LevelName = ExtractString(entities, "level"),
            };

            if (cmd.LengthMm <= 0)
            {
                return CommandRouterResult.NeedsClarification(
                    "How long should the railing be?",
                    new List<string> { "3 meters", "5 meters", "10 meters" });
            }

            var result = creator.CreateRailing(cmd);
            OnCreationCompleted(result);
            return CommandRouterResult.FromPipelineResult(result);
        }

        private CommandRouterResult HandleCreateCurtainWall(Dictionary<string, object> entities, string input)
        {
            var dims = ParseRoomDimensions(input);
            var lengthMatch = System.Text.RegularExpressions.Regex.Match(input,
                @"(\d+\.?\d*)\s*(?:m|meter)\s*(?:long|curtain)?",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            var creator = new Architectural.CurtainWallCreator(_orchestrator.FamilyResolver.Document);
            var cmd = new Architectural.CurtainWallCommand
            {
                LengthMm = dims?.widthMm ?? (lengthMatch.Success
                    ? double.Parse(lengthMatch.Groups[1].Value) * 1000 : 5000),
                HeightMm = dims?.depthMm ?? ExtractDimension(entities, "height"),
                LevelName = ExtractString(entities, "level"),
            };

            var result = creator.CreateCurtainWall(cmd);
            OnCreationCompleted(result);
            return CommandRouterResult.FromPipelineResult(result);
        }

        private CommandRouterResult HandleCreateParapet(Dictionary<string, object> entities, string input)
        {
            var dims = ParseRoomDimensions(input);

            var creator = new Architectural.ParapetCreator(_orchestrator.FamilyResolver.Document);
            var cmd = new Architectural.ParapetCreationCommand
            {
                HeightMm = ExtractDimension(entities, "height"),
                LevelName = ExtractString(entities, "level"),
                WidthMm = dims?.widthMm ?? 0,
                DepthMm = dims?.depthMm ?? 0,
            };

            var result = creator.CreateParapet(cmd);
            OnCreationCompleted(result);
            return CommandRouterResult.FromPipelineResult(result);
        }

        #endregion

        #region Phase 3 Intent Handlers — Electrical MEP

        private CommandRouterResult HandleCreateLighting(Dictionary<string, object> entities, string input)
        {
            var lower = input.ToLowerInvariant();
            var lightType = "downlight"; // default
            if (lower.Contains("fluorescent") || lower.Contains("tube")) lightType = "fluorescent";
            else if (lower.Contains("pendant")) lightType = "pendant";
            else if (lower.Contains("recessed")) lightType = "recessed";
            else if (lower.Contains("floodlight") || lower.Contains("flood")) lightType = "floodlight";
            else if (lower.Contains("led")) lightType = "LED downlight";

            var allRooms = lower.Contains("all room") || lower.Contains("every room") ||
                           lower.Contains("all bedroom") || lower.Contains("whole");
            var roomName = ExtractString(entities, "roomName", "room");
            var roomType = ExtractRoomType(input);

            var creator = new MEP.ElectricalCreator(_orchestrator.FamilyResolver.Document);

            if (allRooms)
            {
                var result = creator.PlaceLightsAllRooms(
                    ExtractString(entities, "level"), lightType);
                OnCreationCompleted(result);
                return CommandRouterResult.FromPipelineResult(result);
            }

            var cmd = new MEP.LightingCommand
            {
                RoomName = roomName,
                RoomType = roomType,
                LightType = lightType,
                LevelName = ExtractString(entities, "level"),
                AllRooms = allRooms,
            };

            // Extract LUX override if specified
            var luxMatch = Regex.Match(input, @"(\d+)\s*lux", RegexOptions.IgnoreCase);
            if (luxMatch.Success)
                cmd.LuxOverride = int.Parse(luxMatch.Groups[1].Value);

            var lightResult = creator.PlaceLightsInRoom(cmd);
            OnCreationCompleted(lightResult);
            return CommandRouterResult.FromPipelineResult(lightResult);
        }

        private CommandRouterResult HandleCreateOutlet(Dictionary<string, object> entities, string input)
        {
            var lower = input.ToLowerInvariant();
            var roomName = ExtractString(entities, "roomName", "room");
            var roomType = ExtractRoomType(input);
            var isWorktop = lower.Contains("worktop") || lower.Contains("counter") ||
                            lower.Contains("benchtop");

            var allRooms = lower.Contains("all room") || lower.Contains("every room");

            // Extract outlets per wall
            var perWallMatch = Regex.Match(input, @"(\d+)\s*per\s*wall", RegexOptions.IgnoreCase);
            var outletsPerWall = perWallMatch.Success ? int.Parse(perWallMatch.Groups[1].Value) : 2;

            // Extract height
            var heightMatch = Regex.Match(input, @"(?:at\s+)?(\d+\.?\d*)\s*(?:mm|m)\s*(?:height|high|aff)?",
                RegexOptions.IgnoreCase);
            var heightMm = heightMatch.Success
                ? double.Parse(heightMatch.Groups[1].Value) * (heightMatch.Value.Contains("mm") ? 1 : 1000)
                : 0;

            var creator = new MEP.ElectricalCreator(_orchestrator.FamilyResolver.Document);
            var cmd = new MEP.OutletCommand
            {
                RoomName = roomName,
                RoomType = roomType ?? (lower.Contains("bathroom") ? "Bathroom"
                    : lower.Contains("kitchen") ? "Kitchen" : null),
                LevelName = ExtractString(entities, "level"),
                HeightMm = heightMm,
                OutletsPerWall = outletsPerWall,
                WorktopOutlets = isWorktop,
                AllRooms = allRooms,
            };

            var result = creator.PlaceOutlets(cmd);
            OnCreationCompleted(result);
            return CommandRouterResult.FromPipelineResult(result);
        }

        private CommandRouterResult HandleCreateSwitch(Dictionary<string, object> entities, string input)
        {
            var lower = input.ToLowerInvariant();
            var roomName = ExtractString(entities, "roomName", "room");
            var allRooms = lower.Contains("all room") || lower.Contains("every room") ||
                           lower.Contains("every door") || lower.Contains("all door");

            var creator = new MEP.ElectricalCreator(_orchestrator.FamilyResolver.Document);
            var cmd = new MEP.SwitchCommand
            {
                RoomName = roomName,
                LevelName = ExtractString(entities, "level"),
                TwoWay = lower.Contains("two way") || lower.Contains("2-way") || lower.Contains("2 way"),
                Dimmer = lower.Contains("dimmer"),
                AllRooms = allRooms,
            };

            var result = creator.PlaceSwitches(cmd);
            OnCreationCompleted(result);
            return CommandRouterResult.FromPipelineResult(result);
        }

        private CommandRouterResult HandleCreateDB(Dictionary<string, object> entities, string input)
        {
            var lower = input.ToLowerInvariant();

            // Extract ways
            var waysMatch = Regex.Match(input, @"(\d+)\s*(?:-\s*)?way", RegexOptions.IgnoreCase);
            var ways = waysMatch.Success ? int.Parse(waysMatch.Groups[1].Value) : 0;

            // Extract rating
            var ratingMatch = Regex.Match(input, @"(\d+)\s*[aA](?:mp)?", RegexOptions.IgnoreCase);
            var rating = ratingMatch.Success ? double.Parse(ratingMatch.Groups[1].Value) : 100;

            var isCommercial = lower.Contains("commercial") || lower.Contains("industrial");
            var phases = lower.Contains("3-phase") || lower.Contains("three phase") ||
                         lower.Contains("3 phase") ? 3 : 1;

            var creator = new MEP.ElectricalCreator(_orchestrator.FamilyResolver.Document);
            var cmd = new MEP.DistributionBoardCommand
            {
                LevelName = ExtractString(entities, "level"),
                RoomName = ExtractString(entities, "roomName", "room"),
                Ways = ways,
                RatingAmps = rating,
                Phases = phases,
                IsCommercial = isCommercial,
                IsOutdoor = lower.Contains("outdoor") || lower.Contains("external"),
            };

            var result = creator.PlaceDistributionBoard(cmd);
            OnCreationCompleted(result);
            return CommandRouterResult.FromPipelineResult(result);
        }

        private CommandRouterResult HandleCreateGenerator(Dictionary<string, object> entities, string input)
        {
            var lower = input.ToLowerInvariant();

            // Extract kVA
            var kvaMatch = Regex.Match(input, @"(\d+\.?\d*)\s*kva", RegexOptions.IgnoreCase);
            var kva = kvaMatch.Success ? double.Parse(kvaMatch.Groups[1].Value) : 0;

            var creator = new MEP.ElectricalCreator(_orchestrator.FamilyResolver.Document);
            var cmd = new MEP.GeneratorCommand
            {
                LevelName = ExtractString(entities, "level"),
                KVA = kva,
                FuelType = lower.Contains("gas") ? "gas" : "diesel",
            };

            var result = creator.PlaceGenerator(cmd);
            OnCreationCompleted(result);
            return CommandRouterResult.FromPipelineResult(result);
        }

        private CommandRouterResult HandleCreateConduit(Dictionary<string, object> entities, string input)
        {
            var lower = input.ToLowerInvariant();
            var toAllRooms = lower.Contains("all room") || lower.Contains("every room") ||
                             lower.Contains("to all") || lower.Contains("from db");

            // Detect conduit type
            var conduitType = "pvc";
            if (lower.Contains("steel") || lower.Contains("imc")) conduitType = "steel";
            else if (lower.Contains("flexible") || lower.Contains("flex")) conduitType = "flexible";
            else if (lower.Contains("trunking") || lower.Contains("surface")) conduitType = "trunking";

            // Extract diameter
            var diaMatch = Regex.Match(input, @"(\d+)\s*mm\s*(?:conduit|diameter)?",
                RegexOptions.IgnoreCase);
            var diameter = diaMatch.Success ? int.Parse(diaMatch.Groups[1].Value) : 0;

            var creator = new MEP.ConduitCreator(_orchestrator.FamilyResolver.Document);
            var cmd = new MEP.ConduitRoutingCommand
            {
                LevelName = ExtractString(entities, "level"),
                ConduitType = conduitType,
                DiameterMm = diameter,
                ToAllRooms = toAllRooms,
            };

            var result = creator.RouteConduits(cmd);
            OnCreationCompleted(result);
            return CommandRouterResult.FromPipelineResult(result);
        }

        private CommandRouterResult HandleCreateCableTray(Dictionary<string, object> entities, string input)
        {
            var lower = input.ToLowerInvariant();

            // Detect tray type
            var trayType = "perforated";
            if (lower.Contains("ladder")) trayType = "ladder";
            else if (lower.Contains("mesh") || lower.Contains("wire") || lower.Contains("basket"))
                trayType = "wire mesh";

            // Extract width
            var widthMatch = Regex.Match(input, @"(\d+)\s*mm\s*(?:wide|width)?",
                RegexOptions.IgnoreCase);
            var width = widthMatch.Success ? int.Parse(widthMatch.Groups[1].Value) : 300;

            // Extract length
            var lengthMatch = Regex.Match(input, @"(\d+\.?\d*)\s*(?:m|meter)\s*(?:long)?",
                RegexOptions.IgnoreCase);
            var length = lengthMatch.Success ? double.Parse(lengthMatch.Groups[1].Value) * 1000 : 0;

            var creator = new MEP.ConduitCreator(_orchestrator.FamilyResolver.Document);
            var cmd = new MEP.CableTrayCommand
            {
                LevelName = ExtractString(entities, "level"),
                TrayType = trayType,
                WidthMm = width,
                LengthMm = length,
            };

            var result = creator.CreateCableTray(cmd);
            OnCreationCompleted(result);
            return CommandRouterResult.FromPipelineResult(result);
        }

        #endregion

        #region Phase 4 Intent Handlers — HVAC, Plumbing, Fire Protection

        private CommandRouterResult HandleCreateHVAC_AC(Dictionary<string, object> entities, string input)
        {
            var lower = input.ToLowerInvariant();
            var allRooms = lower.Contains("all room") || lower.Contains("all bedroom") ||
                           lower.Contains("every room");
            var creator = new MEP.HVACCreator(_orchestrator.FamilyResolver.Document);
            var cmd = new MEP.SplitACCommand
            {
                RoomName = ExtractString(entities, "roomName", "room"),
                LevelName = ExtractString(entities, "level"),
                AllRooms = allRooms,
            };
            var result = creator.PlaceSplitAC(cmd);
            OnCreationCompleted(result);
            return CommandRouterResult.FromPipelineResult(result);
        }

        private CommandRouterResult HandleCreateHVAC_Fan(Dictionary<string, object> entities, string input)
        {
            var lower = input.ToLowerInvariant();
            var allRooms = lower.Contains("all room") || lower.Contains("all bedroom") ||
                           lower.Contains("every room");
            var creator = new MEP.HVACCreator(_orchestrator.FamilyResolver.Document);
            var cmd = new MEP.CeilingFanCommand
            {
                RoomName = ExtractString(entities, "roomName", "room"),
                LevelName = ExtractString(entities, "level"),
                AllRooms = allRooms,
            };
            var result = creator.PlaceCeilingFans(cmd);
            OnCreationCompleted(result);
            return CommandRouterResult.FromPipelineResult(result);
        }

        private CommandRouterResult HandleCreateHVAC_Extract(Dictionary<string, object> entities, string input)
        {
            var lower = input.ToLowerInvariant();
            var allRooms = lower.Contains("all") || lower.Contains("every");
            var creator = new MEP.HVACCreator(_orchestrator.FamilyResolver.Document);
            var cmd = new MEP.ExtractFanCommand
            {
                RoomName = ExtractString(entities, "roomName", "room"),
                LevelName = ExtractString(entities, "level"),
                AllRooms = allRooms,
            };
            var result = creator.PlaceExtractFans(cmd);
            OnCreationCompleted(result);
            return CommandRouterResult.FromPipelineResult(result);
        }

        private CommandRouterResult HandleCreateHVAC_Hood(Dictionary<string, object> entities, string input)
        {
            var creator = new MEP.HVACCreator(_orchestrator.FamilyResolver.Document);
            var cmd = new MEP.KitchenHoodCommand
            {
                LevelName = ExtractString(entities, "level"),
            };
            var result = creator.PlaceKitchenHood(cmd);
            OnCreationCompleted(result);
            return CommandRouterResult.FromPipelineResult(result);
        }

        private CommandRouterResult HandleCreatePlumbing(Dictionary<string, object> entities, string input)
        {
            var lower = input.ToLowerInvariant();
            var allWet = lower.Contains("all") || lower.Contains("every");
            var creator = new MEP.PlumbingCreator(_orchestrator.FamilyResolver.Document);
            var cmd = new MEP.PlumbingFixtureCommand
            {
                RoomName = ExtractString(entities, "roomName", "room"),
                LevelName = ExtractString(entities, "level"),
                AllWetAreas = allWet,
            };
            var result = creator.PlaceFixtures(cmd);
            OnCreationCompleted(result);
            return CommandRouterResult.FromPipelineResult(result);
        }

        private CommandRouterResult HandleCreatePlumbingCW(Dictionary<string, object> entities, string input)
        {
            var creator = new MEP.PlumbingCreator(_orchestrator.FamilyResolver.Document);
            var cmd = new MEP.PipeRoutingCommand
            {
                LevelName = ExtractString(entities, "level"),
                PipeSystem = "coldwater",
                Material = "copper",
            };
            var result = creator.RouteColdWater(cmd);
            OnCreationCompleted(result);
            return CommandRouterResult.FromPipelineResult(result);
        }

        private CommandRouterResult HandleCreatePlumbingWaste(Dictionary<string, object> entities, string input)
        {
            var creator = new MEP.PlumbingCreator(_orchestrator.FamilyResolver.Document);
            var cmd = new MEP.PipeRoutingCommand
            {
                LevelName = ExtractString(entities, "level"),
                PipeSystem = "waste",
                Material = "pvc",
            };
            var result = creator.RouteWastePipes(cmd);
            OnCreationCompleted(result);
            return CommandRouterResult.FromPipelineResult(result);
        }

        private CommandRouterResult HandleCreatePlumbingRain(Dictionary<string, object> entities, string input)
        {
            var creator = new MEP.PlumbingCreator(_orchestrator.FamilyResolver.Document);
            var cmd = new MEP.RainwaterCommand
            {
                LevelName = ExtractString(entities, "level"),
            };
            var result = creator.PlanRainwaterDrainage(cmd);
            OnCreationCompleted(result);
            return CommandRouterResult.FromPipelineResult(result);
        }

        private CommandRouterResult HandleCreateFireDetector(Dictionary<string, object> entities, string input)
        {
            var lower = input.ToLowerInvariant();
            var allRooms = lower.Contains("all room") || lower.Contains("every room") ||
                           lower.Contains("all ") || !lower.Contains("room");
            var creator = new MEP.FireProtectionCreator(_orchestrator.FamilyResolver.Document);
            var cmd = new MEP.DetectorCommand
            {
                RoomName = ExtractString(entities, "roomName", "room"),
                LevelName = ExtractString(entities, "level"),
                AllRooms = allRooms,
            };
            var result = creator.PlaceDetectors(cmd);
            OnCreationCompleted(result);
            return CommandRouterResult.FromPipelineResult(result);
        }

        private CommandRouterResult HandleCreateFireSprinkler(Dictionary<string, object> entities, string input)
        {
            var creator = new MEP.FireProtectionCreator(_orchestrator.FamilyResolver.Document);
            var cmd = new MEP.SprinklerCommand
            {
                LevelName = ExtractString(entities, "level"),
                AllRooms = true,
            };
            var result = creator.PlaceSprinklers(cmd);
            OnCreationCompleted(result);
            return CommandRouterResult.FromPipelineResult(result);
        }

        private CommandRouterResult HandleCreateFireHose(Dictionary<string, object> entities, string input)
        {
            var creator = new MEP.FireProtectionCreator(_orchestrator.FamilyResolver.Document);
            var cmd = new MEP.FireHoseReelCommand
            {
                LevelName = ExtractString(entities, "level"),
            };
            var result = creator.PlaceFireHoseReels(cmd);
            OnCreationCompleted(result);
            return CommandRouterResult.FromPipelineResult(result);
        }

        private CommandRouterResult HandleCreateFireExtinguisher(Dictionary<string, object> entities, string input)
        {
            var creator = new MEP.FireProtectionCreator(_orchestrator.FamilyResolver.Document);
            var cmd = new MEP.ExtinguisherCommand
            {
                LevelName = ExtractString(entities, "level"),
            };
            var result = creator.PlaceExtinguishers(cmd);
            OnCreationCompleted(result);
            return CommandRouterResult.FromPipelineResult(result);
        }

        private CommandRouterResult HandleCreateFireAlarm(Dictionary<string, object> entities, string input)
        {
            var creator = new MEP.FireProtectionCreator(_orchestrator.FamilyResolver.Document);
            var cmd = new MEP.CallPointCommand
            {
                LevelName = ExtractString(entities, "level"),
            };
            var result = creator.PlaceCallPoints(cmd);
            OnCreationCompleted(result);
            return CommandRouterResult.FromPipelineResult(result);
        }

        #endregion

        #region Entity Extraction Helpers

        private double ExtractDimension(Dictionary<string, object> entities, params string[] keys)
        {
            if (entities == null) return 0;

            foreach (var key in keys)
            {
                if (entities.TryGetValue(key, out var value))
                {
                    if (value is double d) return d;
                    if (double.TryParse(value?.ToString(), out d)) return d;
                }
            }
            return 0;
        }

        private string ExtractString(Dictionary<string, object> entities, params string[] keys)
        {
            if (entities == null) return null;

            foreach (var key in keys)
            {
                if (entities.TryGetValue(key, out var value) && value != null)
                    return value.ToString();
            }
            return null;
        }

        private bool HasRoomDimensions(string input)
        {
            // "5×4", "5x4", "5m x 4m", "5 by 4"
            return Regex.IsMatch(input, @"\d+\.?\d*\s*[×xX]\s*\d+\.?\d*", RegexOptions.IgnoreCase) ||
                   Regex.IsMatch(input, @"\d+\.?\d*\s*(?:m|meter|metre)?\s*(?:by|×|x)\s*\d+\.?\d*",
                       RegexOptions.IgnoreCase);
        }

        private (double widthMm, double depthMm)? ParseRoomDimensions(string input)
        {
            // "5×4m", "5x4 meters", "5m by 4m", "5000×4000mm"
            var match = Regex.Match(input,
                @"(\d+\.?\d*)\s*(mm|m|meter|metre)?\s*[×xX]\s*(\d+\.?\d*)\s*(mm|m|meter|metre)?",
                RegexOptions.IgnoreCase);

            if (!match.Success)
            {
                match = Regex.Match(input,
                    @"(\d+\.?\d*)\s*(mm|m|meter|metre)?\s*(?:by)\s*(\d+\.?\d*)\s*(mm|m|meter|metre)?",
                    RegexOptions.IgnoreCase);
            }

            if (match.Success)
            {
                var w = double.Parse(match.Groups[1].Value);
                var d = double.Parse(match.Groups[3].Value);
                var unit1 = match.Groups[2].Value.ToLowerInvariant();
                var unit2 = match.Groups[4].Value.ToLowerInvariant();

                // Default to meters unless mm specified
                var unitW = unit1 == "mm" ? 1.0 : 1000.0;
                var unitD = (unit2 == "mm" || (string.IsNullOrEmpty(unit2) && unit1 == "mm")) ? 1.0 : 1000.0;

                return (w * unitW, d * unitD);
            }

            return null;
        }

        private string ExtractRoomType(string input)
        {
            var lower = input.ToLowerInvariant();
            var roomTypes = new Dictionary<string, string>
            {
                ["bedroom"] = "Bedroom",
                ["master bedroom"] = "Master Bedroom",
                ["living room"] = "Living Room",
                ["lounge"] = "Living Room",
                ["sitting room"] = "Living Room",
                ["kitchen"] = "Kitchen",
                ["bathroom"] = "Bathroom",
                ["toilet"] = "Bathroom",
                ["wc"] = "Bathroom",
                ["dining"] = "Dining Room",
                ["office"] = "Office",
                ["study"] = "Study",
                ["store"] = "Store Room",
                ["storage"] = "Store Room",
                ["garage"] = "Garage",
                ["corridor"] = "Corridor",
                ["hallway"] = "Corridor",
                ["verandah"] = "Verandah",
                ["balcony"] = "Balcony",
                ["laundry"] = "Laundry",
                ["utility"] = "Utility Room",
                ["pantry"] = "Pantry",
                ["en-suite"] = "En-suite",
                ["ensuite"] = "En-suite"
            };

            // Check longest keys first to catch "master bedroom" before "bedroom"
            foreach (var rt in roomTypes.OrderByDescending(kv => kv.Key.Length))
            {
                if (lower.Contains(rt.Key))
                    return rt.Value;
            }

            return null;
        }

        private (double widthMm, double depthMm)? GetRoomTypeDefaults(string roomType)
        {
            if (string.IsNullOrEmpty(roomType)) return null;

            var defaults = new Dictionary<string, (double w, double d)>(StringComparer.OrdinalIgnoreCase)
            {
                ["Bedroom"] = (3500, 4000),
                ["Master Bedroom"] = (4000, 5000),
                ["Living Room"] = (5000, 6000),
                ["Kitchen"] = (3000, 4000),
                ["Bathroom"] = (2000, 2500),
                ["Dining Room"] = (3500, 4000),
                ["Office"] = (3000, 3500),
                ["Study"] = (2500, 3000),
                ["Store Room"] = (2000, 2000),
                ["Garage"] = (3500, 6000),
                ["Corridor"] = (1200, 5000),
                ["Verandah"] = (2000, 6000),
                ["Balcony"] = (1500, 4000),
                ["Laundry"] = (2000, 2500),
                ["Utility Room"] = (2000, 2500),
                ["Pantry"] = (1500, 2000),
                ["En-suite"] = (2000, 2000)
            };

            if (defaults.TryGetValue(roomType, out var dims))
                return dims;
            return null;
        }

        private string FormatRoomName(string roomType)
        {
            if (string.IsNullOrEmpty(roomType)) return "Room";
            // Title case
            return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(roomType.ToLower());
        }

        private int ExtractNumber(string input, string pattern)
        {
            var match = Regex.Match(input, pattern, RegexOptions.IgnoreCase);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var num))
                return num;
            return -1;
        }

        private double ExtractLengthFromText(string input)
        {
            // "5 meter wall", "3m wall", "6000mm wall"
            var match = Regex.Match(input, @"(\d+\.?\d*)\s*(mm|m|meter|metre)\b", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var value = double.Parse(match.Groups[1].Value);
                var unit = match.Groups[2].Value.ToLowerInvariant();
                return unit == "mm" ? value : value * 1000; // Return in mm
            }
            return 0;
        }

        #endregion

        private void OnCreationCompleted(CreationPipelineResult result)
        {
            CreationCompleted?.Invoke(this, new CreationCompletedEventArgs(result));
        }

        #region Phase 8: Specialist Systems + Proactive Intelligence

        private CommandRouterResult HandleSpecialist(string intentType,
            Dictionary<string, object> entities, string input)
        {
            Logger.Info($"Routing specialist intent: {intentType}");
            var lower = input.ToLowerInvariant();

            switch (intentType)
            {
                case "CREATE_DATA_OUTLET":
                {
                    var creator = new MEP.DataITCreator(_document);
                    var allRooms = lower.Contains("all room") || lower.Contains("every room") ||
                                   lower.Contains("whole building") || lower.Contains("all office");

                    if (allRooms)
                    {
                        var result = creator.PlaceDataOutletsAllRooms(ExtractString(entities, "level"));
                        OnCreationCompleted(result);
                        return CommandRouterResult.FromPipelineResult(result);
                    }

                    var cmd = new MEP.DataOutletCommand
                    {
                        RoomName = ExtractString(entities, "roomName", "room"),
                        RoomType = ExtractRoomType(input),
                        LevelName = ExtractString(entities, "level"),
                        CableCategory = lower.Contains("cat5") ? "cat5e"
                            : lower.Contains("cat7") ? "cat7"
                            : lower.Contains("fibre") || lower.Contains("fiber") ? "fibre"
                            : "cat6a"
                    };

                    var outletResult = creator.PlaceDataOutlets(cmd);
                    OnCreationCompleted(outletResult);
                    return CommandRouterResult.FromPipelineResult(outletResult);
                }

                case "CREATE_WIFI_AP":
                {
                    var creator = new MEP.DataITCreator(_document);
                    var cmd = new MEP.WiFiAPCommand
                    {
                        LevelName = ExtractString(entities, "level"),
                        EnvironmentType = lower.Contains("warehouse") ? "Warehouse"
                            : lower.Contains("open plan") ? "Open Plan"
                            : lower.Contains("conference") ? "Conference"
                            : "Office"
                    };

                    var result = creator.PlaceWiFiAccessPoints(cmd);
                    OnCreationCompleted(result);
                    return CommandRouterResult.FromPipelineResult(result);
                }

                case "CREATE_SERVER_ROOM":
                {
                    var creator = new MEP.DataITCreator(_document);
                    int rackCount = ExtractInt(entities, "count", 2);
                    var rackMatch = Regex.Match(input, @"(\d+)\s*rack", RegexOptions.IgnoreCase);
                    if (rackMatch.Success) rackCount = int.Parse(rackMatch.Groups[1].Value);

                    var cmd = new MEP.ServerRoomCommand
                    {
                        RoomName = ExtractString(entities, "roomName", "room"),
                        LevelName = ExtractString(entities, "level"),
                        RackCount = rackCount
                    };

                    var result = creator.DesignServerRoom(cmd);
                    OnCreationCompleted(result);
                    return CommandRouterResult.FromPipelineResult(result);
                }

                case "CREATE_CCTV":
                {
                    var creator = new MEP.SecurityCreator(_document);
                    var allEntries = lower.Contains("all entr") || lower.Contains("all exit") ||
                                    lower.Contains("every entr") || lower.Contains("whole building");

                    if (allEntries)
                    {
                        var result = creator.PlaceCCTVAllEntries(ExtractString(entities, "level"));
                        OnCreationCompleted(result);
                        return CommandRouterResult.FromPipelineResult(result);
                    }

                    var cmd = new MEP.CCTVCommand
                    {
                        CameraType = lower.Contains("ptz") ? "ptz"
                            : lower.Contains("bullet") ? "bullet"
                            : lower.Contains("fisheye") ? "fisheye"
                            : lower.Contains("anpr") || lower.Contains("number plate") ? "anpr"
                            : "dome",
                        LocationType = lower.Contains("car park") || lower.Contains("parking") ? "car park"
                            : lower.Contains("perimete") ? "perimeter"
                            : lower.Contains("entrance") || lower.Contains("entry") ? "entrance"
                            : lower.Contains("outdoor") || lower.Contains("external") ? "outdoor"
                            : "indoor",
                        Resolution = lower.Contains("4k") ? "4K" : "1080p",
                        LevelName = ExtractString(entities, "level")
                    };

                    var cctvResult = creator.PlaceCCTV(cmd);
                    OnCreationCompleted(cctvResult);
                    return CommandRouterResult.FromPipelineResult(cctvResult);
                }

                case "CREATE_ACCESS_CONTROL":
                {
                    var creator = new MEP.SecurityCreator(_document);
                    var cmd = new MEP.AccessControlCommand
                    {
                        DoorName = ExtractString(entities, "doorName", "door"),
                        SecurityLevel = lower.Contains("biometric") || lower.Contains("fingerprint") ? "secure"
                            : lower.Contains("restricted") ? "restricted"
                            : lower.Contains("critical") || lower.Contains("dual") ? "critical"
                            : "staff",
                        LevelName = ExtractString(entities, "level")
                    };

                    var result = creator.PlaceAccessControl(cmd);
                    OnCreationCompleted(result);
                    return CommandRouterResult.FromPipelineResult(result);
                }

                case "CREATE_ALARM_SYSTEM":
                {
                    var creator = new MEP.SecurityCreator(_document);
                    var cmd = new MEP.AlarmSystemCommand
                    {
                        HasPerimeter = !lower.Contains("no perimete") && !lower.Contains("internal only"),
                        LevelName = ExtractString(entities, "level")
                    };

                    var result = creator.DesignAlarmSystem(cmd);
                    OnCreationCompleted(result);
                    return CommandRouterResult.FromPipelineResult(result);
                }

                case "CREATE_INTERCOM":
                {
                    var creator = new MEP.SecurityCreator(_document);
                    var cmd = new MEP.IntercomCommand
                    {
                        SystemType = lower.Contains("audio only") || lower.Contains("audio-only") ? "audio"
                            : "audio-video",
                        LevelName = ExtractString(entities, "level")
                    };

                    var stationMatch = Regex.Match(input, @"(\d+)\s*station", RegexOptions.IgnoreCase);
                    if (stationMatch.Success)
                        cmd.StationCount = int.Parse(stationMatch.Groups[1].Value);

                    var result = creator.PlaceIntercom(cmd);
                    OnCreationCompleted(result);
                    return CommandRouterResult.FromPipelineResult(result);
                }

                case "CREATE_GAS_PIPING":
                {
                    var creator = new MEP.GasCreator(_document);
                    var appliances = new List<string>();
                    if (lower.Contains("cooker") || lower.Contains("stove")) appliances.Add("cooker");
                    if (lower.Contains("hob")) appliances.Add("hob");
                    if (lower.Contains("oven")) appliances.Add("oven");
                    if (lower.Contains("boiler")) appliances.Add("boiler");
                    if (lower.Contains("water heater") || lower.Contains("geyser")) appliances.Add("water heater");
                    if (lower.Contains("generator")) appliances.Add("generator");
                    if (lower.Contains("deep fryer") || lower.Contains("fryer")) appliances.Add("deep fryer");
                    if (appliances.Count == 0) appliances.Add("cooker");

                    var cmd = new MEP.GasPipingCommand
                    {
                        GasType = lower.Contains("natural gas") ? "Natural Gas" : "LPG",
                        Appliances = appliances,
                        LevelName = ExtractString(entities, "level")
                    };

                    var result = creator.DesignGasPiping(cmd);
                    OnCreationCompleted(result);
                    return CommandRouterResult.FromPipelineResult(result);
                }

                case "CREATE_GAS_DETECTOR":
                {
                    var creator = new MEP.GasCreator(_document);
                    var cmd = new MEP.GasDetectorCommand
                    {
                        GasType = lower.Contains("natural gas") ? "Natural Gas" : "LPG",
                        LevelName = ExtractString(entities, "level"),
                        AllRooms = lower.Contains("all room") || lower.Contains("every room")
                    };

                    var result = creator.PlaceGasDetectors(cmd);
                    OnCreationCompleted(result);
                    return CommandRouterResult.FromPipelineResult(result);
                }

                case "CREATE_SOLAR":
                {
                    var creator = new MEP.SolarPVCreator(_document);

                    // Extract system size or demand
                    var kwpMatch = Regex.Match(input, @"(\d+(?:\.\d+)?)\s*kw", RegexOptions.IgnoreCase);
                    var kwhMatch = Regex.Match(input, @"(\d+(?:\.\d+)?)\s*kwh", RegexOptions.IgnoreCase);

                    var cmd = new MEP.SolarPVCommand
                    {
                        Location = lower.Contains("nairobi") ? "Nairobi"
                            : lower.Contains("dar") ? "Dar es Salaam"
                            : lower.Contains("kigali") ? "Kigali"
                            : lower.Contains("lagos") ? "Lagos"
                            : lower.Contains("joburg") || lower.Contains("johannesburg") ? "Johannesburg"
                            : "Kampala",
                        IncludeBattery = !lower.Contains("no battery") && !lower.Contains("grid only"),
                        BatteryType = lower.Contains("lead") ? "lead_acid" : "lithium",
                        PanelType = lower.Contains("economy") || lower.Contains("cheap") ? "economy"
                            : lower.Contains("premium") || lower.Contains("best") ? "premium"
                            : "standard",
                        LevelName = ExtractString(entities, "level")
                    };

                    if (kwhMatch.Success)
                        cmd.DailyDemandKWh = double.Parse(kwhMatch.Groups[1].Value);
                    else if (kwpMatch.Success)
                        cmd.SystemKWp = double.Parse(kwpMatch.Groups[1].Value);

                    var solarResult = creator.DesignSolarSystem(cmd);
                    OnCreationCompleted(solarResult);
                    return CommandRouterResult.FromPipelineResult(solarResult);
                }

                case "CREATE_EV_CHARGER":
                {
                    var creator = new MEP.EVChargingCreator(_document);

                    // Check for EV-ready (conduit only) vs active chargers
                    if (lower.Contains("ev ready") || lower.Contains("ev-ready") || lower.Contains("future"))
                    {
                        var spacesMatch = Regex.Match(input, @"(\d+)\s*(?:space|bay|park)", RegexOptions.IgnoreCase);
                        int spaces = spacesMatch.Success ? int.Parse(spacesMatch.Groups[1].Value) : 20;
                        var readyResult = creator.DesignEVReadiness(spaces);
                        OnCreationCompleted(readyResult);
                        return CommandRouterResult.FromPipelineResult(readyResult);
                    }

                    var cmd = new MEP.EVChargingCommand
                    {
                        ChargerType = lower.Contains("rapid") || lower.Contains("dc") ? "rapid"
                            : lower.Contains("fast") || lower.Contains("22kw") || lower.Contains("22 kw") ? "fast"
                            : lower.Contains("slow") || lower.Contains("3.7") ? "slow"
                            : "standard",
                        IncludeSolar = lower.Contains("solar"),
                        LevelName = ExtractString(entities, "level")
                    };

                    var countMatch = Regex.Match(input, @"(\d+)\s*(?:charger|station|point)", RegexOptions.IgnoreCase);
                    if (countMatch.Success)
                        cmd.ChargerCount = int.Parse(countMatch.Groups[1].Value);

                    var evResult = creator.DesignEVCharging(cmd);
                    OnCreationCompleted(evResult);
                    return CommandRouterResult.FromPipelineResult(evResult);
                }

                case "GET_DESIGN_ADVICE":
                {
                    var advisor = new ProactiveAdvisor(_document);
                    var message = advisor.FormatSuggestions();
                    return CommandRouterResult.Succeeded(message);
                }

                case "RUN_MODEL_AUDIT":
                {
                    var advisor = new ProactiveAdvisor(_document);
                    var auditResult = advisor.RunFullAudit();
                    return CommandRouterResult.Succeeded(auditResult.Summary);
                }

                case "CHECK_UGANDA_COMPLIANCE":
                {
                    var checker = new UgandaComplianceChecker(_document);
                    var report = checker.CheckAll();
                    var formatted = checker.FormatReport(report);
                    return CommandRouterResult.Succeeded(formatted);
                }

                case "SET_BUDGET":
                {
                    var advisor = new ProactiveAdvisor(_document);
                    var budgetAmount = ExtractDecimal(input);
                    if (budgetAmount > 0)
                    {
                        advisor.SetBudget((double)budgetAmount);
                        return CommandRouterResult.Succeeded(
                            $"Project budget set to UGX {budgetAmount:N0} (${budgetAmount / 3750:N0}).\n" +
                            "I'll monitor costs and alert you at 80% and 100% thresholds.");
                    }
                    return CommandRouterResult.NeedsClarification(
                        "What is the project budget? Please specify an amount (e.g., '600M UGX' or '200,000 USD').",
                        new List<string> { "600M UGX", "1B UGX", "200,000 USD" });
                }

                default:
                    return CommandRouterResult.NotHandled($"Specialist intent '{intentType}' not recognized.");
            }
        }

        #endregion
    }

    #region Router Result Types

    /// <summary>
    /// Result from the CommandRouter — includes the message for the chat panel and follow-up suggestions.
    /// </summary>
    public class CommandRouterResult
    {
        public bool Handled { get; set; }
        public bool Success { get; set; }
        public bool NeedsClarificationFlag { get; set; }
        public string Message { get; set; }
        public string Error { get; set; }
        public List<string> Suggestions { get; set; }
        public CostEstimate CostEstimate { get; set; }
        public int ElementsCreated { get; set; }

        public static CommandRouterResult FromPipelineResult(CreationPipelineResult pipelineResult)
        {
            return new CommandRouterResult
            {
                Handled = true,
                Success = pipelineResult.Success,
                Message = pipelineResult.FormatForChat(),
                Error = pipelineResult.Error,
                Suggestions = pipelineResult.Suggestions,
                CostEstimate = pipelineResult.CostEstimate,
                ElementsCreated = pipelineResult.CreatedCount
            };
        }

        public static CommandRouterResult NotHandled(string message)
        {
            return new CommandRouterResult { Handled = false, Message = message };
        }

        public static CommandRouterResult Failed(string error)
        {
            return new CommandRouterResult { Handled = true, Success = false, Error = error };
        }

        public static CommandRouterResult NeedsClarification(string question, List<string> options)
        {
            return new CommandRouterResult
            {
                Handled = true,
                Success = true,
                NeedsClarificationFlag = true,
                Message = question,
                Suggestions = options
            };
        }
    }

    /// <summary>
    /// Event args for creation completed events.
    /// </summary>
    public class CreationCompletedEventArgs : EventArgs
    {
        public CreationPipelineResult Result { get; }

        public CreationCompletedEventArgs(CreationPipelineResult result)
        {
            Result = result;
        }
    }

    /// <summary>
    /// Event args for modification completed events.
    /// </summary>
    public class ModificationCompletedEventArgs : EventArgs
    {
        public ModificationResult Result { get; }

        public ModificationCompletedEventArgs(ModificationResult result)
        {
            Result = result;
        }
    }

    #endregion
}
