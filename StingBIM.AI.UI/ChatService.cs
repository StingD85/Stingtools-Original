// StingBIM.AI.UI.ChatService
// Orchestrates AI chat interactions between the UI and backend services
// Master Proposal Reference: Part 4.2 Phase 1 Month 3 - User Interface

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using StingBIM.AI.NLP.Dialogue;
using StingBIM.AI.NLP.Semantic;
using StingBIM.AI.Knowledge.Graph;

namespace StingBIM.AI.UI
{
    /// <summary>
    /// Central service that orchestrates AI-powered chat interactions.
    /// Bridges the UI layer with NLP processing, knowledge graph lookups,
    /// and design command execution.
    /// </summary>
    public class ChatService : IDisposable
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly ConversationManager _conversationManager;
        private readonly KnowledgeGraph _knowledgeGraph;
        private readonly SemanticUnderstanding _semanticUnderstanding;

        private readonly Dictionary<string, ChatSession> _sessions;
        private readonly object _sessionLock = new object();
        private bool _disposed;

        /// <summary>
        /// Event raised when a design command is ready for execution.
        /// </summary>
        public event EventHandler<CommandReadyEventArgs> CommandReady;

        /// <summary>
        /// Event raised when the service encounters a critical error.
        /// </summary>
        public event EventHandler<ChatServiceErrorEventArgs> ServiceError;

        /// <summary>
        /// Gets whether the service has been initialized.
        /// </summary>
        public bool IsInitialized { get; private set; }

        public ChatService(
            ConversationManager conversationManager,
            KnowledgeGraph knowledgeGraph = null,
            SemanticUnderstanding semanticUnderstanding = null)
        {
            _conversationManager = conversationManager ?? throw new ArgumentNullException(nameof(conversationManager));
            _knowledgeGraph = knowledgeGraph;
            _semanticUnderstanding = semanticUnderstanding;
            _sessions = new Dictionary<string, ChatSession>();

            // Wire up conversation manager events
            _conversationManager.CommandReady += OnConversationCommandReady;
        }

        /// <summary>
        /// Initializes the chat service and its dependencies.
        /// </summary>
        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                Logger.Info("Initializing ChatService");

                // Initialize knowledge graph with building domain if available
                if (_knowledgeGraph != null && _knowledgeGraph.NodeCount == 0)
                {
                    await Task.Run(() => _knowledgeGraph.InitializeWithBuildingKnowledge(), cancellationToken);
                    Logger.Info($"Knowledge graph initialized with {_knowledgeGraph.NodeCount} nodes");
                }

                IsInitialized = true;
                Logger.Info("ChatService initialized successfully");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to initialize ChatService");
                ServiceError?.Invoke(this, new ChatServiceErrorEventArgs
                {
                    Error = ex.Message,
                    IsCritical = true
                });
                throw;
            }
        }

        #region Message Processing

        /// <summary>
        /// Processes a user message and returns a response.
        /// </summary>
        public async Task<ChatResponse> ProcessMessageAsync(
            string sessionId,
            string userMessage,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(userMessage))
            {
                return new ChatResponse
                {
                    Message = "Please enter a message.",
                    Type = ResponseType.Error
                };
            }

            var session = GetOrCreateSession(sessionId);
            var startTime = DateTime.Now;

            try
            {
                Logger.Debug($"Processing message for session {sessionId}: {userMessage}");

                // Check for special commands first
                var specialCommand = ParseSpecialCommand(userMessage);
                if (specialCommand.HasValue)
                {
                    var specialResponse = await _conversationManager.HandleSpecialCommandAsync(
                        sessionId, specialCommand.Value, cancellationToken);

                    return ConvertToResponse(specialResponse, startTime);
                }

                // Process through conversation manager
                var conversationResponse = await _conversationManager.ProcessMessageAsync(
                    sessionId, userMessage, cancellationToken);

                // Enrich with knowledge graph suggestions if available
                var suggestions = GetContextualSuggestions(userMessage, conversationResponse);

                // Enrich with knowledge lookups
                var knowledgeResults = QueryKnowledge(userMessage);

                var response = ConvertToResponse(conversationResponse, startTime);
                response.Suggestions = suggestions;
                response.KnowledgeNodes = knowledgeResults;

                // Track in session
                session.MessageCount++;
                session.LastActivity = DateTime.Now;

                return response;
            }
            catch (OperationCanceledException)
            {
                Logger.Debug($"Message processing cancelled for session {sessionId}");
                throw;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error processing message: {userMessage}");

                return new ChatResponse
                {
                    Message = $"I encountered an error processing your request: {ex.Message}",
                    Type = ResponseType.Error,
                    ProcessingTimeMs = (DateTime.Now - startTime).TotalMilliseconds
                };
            }
        }

        /// <summary>
        /// Processes a special command (undo, redo, cancel, help, status).
        /// </summary>
        private SpecialCommand? ParseSpecialCommand(string input)
        {
            var trimmed = input.Trim().ToLowerInvariant();

            return trimmed switch
            {
                "undo" => SpecialCommand.Undo,
                "redo" => SpecialCommand.Redo,
                "cancel" or "stop" => SpecialCommand.Cancel,
                "help" or "?" => SpecialCommand.Help,
                "status" => SpecialCommand.Status,
                _ => null
            };
        }

        #endregion

        #region Knowledge Lookups

        /// <summary>
        /// Queries the knowledge graph for relevant nodes matching the user input.
        /// </summary>
        private List<KnowledgeNode> QueryKnowledge(string query)
        {
            if (_knowledgeGraph == null || _knowledgeGraph.NodeCount == 0)
            {
                return new List<KnowledgeNode>();
            }

            try
            {
                var results = _knowledgeGraph.SearchNodes(query, maxResults: 5).ToList();
                Logger.Debug($"Knowledge query '{query}' returned {results.Count} results");
                return results;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Knowledge query failed");
                return new List<KnowledgeNode>();
            }
        }

        /// <summary>
        /// Gets related knowledge for a specific topic.
        /// </summary>
        public List<KnowledgeNode> GetRelatedKnowledge(string nodeId, string relationType = null)
        {
            if (_knowledgeGraph == null)
            {
                return new List<KnowledgeNode>();
            }

            try
            {
                return _knowledgeGraph.GetRelatedNodes(nodeId, relationType).ToList();
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"Failed to get related knowledge for {nodeId}");
                return new List<KnowledgeNode>();
            }
        }

        #endregion

        #region Suggestions

        /// <summary>
        /// Generates contextual suggestions based on the conversation state.
        /// </summary>
        private List<string> GetContextualSuggestions(string userMessage, ConversationResponse conversationResponse)
        {
            var suggestions = new List<string>();

            // Use suggestions from the conversation response first
            if (conversationResponse.Suggestions != null && conversationResponse.Suggestions.Any())
            {
                suggestions.AddRange(conversationResponse.Suggestions);
            }

            // Add knowledge-based suggestions if we have few suggestions
            if (suggestions.Count < 3 && _knowledgeGraph != null)
            {
                try
                {
                    var knowledgeNodes = _knowledgeGraph.SearchNodes(userMessage, maxResults: 3);
                    foreach (var node in knowledgeNodes)
                    {
                        var relatedNodes = _knowledgeGraph.GetRelatedNodes(node.Id);
                        foreach (var related in relatedNodes.Take(2))
                        {
                            var suggestion = GenerateSuggestionFromNode(related);
                            if (!string.IsNullOrEmpty(suggestion) && !suggestions.Contains(suggestion))
                            {
                                suggestions.Add(suggestion);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Debug(ex, "Knowledge-based suggestion generation failed");
                }
            }

            return suggestions.Take(5).ToList();
        }

        /// <summary>
        /// Generates a natural language suggestion from a knowledge graph node.
        /// </summary>
        private string GenerateSuggestionFromNode(KnowledgeNode node)
        {
            if (node == null) return null;

            return node.NodeType switch
            {
                "RoomType" => $"Create a {node.Name.ToLower()}",
                "ElementType" => $"Add a {node.Name.ToLower()}",
                "Material" => $"Use {node.Name.ToLower()}",
                _ => null
            };
        }

        #endregion

        #region Feedback

        /// <summary>
        /// Provides execution feedback for the last command.
        /// </summary>
        public void ProvideFeedback(string sessionId, bool success, string message = null)
        {
            _conversationManager.ProvideFeedback(sessionId, new CommandFeedback
            {
                Success = success,
                ErrorMessage = success ? null : message
            });

            Logger.Debug($"Feedback recorded for session {sessionId}: {(success ? "Success" : $"Failure: {message}")}");
        }

        #endregion

        #region Session Management

        /// <summary>
        /// Clears chat history for a session.
        /// </summary>
        public void ClearHistory(string sessionId)
        {
            _conversationManager.ClearHistory(sessionId);

            lock (_sessionLock)
            {
                if (_sessions.TryGetValue(sessionId, out var session))
                {
                    session.MessageCount = 0;
                }
            }

            Logger.Info($"History cleared for session {sessionId}");
        }

        /// <summary>
        /// Gets conversation history for a session.
        /// </summary>
        public IEnumerable<ConversationTurn> GetHistory(string sessionId)
        {
            return _conversationManager.GetHistory(sessionId);
        }

        private ChatSession GetOrCreateSession(string sessionId)
        {
            lock (_sessionLock)
            {
                if (!_sessions.TryGetValue(sessionId, out var session))
                {
                    session = new ChatSession
                    {
                        SessionId = sessionId,
                        CreatedAt = DateTime.Now,
                        LastActivity = DateTime.Now
                    };
                    _sessions[sessionId] = session;
                }
                return session;
            }
        }

        #endregion

        #region Response Conversion

        private ChatResponse ConvertToResponse(ConversationResponse conversationResponse, DateTime startTime)
        {
            return new ChatResponse
            {
                Message = conversationResponse.Message,
                Type = conversationResponse.ResponseType,
                Action = conversationResponse.Action,
                Suggestions = conversationResponse.Suggestions ?? new List<string>(),
                ProcessingTimeMs = conversationResponse.ProcessingTimeMs > 0
                    ? conversationResponse.ProcessingTimeMs
                    : (DateTime.Now - startTime).TotalMilliseconds
            };
        }

        #endregion

        #region Event Handlers

        private void OnConversationCommandReady(object sender, CommandReadyEventArgs e)
        {
            CommandReady?.Invoke(this, e);
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _conversationManager.CommandReady -= OnConversationCommandReady;

            lock (_sessionLock)
            {
                _sessions.Clear();
            }

            Logger.Info("ChatService disposed");
        }

        #endregion
    }

    #region Supporting Classes

    /// <summary>
    /// Response from the chat service.
    /// </summary>
    public class ChatResponse
    {
        /// <summary>
        /// The response message text.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// The type of response.
        /// </summary>
        public ResponseType Type { get; set; }

        /// <summary>
        /// Design command to execute, if applicable.
        /// </summary>
        public DesignCommand Action { get; set; }

        /// <summary>
        /// Follow-up suggestions for the user.
        /// </summary>
        public List<string> Suggestions { get; set; } = new List<string>();

        /// <summary>
        /// Related knowledge graph nodes, if found.
        /// </summary>
        public List<KnowledgeNode> KnowledgeNodes { get; set; } = new List<KnowledgeNode>();

        /// <summary>
        /// Processing time in milliseconds.
        /// </summary>
        public double ProcessingTimeMs { get; set; }
    }

    /// <summary>
    /// Tracks per-session state in the ChatService.
    /// </summary>
    internal class ChatSession
    {
        public string SessionId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastActivity { get; set; }
        public int MessageCount { get; set; }
    }

    /// <summary>
    /// Event args for ChatService errors.
    /// </summary>
    public class ChatServiceErrorEventArgs : EventArgs
    {
        public string Error { get; set; }
        public bool IsCritical { get; set; }
    }

    #endregion
}
