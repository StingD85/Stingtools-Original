// StingBIM.AI.Core.Memory.WorkingMemory
// Active context memory for current command processing
// Master Proposal Reference: Part 2.2 Strategy 5 - Contextual Memory Networks

using System;
using System.Collections.Generic;
using System.Linq;
using NLog;

namespace StingBIM.AI.Core.Memory
{
    /// <summary>
    /// Working memory for immediate context during command processing.
    /// Holds current command, selected elements, and recent context.
    /// Capacity: ~7 items | Duration: Seconds (Part 2.2)
    /// </summary>
    public class WorkingMemory
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly object _lock = new object();
        private readonly int _maxCapacity;

        // Current context items
        private readonly LinkedList<MemoryItem> _items;
        private DateTime _lastAccessTime;

        public WorkingMemory(int maxCapacity = 7)
        {
            _maxCapacity = maxCapacity;
            _items = new LinkedList<MemoryItem>();
            _lastAccessTime = DateTime.Now;
        }

        /// <summary>
        /// Current user command being processed.
        /// </summary>
        public string CurrentCommand { get; private set; }

        /// <summary>
        /// Currently selected element IDs in Revit.
        /// </summary>
        public IReadOnlyList<int> SelectedElementIds { get; private set; } = new List<int>();

        /// <summary>
        /// Current conversation context.
        /// </summary>
        public ConversationContext Context { get; private set; }

        /// <summary>
        /// Gets all items in working memory.
        /// </summary>
        public IEnumerable<MemoryItem> Items
        {
            get
            {
                lock (_lock)
                {
                    return _items.ToList();
                }
            }
        }

        /// <summary>
        /// Sets the current command being processed.
        /// </summary>
        public void SetCurrentCommand(string command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));

            lock (_lock)
            {
                CurrentCommand = command;
                _lastAccessTime = DateTime.Now;
                AddItem(new MemoryItem
                {
                    Type = MemoryItemType.Command,
                    Content = command,
                    Timestamp = DateTime.Now
                });
            }
            Logger.Debug($"Working memory: Set command - {command}");
        }

        /// <summary>
        /// Updates the selected elements.
        /// </summary>
        public void SetSelectedElements(IEnumerable<int> elementIds)
        {
            lock (_lock)
            {
                SelectedElementIds = elementIds.ToList();
                _lastAccessTime = DateTime.Now;
            }
            Logger.Debug($"Working memory: {SelectedElementIds.Count} elements selected");
        }

        /// <summary>
        /// Adds an item to working memory, maintaining capacity limit.
        /// </summary>
        public void AddItem(MemoryItem item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));

            lock (_lock)
            {
                _items.AddFirst(item);
                _lastAccessTime = DateTime.Now;

                // Maintain capacity by removing oldest items
                while (_items.Count > _maxCapacity)
                {
                    _items.RemoveLast();
                }
            }
        }

        /// <summary>
        /// Gets the most recent item of a specific type.
        /// </summary>
        public MemoryItem GetMostRecent(MemoryItemType type)
        {
            lock (_lock)
            {
                return _items.FirstOrDefault(i => i.Type == type);
            }
        }

        /// <summary>
        /// Updates the conversation context.
        /// </summary>
        public void UpdateContext(ConversationContext context)
        {
            lock (_lock)
            {
                Context = context;
                _lastAccessTime = DateTime.Now;
            }
        }

        /// <summary>
        /// Clears all working memory.
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _items.Clear();
                CurrentCommand = null;
                SelectedElementIds = new List<int>();
                Context = null;
                _lastAccessTime = DateTime.Now;
            }
            Logger.Debug("Working memory cleared");
        }

        /// <summary>
        /// Gets the time since last access.
        /// </summary>
        public TimeSpan TimeSinceLastAccess => DateTime.Now - _lastAccessTime;
    }

    /// <summary>
    /// Represents an item stored in memory.
    /// </summary>
    public class MemoryItem
    {
        public MemoryItemType Type { get; set; }
        public string Content { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
        public float Importance { get; set; } = 0.5f;
    }

    /// <summary>
    /// Types of items that can be stored in memory.
    /// </summary>
    public enum MemoryItemType
    {
        Command,
        Response,
        Selection,
        Element,
        Error,
        Clarification,
        UserPreference
    }

    /// <summary>
    /// Tracks the current conversation context.
    /// </summary>
    public class ConversationContext
    {
        public string Topic { get; set; }
        public List<string> RecentEntities { get; set; } = new List<string>();
        public Dictionary<string, string> SlotValues { get; set; } = new Dictionary<string, string>();
        public int TurnCount { get; set; }
        public bool AwaitingClarification { get; set; }
        public string ExpectedInput { get; set; }
    }
}
