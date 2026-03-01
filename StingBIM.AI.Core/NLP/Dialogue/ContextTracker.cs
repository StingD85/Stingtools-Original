// StingBIM.AI.NLP.Dialogue.ContextTracker
// Tracks conversation and design context across turns
// Master Proposal Reference: Part 2.2 Strategy 5 - Contextual Memory Networks

using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using StingBIM.AI.NLP.Pipeline;
using IntentResult = StingBIM.AI.NLP.Pipeline.IntentClassificationResult;

namespace StingBIM.AI.NLP.Dialogue
{
    /// <summary>
    /// Tracks context across conversation turns.
    /// Maintains references to elements, active selections, and user preferences.
    /// Implements Working Memory concept from Part 2.2 (~7 items, seconds duration).
    /// </summary>
    public class ContextTracker
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        // Context capacity limits
        private const int MaxRecentElements = 10;
        private const int MaxRecentActions = 7;
        private const int MaxReferencedItems = 5;

        public ContextTracker()
        {
        }

        /// <summary>
        /// Updates the conversation context based on new input.
        /// </summary>
        public void UpdateContext(
            ConversationContext context,
            IntentClassificationResult intent,
            List<ExtractedEntity> entities,
            IEnumerable<ConversationTurn> history)
        {
            // Update last intent
            context.LastIntent = intent?.Intent;
            context.LastIntentConfidence = intent?.Confidence ?? 0;

            // Update referenced elements from entities
            UpdateReferencedElements(context, entities);

            // Update extracted values
            UpdateExtractedValues(context, entities);

            // Infer context from history
            InferFromHistory(context, history);

            // Update timestamps
            context.LastUpdated = DateTime.Now;

            Logger.Debug($"Context updated: {context.ReferencedElements.Count} elements, {context.ExtractedValues.Count} values");
        }

        /// <summary>
        /// Records a successful action execution.
        /// </summary>
        public void RecordSuccess(ConversationContext context, DesignCommand command)
        {
            context.RecentActions.Insert(0, new ActionRecord
            {
                Command = command,
                Success = true,
                ExecutedAt = DateTime.Now
            });

            // Trim to max
            while (context.RecentActions.Count > MaxRecentActions)
            {
                context.RecentActions.RemoveAt(context.RecentActions.Count - 1);
            }

            // Update defaults based on success
            UpdateDefaultsFromSuccess(context, command);
        }

        /// <summary>
        /// Records a failed action execution.
        /// </summary>
        public void RecordFailure(ConversationContext context, DesignCommand command, string error)
        {
            context.RecentActions.Insert(0, new ActionRecord
            {
                Command = command,
                Success = false,
                Error = error,
                ExecutedAt = DateTime.Now
            });

            // Trim to max
            while (context.RecentActions.Count > MaxRecentActions)
            {
                context.RecentActions.RemoveAt(context.RecentActions.Count - 1);
            }

            // Record failure pattern
            context.FailurePatterns.Add(new FailurePattern
            {
                CommandType = command.CommandType,
                Error = error,
                OccurredAt = DateTime.Now
            });
        }

        /// <summary>
        /// Sets the currently selected elements in Revit.
        /// </summary>
        public void SetSelection(ConversationContext context, IEnumerable<ElementReference> elements)
        {
            context.SelectedElements.Clear();
            context.SelectedElements.AddRange(elements.Take(MaxReferencedItems));

            // Also add to referenced elements
            foreach (var element in context.SelectedElements)
            {
                AddReferencedElement(context, element);
            }
        }

        /// <summary>
        /// Sets the current level/floor context.
        /// </summary>
        public void SetCurrentLevel(ConversationContext context, string levelName, double elevation)
        {
            context.CurrentLevel = levelName;
            context.CurrentElevation = elevation;
        }

        /// <summary>
        /// Sets the current view context.
        /// </summary>
        public void SetCurrentView(ConversationContext context, string viewName, string viewType)
        {
            context.CurrentView = viewName;
            context.CurrentViewType = viewType;
        }

        /// <summary>
        /// Resolves pronoun references like "it", "them", "that".
        /// </summary>
        public ElementReference ResolvePronoun(ConversationContext context, string pronoun)
        {
            pronoun = pronoun.ToLowerInvariant();

            switch (pronoun)
            {
                case "it":
                case "this":
                case "that":
                    // Single reference - return most recent or selected
                    return context.SelectedElements.FirstOrDefault()
                        ?? context.ReferencedElements.FirstOrDefault();

                case "them":
                case "these":
                case "those":
                    // Multiple reference - return all selected
                    return context.SelectedElements.FirstOrDefault();

                case "the last one":
                case "the previous":
                    // Reference to last created/modified element
                    var lastSuccess = context.RecentActions
                        .FirstOrDefault(a => a.Success && a.CreatedElement != null);
                    return lastSuccess?.CreatedElement;

                default:
                    return null;
            }
        }

        /// <summary>
        /// Resolves "here" and location references.
        /// </summary>
        public LocationReference ResolveLocation(ConversationContext context, string locationRef)
        {
            locationRef = locationRef.ToLowerInvariant();

            if (locationRef.Contains("here") || locationRef.Contains("cursor"))
            {
                return new LocationReference
                {
                    Type = LocationType.Cursor,
                    Level = context.CurrentLevel
                };
            }

            if (locationRef.Contains("selected") || locationRef.Contains("this"))
            {
                var selected = context.SelectedElements.FirstOrDefault();
                if (selected != null)
                {
                    return new LocationReference
                    {
                        Type = LocationType.ElementBased,
                        ReferenceElement = selected
                    };
                }
            }

            // Try to resolve as room/space name
            if (context.KnownSpaces.TryGetValue(locationRef, out var spaceRef))
            {
                return new LocationReference
                {
                    Type = LocationType.Named,
                    SpaceName = locationRef,
                    ReferenceElement = spaceRef
                };
            }

            return new LocationReference
            {
                Type = LocationType.Unresolved,
                OriginalText = locationRef
            };
        }

        private void UpdateReferencedElements(ConversationContext context, List<ExtractedEntity> entities)
        {
            var elementRefs = entities
                .Where(e => e.Type == EntityType.ELEMENT_REFERENCE)
                .Select(e => new ElementReference
                {
                    Name = e.Value,
                    ReferencedAt = DateTime.Now
                });

            foreach (var elementRef in elementRefs)
            {
                AddReferencedElement(context, elementRef);
            }
        }

        private void AddReferencedElement(ConversationContext context, ElementReference element)
        {
            // Remove if already exists (to move to front)
            context.ReferencedElements.RemoveAll(e => e.ElementId == element.ElementId);

            // Add to front
            context.ReferencedElements.Insert(0, element);

            // Trim to max
            while (context.ReferencedElements.Count > MaxRecentElements)
            {
                context.ReferencedElements.RemoveAt(context.ReferencedElements.Count - 1);
            }
        }

        private void UpdateExtractedValues(ConversationContext context, List<ExtractedEntity> entities)
        {
            foreach (var entity in entities)
            {
                var key = entity.Type.ToString();
                context.ExtractedValues[key] = entity.NormalizedValue;

                // Also store in type-specific properties
                switch (entity.Type)
                {
                    case EntityType.DIMENSION:
                        context.LastDimension = entity.NormalizedValue;
                        break;
                    case EntityType.ROOM_TYPE:
                        context.LastRoomType = entity.NormalizedValue;
                        break;
                    case EntityType.MATERIAL:
                        context.LastMaterial = entity.NormalizedValue;
                        break;
                    case EntityType.DIRECTION:
                        context.LastDirection = entity.NormalizedValue;
                        break;
                }
            }
        }

        private void InferFromHistory(ConversationContext context, IEnumerable<ConversationTurn> history)
        {
            var recentTurns = history.TakeLast(5).ToList();

            // Check if we're in a multi-step operation
            var lastUserTurn = recentTurns.LastOrDefault(t => t.Role == TurnRole.User);
            var lastAssistantTurn = recentTurns.LastOrDefault(t => t.Role == TurnRole.Assistant);

            if (lastAssistantTurn?.Command != null)
            {
                // Carry forward parameters from last command as potential defaults
                foreach (var (key, value) in lastAssistantTurn.Command.Parameters)
                {
                    if (!context.CurrentDefaults.ContainsKey(key))
                    {
                        context.CurrentDefaults[key] = value;
                    }
                }
            }
        }

        /// <summary>
        /// Records a consulting interaction so follow-up queries retain domain awareness.
        /// </summary>
        public void RecordConsultingActivity(
            ConversationContext context,
            string domain,
            string intent,
            Dictionary<string, string> extractedParameters = null)
        {
            context.ActiveConsultingDomain = domain;
            context.ActiveConsultingIntent = intent;
            context.LastConsultingActivityAt = DateTime.Now;

            // Track topic history (deduplicated, most recent at end)
            if (!context.ConsultingTopicHistory.Contains(intent, StringComparer.OrdinalIgnoreCase))
            {
                context.ConsultingTopicHistory.Add(intent);

                // Keep last 10 topics
                while (context.ConsultingTopicHistory.Count > 10)
                {
                    context.ConsultingTopicHistory.RemoveAt(0);
                }
            }

            // Store extracted parameters so follow-ups can reuse them
            if (extractedParameters != null)
            {
                context.LastConsultingParameters.Clear();
                foreach (var (key, value) in extractedParameters)
                {
                    context.LastConsultingParameters[key] = value;
                }
            }

            Logger.Debug($"Consulting activity recorded: {domain} ({intent}), topics={context.ConsultingTopicHistory.Count}");
        }

        private void UpdateDefaultsFromSuccess(ConversationContext context, DesignCommand command)
        {
            // If user successfully created something with specific parameters,
            // those become reasonable defaults for similar future actions
            foreach (var (key, value) in command.Parameters)
            {
                // Only update for certain parameter types
                if (key == "WALL_TYPE" || key == "FLOOR_TYPE" || key == "MATERIAL" || key == "LEVEL")
                {
                    context.CurrentDefaults[key] = value;
                }
            }
        }
    }

    #region Supporting Classes

    /// <summary>
    /// Holds the current conversation context.
    /// </summary>
    public class ConversationContext
    {
        // Intent tracking
        public string LastIntent { get; set; }
        public float LastIntentConfidence { get; set; }

        // Element references
        public List<ElementReference> SelectedElements { get; set; } = new List<ElementReference>();
        public List<ElementReference> ReferencedElements { get; set; } = new List<ElementReference>();

        // Extracted values from recent conversation
        public Dictionary<string, string> ExtractedValues { get; set; } = new Dictionary<string, string>();

        // Type-specific last values
        public string LastDimension { get; set; }
        public string LastRoomType { get; set; }
        public string LastMaterial { get; set; }
        public string LastDirection { get; set; }

        // Current document context
        public string CurrentLevel { get; set; }
        public double CurrentElevation { get; set; }
        public string CurrentView { get; set; }
        public string CurrentViewType { get; set; }
        public string CurrentPhase { get; set; }

        // Known spaces in the project
        public Dictionary<string, ElementReference> KnownSpaces { get; set; } = new Dictionary<string, ElementReference>(StringComparer.OrdinalIgnoreCase);

        // Action history
        public List<ActionRecord> RecentActions { get; set; } = new List<ActionRecord>();

        // Defaults based on user patterns
        public Dictionary<string, object> CurrentDefaults { get; set; } = new Dictionary<string, object>();

        // Failure tracking
        public List<FailurePattern> FailurePatterns { get; set; } = new List<FailurePattern>();

        // Consulting context
        public string ActiveConsultingDomain { get; set; }
        public string ActiveConsultingIntent { get; set; }
        public List<string> ConsultingTopicHistory { get; set; } = new List<string>();
        public DateTime? LastConsultingActivityAt { get; set; }
        public Dictionary<string, string> LastConsultingParameters { get; set; } = new Dictionary<string, string>();

        // Timestamps
        public DateTime LastUpdated { get; set; }

        /// <summary>
        /// Checks if context has a value for a given key.
        /// </summary>
        public bool HasValue(string key)
        {
            return ExtractedValues.ContainsKey(key) || CurrentDefaults.ContainsKey(key);
        }

        /// <summary>
        /// Gets a value from context.
        /// </summary>
        public object GetValue(string key, object defaultValue = null)
        {
            if (ExtractedValues.TryGetValue(key, out var extracted))
                return extracted;
            if (CurrentDefaults.TryGetValue(key, out var def))
                return def;
            return defaultValue;
        }
    }

    /// <summary>
    /// Reference to a Revit element.
    /// </summary>
    public class ElementReference
    {
        public int ElementId { get; set; }
        public string UniqueId { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string ElementType { get; set; }
        public DateTime ReferencedAt { get; set; }
    }

    /// <summary>
    /// Reference to a location.
    /// </summary>
    public class LocationReference
    {
        public LocationType Type { get; set; }
        public string Level { get; set; }
        public string SpaceName { get; set; }
        public ElementReference ReferenceElement { get; set; }
        public double? X { get; set; }
        public double? Y { get; set; }
        public double? Z { get; set; }
        public string OriginalText { get; set; }
    }

    public enum LocationType
    {
        Cursor,
        ElementBased,
        Coordinate,
        Named,
        Relative,
        Unresolved
    }

    /// <summary>
    /// Record of an executed action.
    /// </summary>
    public class ActionRecord
    {
        public DesignCommand Command { get; set; }
        public bool Success { get; set; }
        public string Error { get; set; }
        public ElementReference CreatedElement { get; set; }
        public DateTime ExecutedAt { get; set; }
    }

    /// <summary>
    /// Pattern of failed actions.
    /// </summary>
    public class FailurePattern
    {
        public string CommandType { get; set; }
        public string Error { get; set; }
        public DateTime OccurredAt { get; set; }
    }

    #endregion
}
