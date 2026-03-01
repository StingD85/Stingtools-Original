// StingBIM.AI.NLP.Dialogue.ConversationManager
// Manages multi-turn conversations with context awareness
// Master Proposal Reference: Part 1.1 Response Generation, Part 2.2 Strategy 5 - Contextual Memory

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using StingBIM.AI.NLP.Domain;
using StingBIM.AI.NLP.Pipeline;

namespace StingBIM.AI.NLP.Dialogue
{
    /// <summary>
    /// Manages conversational interactions with users.
    /// Maintains context across turns, handles clarifications, and coordinates responses.
    /// </summary>
    public class ConversationManager
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly ContextTracker _contextTracker;
        private readonly ResponseGenerator _responseGenerator;
        private readonly IntentClassifier _intentClassifier;
        private readonly EntityExtractor _entityExtractor;

        private readonly Dictionary<string, ConversationSession> _activeSessions;
        private readonly object _sessionLock = new object();

        // Configuration
        public int MaxHistoryTurns { get; set; } = 20;
        public TimeSpan SessionTimeout { get; set; } = TimeSpan.FromHours(2);
        public float ClarificationThreshold { get; set; } = 0.6f;

        /// <summary>
        /// Event fired when a command is ready for execution.
        /// </summary>
        public event EventHandler<CommandReadyEventArgs> CommandReady;

        /// <summary>
        /// Event fired when clarification is needed from user.
        /// </summary>
        public event EventHandler<ClarificationNeededEventArgs> ClarificationNeeded;

        public ConversationManager(
            ContextTracker contextTracker,
            ResponseGenerator responseGenerator,
            IntentClassifier intentClassifier,
            EntityExtractor entityExtractor)
        {
            _contextTracker = contextTracker ?? throw new ArgumentNullException(nameof(contextTracker));
            _responseGenerator = responseGenerator ?? throw new ArgumentNullException(nameof(responseGenerator));
            _intentClassifier = intentClassifier ?? throw new ArgumentNullException(nameof(intentClassifier));
            _entityExtractor = entityExtractor ?? throw new ArgumentNullException(nameof(entityExtractor));
            _activeSessions = new Dictionary<string, ConversationSession>();
        }

        /// <summary>
        /// Processes a user message and returns an appropriate response.
        /// </summary>
        public async Task<ConversationResponse> ProcessMessageAsync(
            string sessionId,
            string userMessage,
            CancellationToken cancellationToken = default)
        {
            Logger.Info($"Processing message for session {sessionId}: {userMessage}");

            var session = GetOrCreateSession(sessionId);
            var startTime = DateTime.Now;

            try
            {
                // Add user message to history
                session.AddTurn(new ConversationTurn
                {
                    Role = TurnRole.User,
                    Content = userMessage,
                    Timestamp = DateTime.Now
                });

                // Step 1: Classify intent
                var intentResult = await _intentClassifier.ClassifyAsync(userMessage, cancellationToken);
                Logger.Debug($"Intent: {intentResult.Intent} ({intentResult.Confidence:P0})");

                // Step 2: Extract entities
                var entities = _entityExtractor.Extract(userMessage);
                Logger.Debug($"Extracted {entities.Count} entities");

                // Step 3: Update context
                var classificationResult = new IntentClassificationResult
                {
                    Intent = intentResult.Intent,
                    Confidence = intentResult.Confidence,
                    AlternativeIntents = intentResult.Alternatives?.Select(a => a.Intent).ToList() ?? new List<string>()
                };
                _contextTracker.UpdateContext(session.Context, classificationResult, entities, session.History);

                // Step 4: Check if we need clarification
                if (NeedsClarification(intentResult, entities, session.Context))
                {
                    // If user had a pending clarification, check if this is a response to it
                    // or a completely new topic. If new intent is UNKNOWN with low confidence,
                    // it's likely a clarification response. Otherwise treat as new query.
                    if (session.PendingClarification != null)
                    {
                        // Only treat as clarification response if intent is truly unknown
                        if (intentResult.Intent == "UNKNOWN" || intentResult.Confidence < 0.5f)
                        {
                            return await HandleClarificationResponseAsync(session, userMessage, cancellationToken);
                        }
                        // User changed topic — clear old clarification and proceed with new query
                        session.PendingClarification = null;
                    }

                    return await HandleClarificationAsync(session, intentResult, entities, cancellationToken);
                }

                // Step 5: If there's a pending clarification but the new message has a clear
                // different intent, the user changed topic — clear it and process normally
                if (session.PendingClarification != null)
                {
                    var pendingIntent = session.PendingClarification.OriginalIntent?.Intent;
                    if (intentResult.Intent != pendingIntent && intentResult.Confidence >= ClarificationThreshold)
                    {
                        // User moved on to a different topic
                        Logger.Debug($"User changed topic from {pendingIntent} to {intentResult.Intent}, clearing pending clarification");
                        session.PendingClarification = null;
                    }
                    else
                    {
                        return await HandleClarificationResponseAsync(session, userMessage, cancellationToken);
                    }
                }

                // Step 6: Build command from intent and entities
                var command = BuildCommand(intentResult, entities, session.Context);

                // Step 7: Generate response (pass user message for knowledge lookup)
                _responseGenerator.CurrentUserMessage = userMessage;
                var response = await _responseGenerator.GenerateAsync(
                    command,
                    session.Context,
                    cancellationToken);

                // Add assistant response to history
                session.AddTurn(new ConversationTurn
                {
                    Role = TurnRole.Assistant,
                    Content = response.Message,
                    Timestamp = DateTime.Now,
                    Command = command
                });

                // Fire command ready event — carries entities for creation pipeline
                if (command.IsExecutable)
                {
                    var entityDict = new Dictionary<string, object>();
                    foreach (var entity in entities)
                    {
                        entityDict[entity.Type.ToString().ToLowerInvariant()] = entity.Value;
                    }

                    CommandReady?.Invoke(this, new CommandReadyEventArgs
                    {
                        SessionId = sessionId,
                        Command = command,
                        Entities = entityDict,
                        OriginalInput = userMessage
                    });
                }

                response.ProcessingTimeMs = (DateTime.Now - startTime).TotalMilliseconds;
                return response;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error processing message: {userMessage}");
                return new ConversationResponse
                {
                    Message = "I encountered an error processing your request. Could you try rephrasing it?",
                    ResponseType = ResponseType.Error,
                    ProcessingTimeMs = (DateTime.Now - startTime).TotalMilliseconds
                };
            }
        }

        /// <summary>
        /// Handles special commands like "undo", "help", "cancel".
        /// </summary>
        public async Task<ConversationResponse> HandleSpecialCommandAsync(
            string sessionId,
            SpecialCommand command,
            CancellationToken cancellationToken = default)
        {
            var session = GetOrCreateSession(sessionId);

            switch (command)
            {
                case SpecialCommand.Undo:
                    return new ConversationResponse
                    {
                        Message = "Undoing the last action.",
                        ResponseType = ResponseType.Confirmation,
                        Action = new DesignCommand { CommandType = "UNDO", IsExecutable = true }
                    };

                case SpecialCommand.Redo:
                    return new ConversationResponse
                    {
                        Message = "Redoing the last undone action.",
                        ResponseType = ResponseType.Confirmation,
                        Action = new DesignCommand { CommandType = "REDO", IsExecutable = true }
                    };

                case SpecialCommand.Cancel:
                    session.PendingClarification = null;
                    return new ConversationResponse
                    {
                        Message = "Operation cancelled. What would you like to do?",
                        ResponseType = ResponseType.Acknowledgment
                    };

                case SpecialCommand.Help:
                    return await GenerateHelpResponseAsync(session.Context, cancellationToken);

                case SpecialCommand.Status:
                    return GenerateStatusResponse(session);

                default:
                    return new ConversationResponse
                    {
                        Message = "I didn't understand that command.",
                        ResponseType = ResponseType.Error
                    };
            }
        }

        /// <summary>
        /// Provides feedback on the result of a command execution.
        /// </summary>
        public void ProvideFeedback(string sessionId, CommandFeedback feedback)
        {
            var session = GetOrCreateSession(sessionId);

            // Update the last turn with feedback
            var lastAssistantTurn = session.History
                .LastOrDefault(t => t.Role == TurnRole.Assistant && t.Command != null);

            if (lastAssistantTurn != null)
            {
                lastAssistantTurn.Feedback = feedback;

                // Update context based on feedback
                if (feedback.Success)
                {
                    _contextTracker.RecordSuccess(session.Context, lastAssistantTurn.Command);
                }
                else
                {
                    _contextTracker.RecordFailure(session.Context, lastAssistantTurn.Command, feedback.ErrorMessage);
                }
            }

            Logger.Debug($"Feedback recorded for session {sessionId}: {(feedback.Success ? "Success" : "Failure")}");
        }

        /// <summary>
        /// Gets conversation history for a session.
        /// </summary>
        public IEnumerable<ConversationTurn> GetHistory(string sessionId)
        {
            lock (_sessionLock)
            {
                if (_activeSessions.TryGetValue(sessionId, out var session))
                {
                    return session.History.ToList();
                }
                return Enumerable.Empty<ConversationTurn>();
            }
        }

        /// <summary>
        /// Clears conversation history for a session.
        /// </summary>
        public void ClearHistory(string sessionId)
        {
            lock (_sessionLock)
            {
                if (_activeSessions.TryGetValue(sessionId, out var session))
                {
                    session.History.Clear();
                    session.Context = new ConversationContext();
                    Logger.Info($"History cleared for session {sessionId}");
                }
            }
        }

        private ConversationSession GetOrCreateSession(string sessionId)
        {
            lock (_sessionLock)
            {
                if (!_activeSessions.TryGetValue(sessionId, out var session))
                {
                    session = new ConversationSession
                    {
                        SessionId = sessionId,
                        StartedAt = DateTime.Now,
                        Context = new ConversationContext()
                    };
                    _activeSessions[sessionId] = session;
                    Logger.Info($"Created new session: {sessionId}");
                }

                session.LastActivityAt = DateTime.Now;

                // Cleanup old sessions
                CleanupOldSessions();

                return session;
            }
        }

        private void CleanupOldSessions()
        {
            var expiredSessions = _activeSessions
                .Where(kvp => DateTime.Now - kvp.Value.LastActivityAt > SessionTimeout)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var sessionId in expiredSessions)
            {
                _activeSessions.Remove(sessionId);
                Logger.Debug($"Removed expired session: {sessionId}");
            }
        }

        /// <summary>
        /// Non-actionable intents that should never trigger clarification or be marked non-executable.
        /// These are informational or conversational intents that the system can respond to directly.
        /// </summary>
        private static readonly HashSet<string> NonActionableIntents = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "GREETING", "HELP", "INFORMATION", "UNDO", "REDO",
            "QUERY_AREA", "QUERY_MODEL", "CHECK_COMPLIANCE",
            "CREATE_WALL", "CREATE_FLOOR", "CREATE_ROOM", "CREATE_HOUSE",
            "CREATE_DOOR", "CREATE_WINDOW", "CREATE_ROOF", "CREATE_CEILING",
            "CREATE_STAIRCASE", "CREATE_COLUMN", "CREATE_BEAM", "CREATE_FOUNDATION",
            "CREATE_RAMP", "CREATE_RAILING", "CREATE_CURTAIN_WALL", "CREATE_PARAPET",
            "CREATE_LIGHTING", "CREATE_OUTLET", "CREATE_SWITCH",
            "CREATE_DB", "CREATE_GENERATOR", "CREATE_CONDUIT", "CREATE_CABLE_TRAY",
            "CREATE_SOLAR", "CREATE_EV_CHARGER",
            "CREATE_HVAC_AC", "CREATE_HVAC_FAN", "CREATE_HVAC_EXTRACT", "CREATE_HVAC_HOOD",
            "CREATE_PLUMBING", "CREATE_PLUMBING_CW", "CREATE_PLUMBING_WASTE", "CREATE_PLUMBING_RAIN",
            "CREATE_FIRE_DETECTOR", "CREATE_FIRE_SPRINKLER", "CREATE_FIRE_HOSE",
            "CREATE_FIRE_EXTINGUISHER", "CREATE_FIRE_ALARM",
            "QUERY_MATERIALS", "GENERATE_BOQ", "MATERIAL_TAKEOFF", "QUERY_PARAMETERS",
            // Phase 5: Modification + Bulk
            "MOVE_ELEMENT", "COPY_ELEMENT", "DELETE_ELEMENT", "ROTATE_ELEMENT",
            "MIRROR_ELEMENT", "RESIZE_ELEMENT", "CHANGE_TYPE", "SET_PARAMETER",
            "SPLIT_ELEMENT", "EXTEND_ELEMENT", "OFFSET_ELEMENT", "LEVEL_ADJUST",
            "PIN_ELEMENT", "UNPIN_ELEMENT",
            "ARRAY_ELEMENT", "ALIGN_ELEMENT", "DISTRIBUTE_ELEMENT",
            "PURGE_UNUSED", "VALUE_ENGINEER", "AUTO_TAG", "RENUMBER_ELEMENT",
            "FM_GENERAL", "GENERATE_MAINTENANCE_SCHEDULE", "PREDICT_FAILURES",
            "OPTIMIZE_MAINTENANCE", "ANALYZE_FAILURES",
            "NEGOTIATE_DESIGN", "GET_RECOMMENDATIONS", "RESOLVE_CONFLICT", "COLLABORATE",
            // Phase 6: LAN Collaboration
            "SETUP_WORKSHARING", "SYNC_MODEL", "CHECK_WORKSHARING_CONFLICTS",
            "DIAGNOSE_EDIT", "GENERATE_BEP", "MODEL_HEALTH_CHECK",
            "VIEW_CHANGELOG", "VIEW_TEAM", "CREATE_BACKUP", "RESTORE_BACKUP",
            "LIST_BACKUPS", "START_AUTOSYNC", "STOP_AUTOSYNC",
            "START_AUTOBACKUP", "STOP_AUTOBACKUP", "RELINQUISH_ELEMENT",
            "EXPORT_CHANGELOG",
            // Phase 7: Budget Design + Exports
            "BUDGET_DESIGN", "ESTIMATE_COST", "CHECK_BUDGET",
            "EXPORT_BOQ", "EXPORT_COBIE", "EXPORT_ROOM_SCHEDULE",
            "EXPORT_DOOR_SCHEDULE", "EXPORT_WINDOW_SCHEDULE",
            "IMPORT_PARAMETERS", "VALUE_ENGINEER_BUDGET",
            // Phase 8: Specialist Systems + Proactive Intelligence
            "CREATE_DATA_OUTLET", "CREATE_WIFI_AP", "CREATE_SERVER_ROOM",
            "CREATE_CCTV", "CREATE_ACCESS_CONTROL", "CREATE_ALARM_SYSTEM", "CREATE_INTERCOM",
            "CREATE_GAS_PIPING", "CREATE_GAS_DETECTOR",
            "GET_DESIGN_ADVICE", "RUN_MODEL_AUDIT",
            "CHECK_UGANDA_COMPLIANCE", "SET_BUDGET"
        };

        private bool NeedsClarification(IntentResult intent, List<ExtractedEntity> entities, ConversationContext context)
        {
            // Non-actionable intents never need clarification — respond directly
            if (NonActionableIntents.Contains(intent.Intent))
            {
                return false;
            }

            // Low confidence intent
            if (intent.Confidence < ClarificationThreshold)
            {
                return true;
            }

            // Missing required entities for certain intents
            var requiredEntities = GetRequiredEntities(intent.Intent);
            var extractedTypes = entities.Select(e => e.Type).ToHashSet();

            foreach (var required in requiredEntities)
            {
                if (!extractedTypes.Contains(required) && !context.HasValue(required.ToString()))
                {
                    return true;
                }
            }

            return false;
        }

        private IEnumerable<EntityType> GetRequiredEntities(string intent)
        {
            return intent.ToUpperInvariant() switch
            {
                "CREATE_WALL" => new[] { EntityType.DIMENSION },
                "CREATE_ROOM" => new[] { EntityType.ROOM_TYPE },
                "MOVE_ELEMENT" => new[] { EntityType.DIRECTION },
                "SET_PARAMETER" => new[] { EntityType.PARAMETER_NAME, EntityType.PARAMETER_VALUE },
                _ => Enumerable.Empty<EntityType>()
            };
        }

        private Task<ConversationResponse> HandleClarificationAsync(
            ConversationSession session,
            IntentResult intent,
            List<ExtractedEntity> entities,
            CancellationToken cancellationToken)
        {
            var clarification = new PendingClarification
            {
                OriginalIntent = intent,
                ExtractedEntities = entities,
                AskedAt = DateTime.Now
            };

            // Determine what to ask about
            if (intent.Confidence < ClarificationThreshold && intent.Alternatives.Any())
            {
                // Ambiguous intent
                clarification.Question = GenerateIntentClarificationQuestion(intent);
                clarification.Type = ClarificationType.IntentAmbiguous;
                clarification.Options = intent.Alternatives.Take(3).Select(a => a.Intent).ToList();
            }
            else
            {
                // Missing entities
                var missingEntities = GetRequiredEntities(intent.Intent)
                    .Where(e => !entities.Any(ex => ex.Type == e))
                    .ToList();

                clarification.Question = GenerateEntityClarificationQuestion(intent.Intent, missingEntities);
                clarification.Type = ClarificationType.MissingEntity;
                clarification.MissingEntities = missingEntities;
            }

            session.PendingClarification = clarification;

            ClarificationNeeded?.Invoke(this, new ClarificationNeededEventArgs
            {
                SessionId = session.SessionId,
                Clarification = clarification
            });

            return Task.FromResult(new ConversationResponse
            {
                Message = clarification.Question,
                ResponseType = ResponseType.Clarification,
                Suggestions = clarification.Options
            });
        }

        private async Task<ConversationResponse> HandleClarificationResponseAsync(
            ConversationSession session,
            string response,
            CancellationToken cancellationToken)
        {
            var pending = session.PendingClarification;
            session.PendingClarification = null;

            if (pending.Type == ClarificationType.IntentAmbiguous)
            {
                // User selected or confirmed intent
                var selectedIntent = pending.Options?.FirstOrDefault(o =>
                    response.Contains(o, StringComparison.OrdinalIgnoreCase)) ?? pending.OriginalIntent.Intent;

                pending.OriginalIntent.Intent = selectedIntent;
                pending.OriginalIntent.Confidence = 0.95f;
            }
            else if (pending.Type == ClarificationType.MissingEntity)
            {
                // Extract entities from the clarification response
                var newEntities = _entityExtractor.Extract(response);
                pending.ExtractedEntities.AddRange(newEntities);
            }

            // Now build and execute the command
            var command = BuildCommand(pending.OriginalIntent, pending.ExtractedEntities, session.Context);

            var conversationResponse = await _responseGenerator.GenerateAsync(
                command,
                session.Context,
                cancellationToken);

            if (command.IsExecutable)
            {
                var entityDict = new Dictionary<string, object>();
                foreach (var entity in pending.ExtractedEntities)
                {
                    entityDict[entity.Type.ToString().ToLowerInvariant()] = entity.Value;
                }

                CommandReady?.Invoke(this, new CommandReadyEventArgs
                {
                    SessionId = session.SessionId,
                    Command = command,
                    Entities = entityDict,
                    OriginalInput = response
                });
            }

            return conversationResponse;
        }

        /// <summary>
        /// Adds intelligent context-aware follow-up suggestions after creation commands.
        /// Uses building code knowledge and spatial reasoning to suggest next design steps.
        /// </summary>
        private void EnrichCreationResponse(ConversationResponse response, DesignCommand command, List<ExtractedEntity> entities)
        {
            if (command == null || !command.IsExecutable)
                return;

            var intent = command.CommandType?.ToUpperInvariant();
            var suggestions = new List<string>();

            // Extract room type if present
            var roomType = entities?.FirstOrDefault(e => e.Type == EntityType.ROOM_TYPE)?.NormalizedValue?.ToLowerInvariant();

            switch (intent)
            {
                case "CREATE_ROOM":
                    suggestions.Add("Add a window to this room");
                    suggestions.Add("Add a door");

                    // Intelligent follow-ups based on room type
                    if (roomType == "bedroom" || roomType == "master bedroom")
                    {
                        suggestions.Add("Create an en-suite bathroom");
                        suggestions.Add("Add a walk-in closet");
                    }
                    else if (roomType == "kitchen")
                    {
                        suggestions.Add("Create adjacent dining room");
                        suggestions.Add("Add a pantry");
                    }
                    else if (roomType == "living room" || roomType == "living")
                    {
                        suggestions.Add("Create adjacent dining area");
                        suggestions.Add("Add a balcony");
                    }
                    else if (roomType == "office")
                    {
                        suggestions.Add("Add a meeting room nearby");
                        suggestions.Add("Create adjacent reception area");
                    }
                    else if (roomType == "bathroom" || roomType == "toilet")
                    {
                        suggestions.Add("Check ADA accessibility compliance");
                    }

                    suggestions.Add("Analyze this room's design quality");
                    suggestions.Add("Validate compliance for this room");
                    break;

                case "CREATE_WALL":
                    suggestions.Add("Add a window to this wall");
                    suggestions.Add("Add a door to this wall");
                    suggestions.Add("Create a room on the other side");
                    suggestions.Add("Check wall fire rating compliance");
                    break;

                case "CREATE_FLOOR":
                    suggestions.Add("Create walls on this floor");
                    suggestions.Add("Add a floor opening for stairs");
                    suggestions.Add("Check structural loading for this floor");
                    break;

                case "ADD_WINDOW":
                    suggestions.Add("Add more windows for cross-ventilation");
                    suggestions.Add("Check IBC glazing area compliance");
                    suggestions.Add("Analyze daylighting for this room");
                    break;

                case "ADD_DOOR":
                    suggestions.Add("Check door width meets ADA requirements");
                    suggestions.Add("Create the room this door leads to");
                    suggestions.Add("Add windows to this room");
                    break;

                case "MODIFY_ELEMENT":
                    suggestions.Add("Analyze updated design");
                    suggestions.Add("Validate compliance after changes");
                    break;
            }

            // Only add suggestions if we have some
            if (suggestions.Count > 0)
            {
                // Merge with existing suggestions (response generator may have added some)
                var existing = response.Suggestions ?? new List<string>();
                var merged = existing.Concat(suggestions).Distinct().Take(5).ToList();
                response.Suggestions = merged;
            }
        }

        private DesignCommand BuildCommand(IntentResult intent, List<ExtractedEntity> entities, ConversationContext context)
        {
            // Non-actionable intents are always "executable" (they produce a direct response)
            var isExecutable = NonActionableIntents.Contains(intent.Intent) ||
                               intent.Confidence >= ClarificationThreshold;

            var command = new DesignCommand
            {
                CommandType = intent.Intent,
                Confidence = intent.Confidence,
                IsExecutable = isExecutable
            };

            // Map entities to parameters
            foreach (var entity in entities)
            {
                command.Parameters[entity.Type.ToString()] = entity.NormalizedValue;
            }

            // Fill in defaults from context
            foreach (var (key, value) in context.CurrentDefaults)
            {
                if (!command.Parameters.ContainsKey(key))
                {
                    command.Parameters[key] = value;
                }
            }

            return command;
        }

        private string GenerateIntentClarificationQuestion(IntentResult intent)
        {
            var options = string.Join(", ", intent.Alternatives.Take(3).Select(a => $"'{a.Intent}'"));
            return $"I'm not sure what you meant. Did you want to {options}?";
        }

        private string GenerateEntityClarificationQuestion(string intent, List<EntityType> missingEntities)
        {
            var missing = missingEntities.FirstOrDefault();
            return missing switch
            {
                EntityType.DIMENSION => "What dimensions would you like?",
                EntityType.ROOM_TYPE => "What type of room would you like to create?",
                EntityType.DIRECTION => "In which direction?",
                EntityType.MATERIAL => "What material should I use?",
                EntityType.PARAMETER_VALUE => "What value should I set?",
                _ => "Could you provide more details?"
            };
        }

        private Task<ConversationResponse> GenerateHelpResponseAsync(
            ConversationContext context,
            CancellationToken cancellationToken)
        {
            var helpMessage = @"I can help you design in Revit using natural language. Here's what I can do:

Design & Modeling:
- 'Create a 4x5 meter bedroom'
- 'Create a wall 5 meters long'
- 'Add a floor slab 6x8 meters'
- 'Build a 3-bedroom house'

Model Information:
- 'Review the model' - full model summary
- 'Generate BOQ' - Bill of Quantities
- 'Material takeoff' - material quantities
- 'What materials are used?' - material list
- 'List the furniture' - element queries
- 'What's the total area?' - floor area

Standards & Compliance:
- 'Check fire code compliance'
- 'What is ISO 19650?'
- 'What are BIM standards?'

Facilities Management:
- 'Generate a maintenance schedule'
- 'Predict equipment failures'
- 'Optimize maintenance strategy'

Collaboration:
- 'Get agent recommendations'
- 'Negotiate design options'
- 'Resolve design conflicts'

LAN Worksharing:
- 'Setup worksharing' — enable worksharing, create central model
- 'Sync to central' — synchronize changes
- 'Why can't I edit this?' — diagnose checkout status
- 'Check conflicts' — pre-sync conflict analysis
- 'Generate BEP' — ISO 19650 BIM Execution Plan
- 'Model health check' — worksharing diagnostics
- 'Who's online?' — team status
- 'Show changelog' — recent team activity
- 'Create backup' / 'Restore backup' — model recovery

Budget & Exports:
- 'Estimate cost' — construction cost from the model
- 'Budget design for 600M UGX' — 3 design options within budget
- 'Export BOQ' — priced Bill of Quantities (CSV)
- 'Export COBie' — 18-sheet COBie 2.4 FM handover
- 'Export room schedule' / 'Export door schedule'
- 'Import parameters from CSV' — batch parameter import

Specialist Systems:
- 'Add data outlets to the office' — structured cabling (Cat6A)
- 'Plan WiFi coverage' — WiFi AP placement and coverage
- 'Design server room with 4 racks' — server room layout
- 'Add CCTV at all entries' — security camera placement
- 'Set up access control' — card reader / biometric access
- 'Design alarm system' — intruder alarm with PIR sensors
- 'Add intercom' — door entry system
- 'Design gas piping for kitchen' — LPG/gas distribution
- 'Design solar PV system' — solar panel array + battery
- 'Add EV chargers' — electric vehicle charging stations

Proactive Intelligence:
- 'Set budget to 600M' — enable budget monitoring
- 'Get design advice' — suggestions for East Africa context
- 'Run model audit' — full compliance + budget check
- 'Check Uganda compliance' — UNBS/fire/accessibility rules

General:
- Ask 'What is BIM?' or any question about building standards
- Say 'undo' to reverse the last action, or 'cancel' to stop";

            return Task.FromResult(new ConversationResponse
            {
                Message = helpMessage,
                ResponseType = ResponseType.Information
            });
        }

        private ConversationResponse GenerateStatusResponse(ConversationSession session)
        {
            var turnCount = session.History.Count;
            var duration = DateTime.Now - session.StartedAt;

            return new ConversationResponse
            {
                Message = $"Session active for {duration.TotalMinutes:F0} minutes with {turnCount} exchanges. Ready for your next command.",
                ResponseType = ResponseType.Information
            };
        }
    }

    #region Supporting Classes

    public class ConversationSession
    {
        public string SessionId { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime LastActivityAt { get; set; }
        public List<ConversationTurn> History { get; set; } = new List<ConversationTurn>();
        public ConversationContext Context { get; set; } = new ConversationContext();
        public PendingClarification PendingClarification { get; set; }

        public void AddTurn(ConversationTurn turn)
        {
            History.Add(turn);
            // Trim old history if needed
            while (History.Count > 50)
            {
                History.RemoveAt(0);
            }
        }
    }

    public class ConversationTurn
    {
        public TurnRole Role { get; set; }
        public string Content { get; set; }
        public DateTime Timestamp { get; set; }
        public DesignCommand Command { get; set; }
        public CommandFeedback Feedback { get; set; }
    }

    public enum TurnRole
    {
        User,
        Assistant,
        System
    }

    public class ConversationResponse
    {
        public string Message { get; set; }
        public ResponseType ResponseType { get; set; }
        public DesignCommand Action { get; set; }
        public List<string> Suggestions { get; set; }
        public double ProcessingTimeMs { get; set; }

        /// <summary>
        /// Expandable detail sections for structured data (BOQ, materials, parameters).
        /// </summary>
        public List<QueryDetailSection> DetailSections { get; set; }
    }

    public enum ResponseType
    {
        Confirmation,
        Clarification,
        Information,
        Error,
        Acknowledgment,
        Warning
    }

    public class DesignCommand
    {
        public string CommandType { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
        public float Confidence { get; set; }
        public bool IsExecutable { get; set; }
    }

    public class CommandFeedback
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public object Result { get; set; }
    }

    public class PendingClarification
    {
        public IntentResult OriginalIntent { get; set; }
        public List<ExtractedEntity> ExtractedEntities { get; set; }
        public ClarificationType Type { get; set; }
        public string Question { get; set; }
        public List<string> Options { get; set; }
        public List<EntityType> MissingEntities { get; set; }
        public DateTime AskedAt { get; set; }
    }

    public enum ClarificationType
    {
        IntentAmbiguous,
        MissingEntity,
        ConfirmationRequired
    }

    public enum SpecialCommand
    {
        Undo,
        Redo,
        Cancel,
        Help,
        Status
    }

    public class CommandReadyEventArgs : EventArgs
    {
        public string SessionId { get; set; }
        public DesignCommand Command { get; set; }
        public Dictionary<string, object> Entities { get; set; }
        public string OriginalInput { get; set; }
    }

    public class ClarificationNeededEventArgs : EventArgs
    {
        public string SessionId { get; set; }
        public PendingClarification Clarification { get; set; }
    }

    #endregion
}
