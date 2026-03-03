// StingBIM.AI.UI.Services.ChatHistoryManager
// Manages chat history persistence and retrieval
// Master Proposal Reference: Part 4.2 Phase 1 Month 3 - Chat History

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using NLog;

namespace StingBIM.AI.UI.Services
{
    /// <summary>
    /// Manages chat history persistence, allowing users to save and restore conversations.
    /// </summary>
    public class ChatHistoryManager
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private static ChatHistoryManager _instance;
        private static readonly object _lock = new object();

        private readonly string _historyDirectory;
        private readonly int _maxHistoryFiles;
        private readonly int _maxMessagesPerFile;

        /// <summary>
        /// Gets the singleton instance of the ChatHistoryManager.
        /// </summary>
        public static ChatHistoryManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new ChatHistoryManager();
                    }
                }
                return _instance;
            }
        }

        private ChatHistoryManager(int maxHistoryFiles = 50, int maxMessagesPerFile = 200)
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _historyDirectory = Path.Combine(appData, "StingBIM", "AI", "History");
            _maxHistoryFiles = maxHistoryFiles;
            _maxMessagesPerFile = maxMessagesPerFile;

            EnsureDirectoryExists();
            Logger.Info($"ChatHistoryManager initialized. History directory: {_historyDirectory}");
        }

        private void EnsureDirectoryExists()
        {
            if (!Directory.Exists(_historyDirectory))
            {
                Directory.CreateDirectory(_historyDirectory);
            }
        }

        /// <summary>
        /// Saves a conversation to a file.
        /// </summary>
        /// <param name="sessionId">The session identifier.</param>
        /// <param name="messages">The messages to save.</param>
        /// <param name="metadata">Optional metadata about the conversation.</param>
        public void SaveConversation(string sessionId, IEnumerable<ChatMessageData> messages, ConversationMetadata metadata = null)
        {
            try
            {
                var conversation = new SavedConversation
                {
                    SessionId = sessionId,
                    SavedAt = DateTime.UtcNow,
                    Messages = messages.Take(_maxMessagesPerFile).ToList(),
                    Metadata = metadata ?? new ConversationMetadata
                    {
                        Title = GenerateTitle(messages),
                        CreatedAt = DateTime.UtcNow
                    }
                };

                var filename = $"conversation_{sessionId}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
                var filePath = Path.Combine(_historyDirectory, filename);

                var json = JsonConvert.SerializeObject(conversation, Formatting.Indented);
                File.WriteAllText(filePath, json);

                Logger.Debug($"Conversation saved: {filePath}");

                // Cleanup old files if needed
                CleanupOldFiles();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to save conversation: {sessionId}");
            }
        }

        /// <summary>
        /// Saves the current conversation automatically.
        /// </summary>
        public void AutoSave(string sessionId, IEnumerable<ChatMessageData> messages)
        {
            try
            {
                var autoSaveFile = Path.Combine(_historyDirectory, $"autosave_{sessionId}.json");

                var conversation = new SavedConversation
                {
                    SessionId = sessionId,
                    SavedAt = DateTime.UtcNow,
                    Messages = messages.Take(_maxMessagesPerFile).ToList(),
                    Metadata = new ConversationMetadata
                    {
                        Title = "Auto-saved conversation",
                        CreatedAt = DateTime.UtcNow,
                        IsAutoSave = true
                    }
                };

                var json = JsonConvert.SerializeObject(conversation, Formatting.Indented);
                File.WriteAllText(autoSaveFile, json);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to auto-save conversation");
            }
        }

        /// <summary>
        /// Loads a conversation from a file.
        /// </summary>
        /// <param name="filePath">The file path to load.</param>
        /// <returns>The loaded conversation or null if failed.</returns>
        public SavedConversation LoadConversation(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    Logger.Warn($"Conversation file not found: {filePath}");
                    return null;
                }

                var json = File.ReadAllText(filePath);
                return JsonConvert.DeserializeObject<SavedConversation>(json);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to load conversation: {filePath}");
                return null;
            }
        }

        /// <summary>
        /// Gets the auto-saved conversation for a session.
        /// </summary>
        public SavedConversation LoadAutoSave(string sessionId)
        {
            var autoSaveFile = Path.Combine(_historyDirectory, $"autosave_{sessionId}.json");
            return LoadConversation(autoSaveFile);
        }

        /// <summary>
        /// Gets a list of saved conversations.
        /// </summary>
        /// <param name="includeAutoSaves">Whether to include auto-save files.</param>
        /// <returns>List of conversation summaries.</returns>
        public List<ConversationSummary> GetSavedConversations(bool includeAutoSaves = false)
        {
            var summaries = new List<ConversationSummary>();

            try
            {
                var files = Directory.GetFiles(_historyDirectory, "*.json")
                    .Where(f => includeAutoSaves || !Path.GetFileName(f).StartsWith("autosave_"))
                    .OrderByDescending(f => File.GetLastWriteTimeUtc(f))
                    .ToList();

                foreach (var file in files)
                {
                    try
                    {
                        var conversation = LoadConversation(file);
                        if (conversation != null)
                        {
                            summaries.Add(new ConversationSummary
                            {
                                FilePath = file,
                                SessionId = conversation.SessionId,
                                Title = conversation.Metadata?.Title ?? "Untitled",
                                SavedAt = conversation.SavedAt,
                                MessageCount = conversation.Messages.Count,
                                Preview = GetPreview(conversation.Messages),
                                IsAutoSave = conversation.Metadata?.IsAutoSave ?? false
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(ex, $"Failed to read conversation summary: {file}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to list saved conversations");
            }

            return summaries;
        }

        /// <summary>
        /// Deletes a saved conversation.
        /// </summary>
        public bool DeleteConversation(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    Logger.Info($"Deleted conversation: {filePath}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to delete conversation: {filePath}");
            }

            return false;
        }

        /// <summary>
        /// Deletes all saved conversations.
        /// </summary>
        public void DeleteAllConversations()
        {
            try
            {
                var files = Directory.GetFiles(_historyDirectory, "*.json");
                foreach (var file in files)
                {
                    File.Delete(file);
                }

                Logger.Info("All conversations deleted");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to delete all conversations");
            }
        }

        /// <summary>
        /// Exports a conversation to a specified location.
        /// </summary>
        public bool ExportConversation(string filePath, string exportPath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Copy(filePath, exportPath, overwrite: true);
                    Logger.Info($"Exported conversation to: {exportPath}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to export conversation: {filePath}");
            }

            return false;
        }

        /// <summary>
        /// Imports a conversation from a file.
        /// </summary>
        public bool ImportConversation(string importPath)
        {
            try
            {
                var conversation = LoadConversation(importPath);
                if (conversation != null)
                {
                    var filename = $"imported_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
                    var destPath = Path.Combine(_historyDirectory, filename);
                    File.Copy(importPath, destPath);
                    Logger.Info($"Imported conversation: {destPath}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to import conversation: {importPath}");
            }

            return false;
        }

        private string GenerateTitle(IEnumerable<ChatMessageData> messages)
        {
            // Use first user message as title, truncated
            var firstUserMessage = messages.FirstOrDefault(m => m.IsUser);
            if (firstUserMessage != null)
            {
                var text = firstUserMessage.Text;
                return text.Length > 50 ? text.Substring(0, 47) + "..." : text;
            }

            return $"Conversation {DateTime.Now:yyyy-MM-dd HH:mm}";
        }

        private string GetPreview(List<ChatMessageData> messages)
        {
            var firstUserMessage = messages.FirstOrDefault(m => m.IsUser);
            if (firstUserMessage != null)
            {
                var text = firstUserMessage.Text;
                return text.Length > 100 ? text.Substring(0, 97) + "..." : text;
            }

            return "No messages";
        }

        private void CleanupOldFiles()
        {
            try
            {
                var files = Directory.GetFiles(_historyDirectory, "conversation_*.json")
                    .OrderByDescending(f => File.GetLastWriteTimeUtc(f))
                    .Skip(_maxHistoryFiles)
                    .ToList();

                foreach (var file in files)
                {
                    File.Delete(file);
                    Logger.Debug($"Deleted old conversation: {file}");
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to cleanup old history files");
            }
        }
    }

    /// <summary>
    /// Represents a saved conversation.
    /// </summary>
    public class SavedConversation
    {
        public string SessionId { get; set; }
        public DateTime SavedAt { get; set; }
        public List<ChatMessageData> Messages { get; set; } = new List<ChatMessageData>();
        public ConversationMetadata Metadata { get; set; }
    }

    /// <summary>
    /// Metadata about a conversation.
    /// </summary>
    public class ConversationMetadata
    {
        public string Title { get; set; }
        public DateTime CreatedAt { get; set; }
        public string ProjectName { get; set; }
        public string DocumentPath { get; set; }
        public bool IsAutoSave { get; set; }
        public Dictionary<string, object> CustomData { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Summary of a saved conversation for listing.
    /// </summary>
    public class ConversationSummary
    {
        public string FilePath { get; set; }
        public string SessionId { get; set; }
        public string Title { get; set; }
        public DateTime SavedAt { get; set; }
        public int MessageCount { get; set; }
        public string Preview { get; set; }
        public bool IsAutoSave { get; set; }

        public string FormattedDate => SavedAt.ToLocalTime().ToString("MMM d, yyyy HH:mm");
    }

    /// <summary>
    /// Serializable chat message data.
    /// </summary>
    public class ChatMessageData
    {
        public string Text { get; set; }
        public bool IsUser { get; set; }
        public bool IsError { get; set; }
        public bool IsSystem { get; set; }
        public DateTime Timestamp { get; set; }
        public string ErrorCode { get; set; }
        public string ErrorDetails { get; set; }

        // Convert from ChatMessage
        public static ChatMessageData FromChatMessage(dynamic message)
        {
            return new ChatMessageData
            {
                Text = message.Text,
                IsUser = message.IsUser,
                IsError = message.IsError,
                IsSystem = message.IsSystem ?? false,
                Timestamp = message.Timestamp,
                ErrorCode = message.ErrorCode,
                ErrorDetails = message.ErrorDetails
            };
        }
    }
}
