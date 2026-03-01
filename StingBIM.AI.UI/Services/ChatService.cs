// ============================================================================
// StingBIM AI - Chat Service
// Connects UI components to the AI orchestrator for seamless interaction
// ============================================================================

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using StingBIM.AI.Intelligence.Core;
using StingBIM.AI.Intelligence.Training;

namespace StingBIM.AI.UI.Services
{
    /// <summary>
    /// Service that manages chat interactions between UI and AI system
    /// </summary>
    public class ChatService : INotifyPropertyChanged
    {
        private static readonly Lazy<ChatService> _instance =
            new Lazy<ChatService>(() => new ChatService());
        public static ChatService Instance => _instance.Value;

        private readonly AIOrchestrator _orchestrator;
        private readonly StreamingResponseService _streamingService;
        private CancellationTokenSource _currentCts;

        private bool _isProcessing;
        private bool _isInitialized;
        private string _statusMessage;
        private int _progressPercent;

        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler<ChatMessageEventArgs> MessageReceived;
        public event EventHandler<ChatErrorEventArgs> ErrorOccurred;

        public ObservableCollection<ChatMessage> Messages { get; } = new ObservableCollection<ChatMessage>();

        public bool IsProcessing
        {
            get => _isProcessing;
            private set { _isProcessing = value; OnPropertyChanged(); }
        }

        public bool IsInitialized
        {
            get => _isInitialized;
            private set { _isInitialized = value; OnPropertyChanged(); }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set { _statusMessage = value; OnPropertyChanged(); }
        }

        public int ProgressPercent
        {
            get => _progressPercent;
            private set { _progressPercent = value; OnPropertyChanged(); }
        }

        public int KnowledgeEntryCount => _orchestrator.KnowledgeEntries;
        public int TaskTemplateCount => _orchestrator.TaskTemplates;

        private ChatService()
        {
            _orchestrator = AIOrchestrator.Instance;
            _streamingService = StreamingResponseService.Instance;

            // Subscribe to orchestrator events
            _orchestrator.ProcessingProgress += (s, e) =>
            {
                StatusMessage = e.Message;
                ProgressPercent = e.Percent;
            };

            _orchestrator.ErrorOccurred += (s, e) =>
            {
                OnError(e.Context, e.Exception);
            };
        }

        /// <summary>
        /// Initialize the chat service and load AI data
        /// </summary>
        public async Task InitializeAsync(string basePath = null)
        {
            if (IsInitialized) return;

            try
            {
                IsProcessing = true;
                StatusMessage = "Loading AI knowledge base...";

                // Use provided path or default
                basePath ??= GetDefaultBasePath();

                await _orchestrator.InitializeAsync(basePath);

                IsInitialized = true;
                StatusMessage = $"Ready - {KnowledgeEntryCount} knowledge entries loaded";

                // Add welcome message
                AddSystemMessage($"StingBIM AI Assistant is ready!\n" +
                    $"Loaded {KnowledgeEntryCount} knowledge entries and {TaskTemplateCount} task templates.\n\n" +
                    "Try asking:\n" +
                    "- 'What is LOD 350?'\n" +
                    "- 'Generate a BIM Execution Plan'\n" +
                    "- 'What parameters are available for walls?'\n\n" +
                    "Type 'help' for more options.");
            }
            catch (Exception ex)
            {
                OnError("Initialization", ex);
                AddSystemMessage($"Failed to initialize AI: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
            }
        }

        /// <summary>
        /// Send a message and get AI response
        /// </summary>
        public async Task<ChatMessage> SendMessageAsync(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return null;

            // Cancel any existing operation
            _currentCts?.Cancel();
            _currentCts = new CancellationTokenSource();

            try
            {
                IsProcessing = true;

                // Add user message
                var userMessage = new ChatMessage
                {
                    Type = MessageType.User,
                    Content = message,
                    Timestamp = DateTime.Now
                };
                AddMessage(userMessage);

                // Process with AI
                var response = await _orchestrator.ProcessInputAsync(message, _currentCts.Token);

                // Create response message
                var responseMessage = new ChatMessage
                {
                    Type = GetMessageType(response.Type),
                    Content = response.Message,
                    Timestamp = DateTime.Now,
                    Confidence = response.Confidence,
                    Source = response.Source,
                    Data = response.Data,
                    Suggestions = response.Suggestions,
                    RelatedTopics = response.RelatedTopics
                };

                // Add with optional streaming effect
                if (response.Type == ResponseType.Answer || response.Type == ResponseType.Help)
                {
                    await AddMessageWithStreamingAsync(responseMessage);
                }
                else
                {
                    AddMessage(responseMessage);
                }

                return responseMessage;
            }
            catch (OperationCanceledException)
            {
                // User cancelled - ignore
                return null;
            }
            catch (Exception ex)
            {
                OnError("Message processing", ex);
                var errorMessage = new ChatMessage
                {
                    Type = MessageType.Error,
                    Content = $"Error: {ex.Message}",
                    Timestamp = DateTime.Now
                };
                AddMessage(errorMessage);
                return errorMessage;
            }
            finally
            {
                IsProcessing = false;
            }
        }

        /// <summary>
        /// Get command suggestions based on partial input
        /// </summary>
        public List<CommandSuggestion> GetSuggestions(string partialInput = null)
        {
            return _orchestrator.GetSuggestions(partialInput);
        }

        /// <summary>
        /// Search the knowledge base
        /// </summary>
        public List<KnowledgeMatch> SearchKnowledge(string query, int maxResults = 10)
        {
            return _orchestrator.SearchKnowledge(query, maxResults);
        }

        /// <summary>
        /// Cancel current operation
        /// </summary>
        public void CancelCurrentOperation()
        {
            _currentCts?.Cancel();
            _streamingService.CancelStreaming();
            IsProcessing = false;
            StatusMessage = "Operation cancelled";
        }

        /// <summary>
        /// Clear conversation history
        /// </summary>
        public void ClearHistory()
        {
            Messages.Clear();
            AddSystemMessage("Conversation cleared. How can I help you?");
        }

        /// <summary>
        /// Export conversation to text
        /// </summary>
        public string ExportConversation()
        {
            var lines = Messages.Select(m =>
                $"[{m.Timestamp:HH:mm:ss}] {m.Type}: {m.Content}");
            return string.Join("\n\n", lines);
        }

        private void AddMessage(ChatMessage message)
        {
            // Thread-safe add
            if (System.Windows.Application.Current?.Dispatcher != null)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    Messages.Add(message);
                    MessageReceived?.Invoke(this, new ChatMessageEventArgs { Message = message });
                });
            }
            else
            {
                Messages.Add(message);
                MessageReceived?.Invoke(this, new ChatMessageEventArgs { Message = message });
            }
        }

        private async Task AddMessageWithStreamingAsync(ChatMessage message)
        {
            // Add empty message first
            var streamingMessage = new ChatMessage
            {
                Type = message.Type,
                Content = "",
                Timestamp = message.Timestamp,
                IsStreaming = true
            };
            AddMessage(streamingMessage);

            // Stream content
            await _streamingService.StreamTextAsync(
                message.Content,
                update =>
                {
                    if (System.Windows.Application.Current?.Dispatcher != null)
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            streamingMessage.Content = update;
                        });
                    }
                    else
                    {
                        streamingMessage.Content = update;
                    }
                },
                System.Windows.Application.Current?.Dispatcher);

            streamingMessage.IsStreaming = false;
            streamingMessage.Confidence = message.Confidence;
            streamingMessage.Source = message.Source;
            streamingMessage.Data = message.Data;
            streamingMessage.Suggestions = message.Suggestions;
            streamingMessage.RelatedTopics = message.RelatedTopics;
        }

        private void AddSystemMessage(string content)
        {
            AddMessage(new ChatMessage
            {
                Type = MessageType.System,
                Content = content,
                Timestamp = DateTime.Now
            });
        }

        private MessageType GetMessageType(ResponseType responseType)
        {
            return responseType switch
            {
                ResponseType.Answer => MessageType.Assistant,
                ResponseType.TaskResult => MessageType.Assistant,
                ResponseType.Command => MessageType.Assistant,
                ResponseType.Help => MessageType.System,
                ResponseType.NoMatch => MessageType.Assistant,
                ResponseType.Error => MessageType.Error,
                _ => MessageType.Assistant
            };
        }

        private string GetDefaultBasePath()
        {
            // Try to find project root
            var currentDir = AppDomain.CurrentDomain.BaseDirectory;
            var searchPaths = new[]
            {
                currentDir,
                System.IO.Path.Combine(currentDir, ".."),
                System.IO.Path.Combine(currentDir, "..", ".."),
                System.IO.Path.Combine(currentDir, "..", "..", ".."),
                System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "StingBIM"),
                System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StingBIM")
            };

            foreach (var path in searchPaths)
            {
                var docsPath = System.IO.Path.Combine(path, "docs");
                if (System.IO.Directory.Exists(docsPath))
                {
                    return path;
                }
            }

            return currentDir;
        }

        private void OnError(string context, Exception ex)
        {
            ErrorOccurred?.Invoke(this, new ChatErrorEventArgs
            {
                Context = context,
                Exception = ex
            });
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    #region Data Models

    public class ChatMessage : INotifyPropertyChanged
    {
        private string _content;
        private bool _isStreaming;

        public MessageType Type { get; set; }

        public string Content
        {
            get => _content;
            set { _content = value; OnPropertyChanged(); }
        }

        public DateTime Timestamp { get; set; }
        public double Confidence { get; set; }
        public string Source { get; set; }
        public object Data { get; set; }
        public List<string> Suggestions { get; set; }
        public List<string> RelatedTopics { get; set; }

        public bool IsStreaming
        {
            get => _isStreaming;
            set { _isStreaming = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public enum MessageType
    {
        User,
        Assistant,
        System,
        Error
    }

    public class ChatMessageEventArgs : EventArgs
    {
        public ChatMessage Message { get; set; }
    }

    public class ChatErrorEventArgs : EventArgs
    {
        public string Context { get; set; }
        public Exception Exception { get; set; }
    }

    #endregion
}
