// ============================================================================
// StingBIM AI - AI Orchestrator
// Central intelligence hub connecting NLP, knowledge base, and executive tasks
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StingBIM.AI.Intelligence.NLP;
using StingBIM.AI.Intelligence.Training;
using StingBIM.AI.Intelligence.Executive;

namespace StingBIM.AI.Intelligence.Core
{
    /// <summary>
    /// Central AI orchestrator that routes user input to appropriate handlers
    /// </summary>
    public class AIOrchestrator
    {
        private static readonly Lazy<AIOrchestrator> _instance =
            new Lazy<AIOrchestrator>(() => new AIOrchestrator());
        public static AIOrchestrator Instance => _instance.Value;

        private readonly OfflineNLPEngine _nlpEngine;
        private readonly TrainingDataLoader _trainingData;
        private readonly ExecutiveTaskEngine _taskEngine;
        private readonly ConversationContext _context;
        private bool _isInitialized;

        public event EventHandler<AIResponseEventArgs> ResponseGenerated;
        public event EventHandler<AIProgressEventArgs> ProcessingProgress;
        public event EventHandler<AIErrorEventArgs> ErrorOccurred;

        public bool IsInitialized => _isInitialized;
        public int KnowledgeEntries => _trainingData?.KnowledgeEntryCount ?? 0;
        public int TaskTemplates => _trainingData?.TaskTemplateCount ?? 0;

        private AIOrchestrator()
        {
            _nlpEngine = new OfflineNLPEngine();
            _trainingData = TrainingDataLoader.Instance;
            _taskEngine = ExecutiveTaskEngine.Instance;
            _context = new ConversationContext();

            // Subscribe to task progress
            _taskEngine.TaskProgress += (s, e) =>
            {
                OnProgress(e.Percent, e.Message);
            };
        }

        /// <summary>
        /// Initialize the AI system by loading training data
        /// </summary>
        public async Task InitializeAsync(string basePath, CancellationToken cancellationToken = default)
        {
            try
            {
                OnProgress(0, "Initializing AI system...");

                _trainingData.LoadProgress += (s, e) =>
                {
                    OnProgress(e.Percent / 2, e.Message);
                };

                await _trainingData.LoadAllAsync(basePath, cancellationToken);

                _isInitialized = true;
                OnProgress(100, $"AI ready: {_trainingData.KnowledgeEntryCount} knowledge entries, {_trainingData.TaskTemplateCount} task templates");
            }
            catch (Exception ex)
            {
                OnError("Initialization failed", ex);
                throw;
            }
        }

        /// <summary>
        /// Process user input and generate appropriate response
        /// </summary>
        public async Task<AIResponse> ProcessInputAsync(string input, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return new AIResponse
                {
                    Type = ResponseType.Error,
                    Message = "Please enter a message or command."
                };
            }

            try
            {
                OnProgress(10, "Analyzing input...");

                // Step 1: Process with NLP
                var nlpResult = _nlpEngine.Process(input, new NLPContext
                {
                    LastIntent = _context.LastIntent,
                    SelectedElements = _context.SelectedElements
                });

                OnProgress(30, "Determining response type...");

                // Step 2: Route based on classification
                AIResponse response;

                if (_taskEngine.IsTaskRequest(input))
                {
                    // Handle as executive task
                    response = await HandleTaskRequestAsync(input, nlpResult, cancellationToken);
                }
                else if (IsQuestion(nlpResult))
                {
                    // Handle as knowledge query
                    response = await HandleQuestionAsync(input, nlpResult, cancellationToken);
                }
                else if (nlpResult.Intent.IntentType == IntentType.Command ||
                         nlpResult.Intent.IntentType == IntentType.Action)
                {
                    // Handle as BIM command
                    response = HandleBIMCommand(nlpResult);
                }
                else if (nlpResult.Intent.IntentType == IntentType.Help)
                {
                    // Handle help request
                    response = GenerateHelpResponse();
                }
                else
                {
                    // Default to knowledge search
                    response = await HandleQuestionAsync(input, nlpResult, cancellationToken);
                }

                // Update context
                _context.LastIntent = nlpResult.Intent.IntentType;
                _context.AddMessage(input, response.Message);

                OnProgress(100, "Response ready");
                OnResponse(response);

                return response;
            }
            catch (Exception ex)
            {
                OnError("Processing failed", ex);
                return new AIResponse
                {
                    Type = ResponseType.Error,
                    Message = $"Sorry, I encountered an error: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Get suggested commands based on current context
        /// </summary>
        public List<CommandSuggestion> GetSuggestions(string partialInput = null)
        {
            var suggestions = new List<CommandSuggestion>();

            // Add task suggestions
            foreach (var task in _taskEngine.GetAvailableTasks())
            {
                var parts = task.Split(" - ");
                suggestions.Add(new CommandSuggestion
                {
                    Command = parts[0],
                    Description = parts.Length > 1 ? parts[1] : "",
                    Category = "Executive Tasks"
                });
            }

            // Add common query suggestions
            var queries = new[]
            {
                ("What is LOD 350?", "Level of Development definitions"),
                ("How do I calculate voltage drop?", "Engineering formulas"),
                ("What parameters are available for walls?", "Parameter information"),
                ("Show me MEP schedule templates", "Schedule templates"),
                ("What is ISO 19650?", "BIM standards")
            };

            foreach (var (cmd, desc) in queries)
            {
                suggestions.Add(new CommandSuggestion
                {
                    Command = cmd,
                    Description = desc,
                    Category = "Knowledge Queries"
                });
            }

            // Filter by partial input if provided
            if (!string.IsNullOrWhiteSpace(partialInput))
            {
                var searchLower = partialInput.ToLower();
                suggestions = suggestions
                    .Where(s => s.Command.ToLower().Contains(searchLower) ||
                               s.Description.ToLower().Contains(searchLower))
                    .ToList();
            }

            return suggestions.Take(10).ToList();
        }

        /// <summary>
        /// Search the knowledge base
        /// </summary>
        public List<KnowledgeMatch> SearchKnowledge(string query, int maxResults = 10)
        {
            return _trainingData.Search(query, maxResults);
        }

        private async Task<AIResponse> HandleTaskRequestAsync(string input, NLPResult nlpResult, CancellationToken cancellationToken)
        {
            OnProgress(40, "Executing task...");

            var result = await _taskEngine.ExecuteTaskAsync(input);

            if (result.Success)
            {
                return new AIResponse
                {
                    Type = ResponseType.TaskResult,
                    Message = result.Summary,
                    Data = result.Output,
                    TaskType = result.TaskType
                };
            }
            else
            {
                return new AIResponse
                {
                    Type = ResponseType.Error,
                    Message = result.ErrorMessage ?? "Task execution failed"
                };
            }
        }

        private async Task<AIResponse> HandleQuestionAsync(string input, NLPResult nlpResult, CancellationToken cancellationToken)
        {
            OnProgress(50, "Searching knowledge base...");

            // Try to find answer in knowledge base
            var match = _trainingData.FindAnswer(input);

            if (match != null && match.Score > 0.5)
            {
                OnProgress(80, "Found answer");
                return new AIResponse
                {
                    Type = ResponseType.Answer,
                    Message = match.Entry.Answer,
                    Confidence = match.Score,
                    Source = match.Entry.Category ?? "Knowledge Base",
                    MatchType = match.MatchType
                };
            }

            // Try broader search
            var searchResults = _trainingData.Search(input, 3);
            if (searchResults.Any())
            {
                var bestResult = searchResults.First();
                var additionalResults = searchResults.Skip(1).ToList();

                return new AIResponse
                {
                    Type = ResponseType.Answer,
                    Message = bestResult.Entry.Answer,
                    Confidence = bestResult.Score,
                    Source = bestResult.Entry.Category ?? "Knowledge Base",
                    RelatedTopics = additionalResults.Select(r => r.Entry.Question).ToList()
                };
            }

            // No match found
            return new AIResponse
            {
                Type = ResponseType.NoMatch,
                Message = "I couldn't find specific information about that in my knowledge base. " +
                         "Could you rephrase your question or try asking about:\n" +
                         "- BIM parameters (e.g., 'What is BLE_WALL_HEIGHT_MM?')\n" +
                         "- Engineering formulas (e.g., 'How do I calculate area?')\n" +
                         "- Schedule templates (e.g., 'Show MEP schedules')\n" +
                         "- Executive tasks (e.g., 'Generate a BIM Execution Plan')",
                Suggestions = GetSuggestions().Take(5).Select(s => s.Command).ToList()
            };
        }

        private AIResponse HandleBIMCommand(NLPResult nlpResult)
        {
            if (nlpResult.Command == null)
            {
                return new AIResponse
                {
                    Type = ResponseType.Error,
                    Message = "I understood you want to perform an action, but I couldn't determine the specific command."
                };
            }

            // Build command description
            var commandDesc = $"Command: {nlpResult.Command.CommandType}\n";

            if (nlpResult.Command.TargetElements.Any())
            {
                commandDesc += $"Target: {string.Join(", ", nlpResult.Command.TargetElements)}\n";
            }

            if (nlpResult.Command.Parameters.Any())
            {
                commandDesc += "Parameters:\n";
                foreach (var param in nlpResult.Command.Parameters)
                {
                    commandDesc += $"  - {param.Key}: {param.Value ?? "not specified"}\n";
                }
            }

            return new AIResponse
            {
                Type = ResponseType.Command,
                Message = commandDesc,
                Command = nlpResult.Command,
                Confidence = nlpResult.Command.Confidence
            };
        }

        private AIResponse GenerateHelpResponse()
        {
            var helpText = @"StingBIM AI Assistant - Help

**Knowledge Queries**
Ask me about BIM parameters, formulas, schedules, and standards:
- 'What is LOD 350?'
- 'How do I calculate voltage drop?'
- 'What are the ASHRAE ventilation requirements?'

**Executive Tasks**
I can help with project management tasks:
- 'Parse this project brief: [description]'
- 'Generate a BIM Execution Plan'
- 'Create a construction schedule'
- 'Track project costs'
- 'Provide recommendations'

**BIM Commands**
I can interpret commands for Revit actions:
- 'Create a wall 3m tall'
- 'Select all doors on Level 1'
- 'Show me all MEP equipment'

**Tips**
- Be specific with measurements and parameters
- Mention disciplines (Architectural, MEP, Structural)
- Reference standards (ISO 19650, ASHRAE, NEC)

Type your question or command to get started!";

            return new AIResponse
            {
                Type = ResponseType.Help,
                Message = helpText
            };
        }

        private bool IsQuestion(NLPResult nlpResult)
        {
            return nlpResult.Intent.IntentType == IntentType.Query ||
                   nlpResult.QueryStructure.QuestionType != QuestionType.Statement;
        }

        private void OnProgress(int percent, string message)
        {
            ProcessingProgress?.Invoke(this, new AIProgressEventArgs { Percent = percent, Message = message });
        }

        private void OnResponse(AIResponse response)
        {
            ResponseGenerated?.Invoke(this, new AIResponseEventArgs { Response = response });
        }

        private void OnError(string context, Exception ex)
        {
            ErrorOccurred?.Invoke(this, new AIErrorEventArgs { Context = context, Exception = ex });
        }
    }

    #region Data Models

    public class AIResponse
    {
        public ResponseType Type { get; set; }
        public string Message { get; set; }
        public double Confidence { get; set; }
        public string Source { get; set; }
        public string MatchType { get; set; }
        public object Data { get; set; }
        public string TaskType { get; set; }
        public BIMCommand Command { get; set; }
        public List<string> Suggestions { get; set; }
        public List<string> RelatedTopics { get; set; }
    }

    public enum ResponseType
    {
        Answer,
        TaskResult,
        Command,
        Help,
        NoMatch,
        Error
    }

    public class CommandSuggestion
    {
        public string Command { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
    }

    public class ConversationContext
    {
        public IntentType LastIntent { get; set; }
        public List<string> SelectedElements { get; set; } = new List<string>();
        public List<ConversationMessage> History { get; set; } = new List<ConversationMessage>();

        public void AddMessage(string userInput, string aiResponse)
        {
            History.Add(new ConversationMessage
            {
                UserInput = userInput,
                AIResponse = aiResponse,
                Timestamp = DateTime.Now
            });

            // Keep last 20 messages
            if (History.Count > 20)
            {
                History = History.Skip(History.Count - 20).ToList();
            }
        }
    }

    public class ConversationMessage
    {
        public string UserInput { get; set; }
        public string AIResponse { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class AIResponseEventArgs : EventArgs
    {
        public AIResponse Response { get; set; }
    }

    public class AIProgressEventArgs : EventArgs
    {
        public int Percent { get; set; }
        public string Message { get; set; }
    }

    public class AIErrorEventArgs : EventArgs
    {
        public string Context { get; set; }
        public Exception Exception { get; set; }
    }

    #endregion
}
