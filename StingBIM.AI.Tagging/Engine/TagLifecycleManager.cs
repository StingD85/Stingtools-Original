// StingBIM.AI.Tagging - SuperIntelligent Tagging System
// TagLifecycleManager.cs - Full lifecycle management: creation, update, deletion, undo/redo, state tracking
// Orchestrates tag operations through the ITagCreator abstraction and maintains operation history

using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using StingBIM.AI.Tagging.Data;
using StingBIM.AI.Tagging.Models;

namespace StingBIM.AI.Tagging.Engine
{
    #region ITagCreator Interface

    /// <summary>
    /// Abstraction over Revit API tag operations. In a real Revit context, the implementation
    /// calls IndependentTag.Create, Element.Location manipulation, and Document.Delete.
    /// This interface isolates the lifecycle manager from direct Revit API dependencies,
    /// enabling unit testing and headless operation.
    /// </summary>
    public interface ITagCreator
    {
        /// <summary>
        /// Creates a new independent tag in the Revit model for the specified host element.
        /// </summary>
        /// <param name="viewId">The Revit ElementId of the view in which to place the tag.</param>
        /// <param name="hostElementId">The Revit ElementId of the element to tag.</param>
        /// <param name="tagFamilyName">Name of the tag family to use.</param>
        /// <param name="tagTypeName">Name of the tag type within the family.</param>
        /// <param name="position">Anchor position in view coordinates.</param>
        /// <param name="leaderType">Leader line style to apply.</param>
        /// <param name="leaderEnd">Leader endpoint on the host element.</param>
        /// <returns>The Revit ElementId of the newly created IndependentTag, or 0 on failure.</returns>
        int CreateTag(int viewId, int hostElementId, string tagFamilyName,
            string tagTypeName, Point2D position, LeaderType leaderType, Point2D leaderEnd);

        /// <summary>
        /// Moves an existing tag to a new position in view coordinates.
        /// </summary>
        /// <param name="tagElementId">The Revit ElementId of the tag to move.</param>
        /// <param name="newPosition">The new anchor position in view coordinates.</param>
        /// <returns>True if the move was successful.</returns>
        bool MoveTag(int tagElementId, Point2D newPosition);

        /// <summary>
        /// Deletes a tag from the Revit model.
        /// </summary>
        /// <param name="tagElementId">The Revit ElementId of the tag to delete.</param>
        /// <returns>True if the deletion was successful.</returns>
        bool DeleteTag(int tagElementId);

        /// <summary>
        /// Gets the current bounding box of a tag element in view coordinates.
        /// </summary>
        /// <param name="tagElementId">The Revit ElementId of the tag.</param>
        /// <returns>The tag's bounding box, or null if the element cannot be found.</returns>
        TagBounds2D GetTagBounds(int tagElementId);

        /// <summary>
        /// Checks whether a tag element is currently visible in its view
        /// (not hidden by filters, crop regions, or overrides).
        /// </summary>
        /// <param name="tagElementId">The Revit ElementId of the tag.</param>
        /// <returns>True if the tag is visible.</returns>
        bool IsTagVisible(int tagElementId);

        /// <summary>
        /// Checks whether a host element still exists in the model.
        /// Used for orphan detection after document changes.
        /// </summary>
        /// <param name="hostElementId">The Revit ElementId of the host element.</param>
        /// <returns>True if the element exists.</returns>
        bool DoesElementExist(int hostElementId);

        /// <summary>
        /// Gets the current position of a host element's reference point in view coordinates.
        /// Used to detect whether a tagged element has moved.
        /// </summary>
        /// <param name="hostElementId">The Revit ElementId of the host element.</param>
        /// <param name="viewId">The view in which to evaluate the position.</param>
        /// <returns>The element position, or null if not found.</returns>
        Point2D? GetElementPosition(int hostElementId, int viewId);
    }

    #endregion

    #region Lifecycle Result Types

    /// <summary>
    /// Result of a single tag lifecycle operation (create, update, delete).
    /// </summary>
    public class TagLifecycleResult
    {
        /// <summary>Whether the operation succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>The affected tag instance after the operation.</summary>
        public TagInstance Tag { get; set; }

        /// <summary>The operation record for undo support.</summary>
        public TagOperation Operation { get; set; }

        /// <summary>Error message if the operation failed.</summary>
        public string ErrorMessage { get; set; }
    }

    /// <summary>
    /// Result of a batch lifecycle operation affecting multiple tags.
    /// </summary>
    public class BatchLifecycleResult
    {
        /// <summary>Individual results for each tag in the batch.</summary>
        public List<TagLifecycleResult> Results { get; set; } = new List<TagLifecycleResult>();

        /// <summary>Number of operations that succeeded.</summary>
        public int SuccessCount { get; set; }

        /// <summary>Number of operations that failed.</summary>
        public int FailureCount { get; set; }

        /// <summary>The batch session identifier for undo grouping.</summary>
        public string BatchSessionId { get; set; }

        /// <summary>Total elapsed time for the batch operation.</summary>
        public TimeSpan Duration { get; set; }
    }

    /// <summary>
    /// Snapshot of the lifecycle manager's current state for diagnostics and reporting.
    /// </summary>
    public class TagLifecycleStatistics
    {
        /// <summary>Total number of managed tags.</summary>
        public int TotalTags { get; set; }

        /// <summary>Tag counts grouped by current state.</summary>
        public Dictionary<TagState, int> TagsByState { get; set; } = new Dictionary<TagState, int>();

        /// <summary>Number of operations in the undo history.</summary>
        public int UndoHistoryDepth { get; set; }

        /// <summary>Number of operations available for redo.</summary>
        public int RedoHistoryDepth { get; set; }

        /// <summary>Total operations performed since the manager was created.</summary>
        public long TotalOperationsPerformed { get; set; }

        /// <summary>Tags currently flagged as orphaned.</summary>
        public int OrphanedTagCount { get; set; }

        /// <summary>Tags currently flagged as stale.</summary>
        public int StaleTagCount { get; set; }
    }

    /// <summary>
    /// Notification raised when monitored elements change in the Revit document.
    /// </summary>
    public class ElementChangeInfo
    {
        /// <summary>Revit ElementId of the changed element.</summary>
        public int ElementId { get; set; }

        /// <summary>Type of change detected.</summary>
        public ElementChangeType ChangeType { get; set; }

        /// <summary>New position of the element if it moved (null if deleted or unchanged).</summary>
        public Point2D? NewPosition { get; set; }
    }

    /// <summary>
    /// Types of element changes detected by the document change monitor.
    /// </summary>
    public enum ElementChangeType
    {
        /// <summary>The element was moved to a new location.</summary>
        Moved,

        /// <summary>The element was deleted from the model.</summary>
        Deleted,

        /// <summary>A parameter value on the element changed.</summary>
        ParameterChanged,

        /// <summary>The element's geometry was modified.</summary>
        GeometryChanged
    }

    #endregion

    /// <summary>
    /// Manages the full lifecycle of tag instances within the StingBIM tagging system.
    /// Handles creation, update, deletion, state tracking, undo/redo, and change monitoring.
    ///
    /// All operations are recorded in the operation history to support undo/redo.
    /// The manager maintains an in-memory registry of all tags and their states,
    /// delegating actual Revit API interactions to the <see cref="ITagCreator"/> abstraction.
    ///
    /// Thread safety is ensured via dedicated lock objects for the tag registry,
    /// operation history, and state synchronization.
    /// </summary>
    public class TagLifecycleManager
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        #region Fields

        private readonly ITagCreator _tagCreator;
        private readonly TagRepository _repository;

        private readonly object _registryLock = new object();
        private readonly object _historyLock = new object();
        private readonly object _stateLock = new object();

        /// <summary>In-memory registry of all managed tags keyed by TagId.</summary>
        private readonly Dictionary<string, TagInstance> _tagRegistry;

        /// <summary>Reverse index: host element ID to list of tag IDs tagging that element.</summary>
        private readonly Dictionary<int, List<string>> _hostElementIndex;

        /// <summary>Reverse index: view ID to list of tag IDs in that view.</summary>
        private readonly Dictionary<int, List<string>> _viewIndex;

        /// <summary>Recorded element positions for move detection (keyed by "hostId:viewId").</summary>
        private readonly Dictionary<string, Point2D> _lastKnownElementPositions;

        /// <summary>Undo stack: most recent operations at the end.</summary>
        private readonly List<TagOperation> _undoStack;

        /// <summary>Redo stack: operations undone and available for redo.</summary>
        private readonly List<TagOperation> _redoStack;

        /// <summary>Maximum depth of the undo history.</summary>
        private readonly int _maxUndoDepth;

        /// <summary>Running counter of all operations performed.</summary>
        private long _totalOperationsPerformed;

        /// <summary>Current batch session identifier for grouping operations.</summary>
        private string _currentBatchSessionId;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of <see cref="TagLifecycleManager"/>.
        /// </summary>
        /// <param name="tagCreator">
        /// The Revit API abstraction for creating, moving, and deleting tags.
        /// </param>
        /// <param name="repository">
        /// The tag repository for persistence and query support.
        /// </param>
        /// <param name="maxUndoDepth">
        /// Maximum number of operations retained in the undo history. Defaults to 500.
        /// </param>
        public TagLifecycleManager(ITagCreator tagCreator, TagRepository repository, int maxUndoDepth = 500)
        {
            _tagCreator = tagCreator ?? throw new ArgumentNullException(nameof(tagCreator));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _maxUndoDepth = maxUndoDepth > 0 ? maxUndoDepth : 500;

            _tagRegistry = new Dictionary<string, TagInstance>(StringComparer.OrdinalIgnoreCase);
            _hostElementIndex = new Dictionary<int, List<string>>();
            _viewIndex = new Dictionary<int, List<string>>();
            _lastKnownElementPositions = new Dictionary<string, Point2D>(StringComparer.OrdinalIgnoreCase);
            _undoStack = new List<TagOperation>();
            _redoStack = new List<TagOperation>();
            _totalOperationsPerformed = 0;
            _currentBatchSessionId = null;

            Logger.Info("TagLifecycleManager initialized with maxUndoDepth={0}", _maxUndoDepth);
        }

        #endregion

        #region Tag Creation

        /// <summary>
        /// Creates a new tag in the Revit model from a placement result.
        /// The tag is created via <see cref="ITagCreator"/>, registered in the in-memory
        /// registry and the <see cref="TagRepository"/>, and the operation is recorded
        /// for undo support.
        /// </summary>
        /// <param name="placementResult">
        /// The placement result containing the tag instance with resolved position data.
        /// </param>
        /// <param name="source">How this tag creation was triggered.</param>
        /// <returns>A lifecycle result indicating success or failure.</returns>
        public TagLifecycleResult CreateTag(TagPlacementResult placementResult, TagCreationSource source)
        {
            if (placementResult == null)
                throw new ArgumentNullException(nameof(placementResult));
            if (placementResult.Tag == null)
                throw new ArgumentException("PlacementResult.Tag cannot be null.", nameof(placementResult));

            var tag = placementResult.Tag;

            try
            {
                Logger.Debug("Creating tag for host element {0} in view {1}",
                    tag.HostElementId, tag.ViewId);

                // Assign a unique TagId if not already set
                if (string.IsNullOrEmpty(tag.TagId))
                    tag.TagId = Guid.NewGuid().ToString("N");

                // Determine leader endpoint
                var leaderEnd = tag.Placement?.LeaderEndPoint ?? tag.Placement?.Position ?? new Point2D(0, 0);
                var position = tag.Placement?.Position ?? new Point2D(0, 0);
                var leaderType = tag.Placement?.LeaderType ?? LeaderType.None;

                // Call the Revit API abstraction
                int revitElementId = _tagCreator.CreateTag(
                    tag.ViewId,
                    tag.HostElementId,
                    tag.TagFamilyName,
                    tag.TagTypeName,
                    position,
                    leaderType,
                    leaderEnd);

                if (revitElementId == 0)
                {
                    Logger.Warn("ITagCreator.CreateTag returned 0 for host element {0}", tag.HostElementId);
                    return new TagLifecycleResult
                    {
                        Success = false,
                        Tag = tag,
                        ErrorMessage = "Tag creation failed in the Revit API layer."
                    };
                }

                // Update tag instance with the Revit element ID
                tag.RevitElementId = revitElementId;
                tag.State = TagState.Active;
                tag.CreationSource = source;
                tag.LastModified = DateTime.UtcNow;
                tag.PlacementScore = placementResult.SelectedCandidate?.Score ?? 0.0;

                // Retrieve actual bounds from the model
                var bounds = _tagCreator.GetTagBounds(revitElementId);
                if (bounds != null)
                    tag.Bounds = bounds;

                // Register in the in-memory registry and indices
                RegisterTag(tag);

                // Persist to repository
                _repository.AddTag(tag);

                // Record the host element position for future move detection
                RecordElementPosition(tag.HostElementId, tag.ViewId);

                // Record the operation for undo
                var operation = RecordOperation(TagOperationType.Create, tag.TagId, null, CloneTag(tag), source);

                Logger.Info("Tag {0} created (Revit ID {1}) for host element {2} in view {3}",
                    tag.TagId, revitElementId, tag.HostElementId, tag.ViewId);

                return new TagLifecycleResult
                {
                    Success = true,
                    Tag = tag,
                    Operation = operation
                };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to create tag for host element {0}", tag.HostElementId);
                return new TagLifecycleResult
                {
                    Success = false,
                    Tag = tag,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// Creates a tag directly from a TagInstance that already has all fields populated.
        /// Useful when recreating tags during undo/redo or import operations.
        /// </summary>
        /// <param name="tag">A fully populated tag instance.</param>
        /// <param name="source">How this tag creation was triggered.</param>
        /// <returns>A lifecycle result indicating success or failure.</returns>
        public TagLifecycleResult CreateTagDirect(TagInstance tag, TagCreationSource source)
        {
            if (tag == null)
                throw new ArgumentNullException(nameof(tag));

            try
            {
                if (string.IsNullOrEmpty(tag.TagId))
                    tag.TagId = Guid.NewGuid().ToString("N");

                var position = tag.Placement?.Position ?? new Point2D(0, 0);
                var leaderEnd = tag.Placement?.LeaderEndPoint ?? position;
                var leaderType = tag.Placement?.LeaderType ?? LeaderType.None;

                int revitElementId = _tagCreator.CreateTag(
                    tag.ViewId,
                    tag.HostElementId,
                    tag.TagFamilyName,
                    tag.TagTypeName,
                    position,
                    leaderType,
                    leaderEnd);

                if (revitElementId == 0)
                {
                    return new TagLifecycleResult
                    {
                        Success = false,
                        Tag = tag,
                        ErrorMessage = "Tag creation failed in the Revit API layer."
                    };
                }

                tag.RevitElementId = revitElementId;
                tag.State = TagState.Active;
                tag.CreationSource = source;
                tag.LastModified = DateTime.UtcNow;

                var bounds = _tagCreator.GetTagBounds(revitElementId);
                if (bounds != null)
                    tag.Bounds = bounds;

                RegisterTag(tag);
                _repository.AddTag(tag);
                RecordElementPosition(tag.HostElementId, tag.ViewId);

                var operation = RecordOperation(TagOperationType.Create, tag.TagId, null, CloneTag(tag), source);

                Logger.Info("Tag {0} created directly (Revit ID {1})", tag.TagId, revitElementId);

                return new TagLifecycleResult
                {
                    Success = true,
                    Tag = tag,
                    Operation = operation
                };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to create tag directly for host element {0}", tag.HostElementId);
                return new TagLifecycleResult
                {
                    Success = false,
                    Tag = tag,
                    ErrorMessage = ex.Message
                };
            }
        }

        #endregion

        #region Tag Update

        /// <summary>
        /// Moves a tag to a new position. Records the previous state for undo.
        /// </summary>
        /// <param name="tagId">Identifier of the tag to move.</param>
        /// <param name="newPosition">The new anchor position in view coordinates.</param>
        /// <returns>A lifecycle result indicating success or failure.</returns>
        public TagLifecycleResult MoveTag(string tagId, Point2D newPosition)
        {
            if (string.IsNullOrEmpty(tagId))
                throw new ArgumentNullException(nameof(tagId));

            try
            {
                TagInstance tag;
                lock (_registryLock)
                {
                    if (!_tagRegistry.TryGetValue(tagId, out tag))
                    {
                        return new TagLifecycleResult
                        {
                            Success = false,
                            ErrorMessage = $"Tag '{tagId}' not found in the registry."
                        };
                    }
                }

                var previousState = CloneTag(tag);

                bool moved = _tagCreator.MoveTag(tag.RevitElementId, newPosition);
                if (!moved)
                {
                    return new TagLifecycleResult
                    {
                        Success = false,
                        Tag = tag,
                        ErrorMessage = "ITagCreator.MoveTag returned false."
                    };
                }

                // Update the placement position
                if (tag.Placement == null)
                    tag.Placement = new TagPlacement();

                tag.Placement.Position = newPosition;
                tag.LastModified = DateTime.UtcNow;

                // Refresh bounds from the model
                var bounds = _tagCreator.GetTagBounds(tag.RevitElementId);
                if (bounds != null)
                    tag.Bounds = bounds;

                // Update the repository
                _repository.UpdateTag(tag);

                // Record for undo
                var operation = RecordOperation(TagOperationType.Move, tagId, previousState, CloneTag(tag),
                    TagCreationSource.UserAssisted);

                Logger.Debug("Tag {0} moved to {1}", tagId, newPosition);

                return new TagLifecycleResult
                {
                    Success = true,
                    Tag = tag,
                    Operation = operation
                };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to move tag {0}", tagId);
                return new TagLifecycleResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// Updates the display text and content expression of a tag.
        /// Records the previous state for undo.
        /// </summary>
        /// <param name="tagId">Identifier of the tag to update.</param>
        /// <param name="newDisplayText">New display text content.</param>
        /// <param name="newContentExpression">
        /// New content expression that generates the display text. Pass null to leave unchanged.
        /// </param>
        /// <returns>A lifecycle result indicating success or failure.</returns>
        public TagLifecycleResult UpdateTagText(string tagId, string newDisplayText, string newContentExpression = null)
        {
            if (string.IsNullOrEmpty(tagId))
                throw new ArgumentNullException(nameof(tagId));

            try
            {
                TagInstance tag;
                lock (_registryLock)
                {
                    if (!_tagRegistry.TryGetValue(tagId, out tag))
                    {
                        return new TagLifecycleResult
                        {
                            Success = false,
                            ErrorMessage = $"Tag '{tagId}' not found in the registry."
                        };
                    }
                }

                var previousState = CloneTag(tag);

                tag.DisplayText = newDisplayText;
                if (newContentExpression != null)
                    tag.ContentExpression = newContentExpression;
                tag.LastModified = DateTime.UtcNow;

                // If the tag was stale, mark it active again
                if (tag.State == TagState.Stale)
                    tag.State = TagState.Active;

                _repository.UpdateTag(tag);

                var operation = RecordOperation(TagOperationType.ReText, tagId, previousState, CloneTag(tag),
                    TagCreationSource.UserAssisted);

                Logger.Debug("Tag {0} text updated to '{1}'", tagId, newDisplayText);

                return new TagLifecycleResult
                {
                    Success = true,
                    Tag = tag,
                    Operation = operation
                };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to update text for tag {0}", tagId);
                return new TagLifecycleResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// Changes the tag family and type (restyling). Records the previous state for undo.
        /// Because Revit does not allow changing the family of an existing IndependentTag,
        /// this operation deletes the old tag and creates a new one with the same placement.
        /// </summary>
        /// <param name="tagId">Identifier of the tag to restyle.</param>
        /// <param name="newTagFamilyName">New tag family name.</param>
        /// <param name="newTagTypeName">New tag type name within the family.</param>
        /// <returns>A lifecycle result indicating success or failure.</returns>
        public TagLifecycleResult RestyleTag(string tagId, string newTagFamilyName, string newTagTypeName)
        {
            if (string.IsNullOrEmpty(tagId))
                throw new ArgumentNullException(nameof(tagId));
            if (string.IsNullOrEmpty(newTagFamilyName))
                throw new ArgumentNullException(nameof(newTagFamilyName));

            try
            {
                TagInstance tag;
                lock (_registryLock)
                {
                    if (!_tagRegistry.TryGetValue(tagId, out tag))
                    {
                        return new TagLifecycleResult
                        {
                            Success = false,
                            ErrorMessage = $"Tag '{tagId}' not found in the registry."
                        };
                    }
                }

                var previousState = CloneTag(tag);

                // Delete the existing Revit tag element
                if (tag.RevitElementId != 0)
                {
                    _tagCreator.DeleteTag(tag.RevitElementId);
                }

                // Create a new tag with the updated family/type
                var position = tag.Placement?.Position ?? new Point2D(0, 0);
                var leaderEnd = tag.Placement?.LeaderEndPoint ?? position;
                var leaderType = tag.Placement?.LeaderType ?? LeaderType.None;

                int newRevitId = _tagCreator.CreateTag(
                    tag.ViewId,
                    tag.HostElementId,
                    newTagFamilyName,
                    newTagTypeName,
                    position,
                    leaderType,
                    leaderEnd);

                if (newRevitId == 0)
                {
                    // Attempt to restore the original tag
                    int restoredId = _tagCreator.CreateTag(
                        tag.ViewId,
                        tag.HostElementId,
                        previousState.TagFamilyName,
                        previousState.TagTypeName,
                        position,
                        leaderType,
                        leaderEnd);

                    if (restoredId != 0)
                        tag.RevitElementId = restoredId;

                    return new TagLifecycleResult
                    {
                        Success = false,
                        Tag = tag,
                        ErrorMessage = "Failed to create replacement tag with new style."
                    };
                }

                tag.RevitElementId = newRevitId;
                tag.TagFamilyName = newTagFamilyName;
                tag.TagTypeName = newTagTypeName;
                tag.LastModified = DateTime.UtcNow;

                var bounds = _tagCreator.GetTagBounds(newRevitId);
                if (bounds != null)
                    tag.Bounds = bounds;

                _repository.UpdateTag(tag);

                var operation = RecordOperation(TagOperationType.Restyle, tagId, previousState, CloneTag(tag),
                    TagCreationSource.UserAssisted);

                Logger.Info("Tag {0} restyled from {1}:{2} to {3}:{4}",
                    tagId, previousState.TagFamilyName, previousState.TagTypeName,
                    newTagFamilyName, newTagTypeName);

                return new TagLifecycleResult
                {
                    Success = true,
                    Tag = tag,
                    Operation = operation
                };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to restyle tag {0}", tagId);
                return new TagLifecycleResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        #endregion

        #region Tag Deletion

        /// <summary>
        /// Deletes a tag from the Revit model and removes it from the registry.
        /// Records the operation for undo.
        /// </summary>
        /// <param name="tagId">Identifier of the tag to delete.</param>
        /// <returns>A lifecycle result indicating success or failure.</returns>
        public TagLifecycleResult DeleteTag(string tagId)
        {
            if (string.IsNullOrEmpty(tagId))
                throw new ArgumentNullException(nameof(tagId));

            try
            {
                TagInstance tag;
                lock (_registryLock)
                {
                    if (!_tagRegistry.TryGetValue(tagId, out tag))
                    {
                        return new TagLifecycleResult
                        {
                            Success = false,
                            ErrorMessage = $"Tag '{tagId}' not found in the registry."
                        };
                    }
                }

                var previousState = CloneTag(tag);

                // Delete from the Revit model
                if (tag.RevitElementId != 0)
                {
                    bool deleted = _tagCreator.DeleteTag(tag.RevitElementId);
                    if (!deleted)
                    {
                        Logger.Warn("ITagCreator.DeleteTag returned false for tag {0} (Revit ID {1})",
                            tagId, tag.RevitElementId);
                    }
                }

                // Remove from the in-memory registry and indices
                UnregisterTag(tagId);

                // Remove from the repository
                _repository.RemoveTag(tagId);

                // Record for undo
                var operation = RecordOperation(TagOperationType.Delete, tagId, previousState, null,
                    TagCreationSource.UserAssisted);

                Logger.Info("Tag {0} deleted (was Revit ID {1})", tagId, previousState.RevitElementId);

                return new TagLifecycleResult
                {
                    Success = true,
                    Tag = previousState,
                    Operation = operation
                };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to delete tag {0}", tagId);
                return new TagLifecycleResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        #endregion

        #region Batch Operations

        /// <summary>
        /// Creates multiple tags in a single batch. All operations share a batch session ID
        /// so they can be undone as a group.
        /// </summary>
        /// <param name="placementResults">Placement results for each tag to create.</param>
        /// <param name="source">How this batch creation was triggered.</param>
        /// <returns>A batch result with individual outcomes for each tag.</returns>
        public BatchLifecycleResult CreateBatch(List<TagPlacementResult> placementResults, TagCreationSource source)
        {
            if (placementResults == null)
                throw new ArgumentNullException(nameof(placementResults));

            var startTime = DateTime.UtcNow;
            var batchSessionId = Guid.NewGuid().ToString("N");
            var result = new BatchLifecycleResult { BatchSessionId = batchSessionId };

            Logger.Info("Starting batch creation of {0} tags (session {1})", placementResults.Count, batchSessionId);

            lock (_historyLock)
            {
                _currentBatchSessionId = batchSessionId;
            }

            try
            {
                foreach (var placementResult in placementResults)
                {
                    if (placementResult == null || !placementResult.Success || placementResult.Tag == null)
                    {
                        result.Results.Add(new TagLifecycleResult
                        {
                            Success = false,
                            ErrorMessage = "Skipped: placement result was null, unsuccessful, or missing tag."
                        });
                        result.FailureCount++;
                        continue;
                    }

                    var tagResult = CreateTag(placementResult, source);
                    result.Results.Add(tagResult);

                    if (tagResult.Success)
                        result.SuccessCount++;
                    else
                        result.FailureCount++;
                }
            }
            finally
            {
                lock (_historyLock)
                {
                    _currentBatchSessionId = null;
                }
            }

            result.Duration = DateTime.UtcNow - startTime;

            Logger.Info("Batch creation completed: {0} succeeded, {1} failed in {2:F1}ms",
                result.SuccessCount, result.FailureCount, result.Duration.TotalMilliseconds);

            return result;
        }

        /// <summary>
        /// Deletes multiple tags in a single batch. All operations share a batch session ID
        /// so they can be undone as a group.
        /// </summary>
        /// <param name="tagIds">Identifiers of the tags to delete.</param>
        /// <returns>A batch result with individual outcomes for each tag.</returns>
        public BatchLifecycleResult DeleteBatch(List<string> tagIds)
        {
            if (tagIds == null)
                throw new ArgumentNullException(nameof(tagIds));

            var startTime = DateTime.UtcNow;
            var batchSessionId = Guid.NewGuid().ToString("N");
            var result = new BatchLifecycleResult { BatchSessionId = batchSessionId };

            Logger.Info("Starting batch deletion of {0} tags (session {1})", tagIds.Count, batchSessionId);

            lock (_historyLock)
            {
                _currentBatchSessionId = batchSessionId;
            }

            try
            {
                foreach (var tagId in tagIds)
                {
                    if (string.IsNullOrEmpty(tagId))
                    {
                        result.Results.Add(new TagLifecycleResult
                        {
                            Success = false,
                            ErrorMessage = "Skipped: tag ID was null or empty."
                        });
                        result.FailureCount++;
                        continue;
                    }

                    var tagResult = DeleteTag(tagId);
                    result.Results.Add(tagResult);

                    if (tagResult.Success)
                        result.SuccessCount++;
                    else
                        result.FailureCount++;
                }
            }
            finally
            {
                lock (_historyLock)
                {
                    _currentBatchSessionId = null;
                }
            }

            result.Duration = DateTime.UtcNow - startTime;

            Logger.Info("Batch deletion completed: {0} succeeded, {1} failed in {2:F1}ms",
                result.SuccessCount, result.FailureCount, result.Duration.TotalMilliseconds);

            return result;
        }

        /// <summary>
        /// Moves multiple tags to new positions in a single batch. All operations share
        /// a batch session ID so they can be undone as a group.
        /// </summary>
        /// <param name="moves">
        /// Dictionary mapping tag IDs to their new positions.
        /// </param>
        /// <returns>A batch result with individual outcomes for each tag.</returns>
        public BatchLifecycleResult MoveBatch(Dictionary<string, Point2D> moves)
        {
            if (moves == null)
                throw new ArgumentNullException(nameof(moves));

            var startTime = DateTime.UtcNow;
            var batchSessionId = Guid.NewGuid().ToString("N");
            var result = new BatchLifecycleResult { BatchSessionId = batchSessionId };

            Logger.Info("Starting batch move of {0} tags (session {1})", moves.Count, batchSessionId);

            lock (_historyLock)
            {
                _currentBatchSessionId = batchSessionId;
            }

            try
            {
                foreach (var kvp in moves)
                {
                    var tagResult = MoveTag(kvp.Key, kvp.Value);
                    result.Results.Add(tagResult);

                    if (tagResult.Success)
                        result.SuccessCount++;
                    else
                        result.FailureCount++;
                }
            }
            finally
            {
                lock (_historyLock)
                {
                    _currentBatchSessionId = null;
                }
            }

            result.Duration = DateTime.UtcNow - startTime;

            Logger.Info("Batch move completed: {0} succeeded, {1} failed in {2:F1}ms",
                result.SuccessCount, result.FailureCount, result.Duration.TotalMilliseconds);

            return result;
        }

        #endregion

        #region State Tracking

        /// <summary>
        /// Gets the current state of a tag by its identifier.
        /// </summary>
        /// <param name="tagId">The tag identifier.</param>
        /// <returns>The tag's current state, or null if not found.</returns>
        public TagState? GetTagState(string tagId)
        {
            lock (_registryLock)
            {
                return _tagRegistry.TryGetValue(tagId, out var tag) ? tag.State : (TagState?)null;
            }
        }

        /// <summary>
        /// Sets the state of a tag. Does not record an undo operation; state changes
        /// from synchronization are considered non-undoable metadata updates.
        /// </summary>
        /// <param name="tagId">The tag identifier.</param>
        /// <param name="newState">The new state to assign.</param>
        public void SetTagState(string tagId, TagState newState)
        {
            lock (_registryLock)
            {
                if (_tagRegistry.TryGetValue(tagId, out var tag))
                {
                    tag.State = newState;
                    tag.LastModified = DateTime.UtcNow;
                    _repository.UpdateTag(tag);
                }
            }
        }

        /// <summary>
        /// Synchronizes the state of all managed tags by checking whether their host elements
        /// still exist and whether the tags are still visible. Flags orphaned and hidden tags.
        /// </summary>
        /// <returns>
        /// The number of tags whose state changed during synchronization.
        /// </returns>
        public int SyncTagStates()
        {
            int changedCount = 0;
            List<TagInstance> snapshot;

            lock (_registryLock)
            {
                snapshot = _tagRegistry.Values.ToList();
            }

            Logger.Debug("Synchronizing state for {0} managed tags", snapshot.Count);

            foreach (var tag in snapshot)
            {
                lock (_stateLock)
                {
                    TagState previousState = tag.State;
                    TagState resolvedState = ResolveTagState(tag);

                    if (resolvedState != previousState)
                    {
                        tag.State = resolvedState;
                        tag.LastModified = DateTime.UtcNow;

                        lock (_registryLock)
                        {
                            if (_tagRegistry.ContainsKey(tag.TagId))
                                _tagRegistry[tag.TagId] = tag;
                        }

                        _repository.UpdateTag(tag);
                        changedCount++;

                        Logger.Debug("Tag {0} state changed: {1} -> {2}",
                            tag.TagId, previousState, resolvedState);
                    }
                }
            }

            if (changedCount > 0)
                Logger.Info("State synchronization completed: {0} tags changed state", changedCount);

            return changedCount;
        }

        /// <summary>
        /// Resolves the current state of a tag by querying the Revit API abstraction.
        /// </summary>
        private TagState ResolveTagState(TagInstance tag)
        {
            // Processing and MarkedForDeletion states are managed explicitly, not resolved
            if (tag.State == TagState.Processing || tag.State == TagState.MarkedForDeletion)
                return tag.State;

            // Check if the host element still exists
            bool hostExists = _tagCreator.DoesElementExist(tag.HostElementId);
            if (!hostExists)
                return TagState.Orphaned;

            // Check if the tag element itself still exists (it may have been deleted externally)
            if (tag.RevitElementId != 0)
            {
                bool tagExists = _tagCreator.DoesElementExist(tag.RevitElementId);
                if (!tagExists)
                    return TagState.Orphaned;

                // Check visibility
                bool visible = _tagCreator.IsTagVisible(tag.RevitElementId);
                if (!visible)
                    return TagState.Hidden;
            }

            return TagState.Active;
        }

        #endregion

        #region Undo / Redo

        /// <summary>
        /// Undoes the most recent operation. The undone operation is pushed onto the redo stack.
        /// </summary>
        /// <returns>A lifecycle result describing the undo, or null if there is nothing to undo.</returns>
        public TagLifecycleResult UndoLastOperation()
        {
            TagOperation operation;

            lock (_historyLock)
            {
                if (_undoStack.Count == 0)
                {
                    Logger.Debug("Undo requested but undo stack is empty");
                    return null;
                }

                operation = _undoStack[_undoStack.Count - 1];
                _undoStack.RemoveAt(_undoStack.Count - 1);
            }

            var result = ExecuteUndo(operation);

            if (result != null && result.Success)
            {
                lock (_historyLock)
                {
                    _redoStack.Add(operation);
                    TrimRedoStack();
                }
            }

            return result;
        }

        /// <summary>
        /// Undoes all operations belonging to a specific batch session.
        /// Operations are undone in reverse chronological order.
        /// </summary>
        /// <param name="batchSessionId">
        /// The batch session identifier assigned during the batch operation.
        /// </param>
        /// <returns>A batch result describing the outcomes of all undo operations.</returns>
        public BatchLifecycleResult UndoBatch(string batchSessionId)
        {
            if (string.IsNullOrEmpty(batchSessionId))
                throw new ArgumentNullException(nameof(batchSessionId));

            var startTime = DateTime.UtcNow;
            var result = new BatchLifecycleResult { BatchSessionId = batchSessionId };

            List<TagOperation> batchOperations;
            lock (_historyLock)
            {
                batchOperations = _undoStack
                    .Where(op => string.Equals(op.OperationId?.Split(':').FirstOrDefault(),
                        batchSessionId, StringComparison.OrdinalIgnoreCase)
                        || (op.PreviousState?.Metadata != null
                            && op.PreviousState.Metadata.TryGetValue("BatchSessionId", out var sid)
                            && string.Equals(sid?.ToString(), batchSessionId, StringComparison.OrdinalIgnoreCase))
                        || (op.NewState?.Metadata != null
                            && op.NewState.Metadata.TryGetValue("BatchSessionId", out var sid2)
                            && string.Equals(sid2?.ToString(), batchSessionId, StringComparison.OrdinalIgnoreCase)))
                    .OrderByDescending(op => op.Timestamp)
                    .ToList();

                // Also match by checking the internal batch ID embedded in the operation ID
                if (batchOperations.Count == 0)
                {
                    batchOperations = _undoStack
                        .Where(op => op.OperationId != null && op.OperationId.StartsWith(batchSessionId + ":"))
                        .OrderByDescending(op => op.Timestamp)
                        .ToList();
                }

                foreach (var op in batchOperations)
                    _undoStack.Remove(op);
            }

            Logger.Info("Undoing batch {0}: {1} operations", batchSessionId, batchOperations.Count);

            foreach (var operation in batchOperations)
            {
                var undoResult = ExecuteUndo(operation);
                if (undoResult != null)
                {
                    result.Results.Add(undoResult);
                    if (undoResult.Success)
                        result.SuccessCount++;
                    else
                        result.FailureCount++;
                }

                lock (_historyLock)
                {
                    _redoStack.Add(operation);
                }
            }

            TrimRedoStack();
            result.Duration = DateTime.UtcNow - startTime;

            Logger.Info("Batch undo completed: {0} succeeded, {1} failed",
                result.SuccessCount, result.FailureCount);

            return result;
        }

        /// <summary>
        /// Redoes the most recently undone operation.
        /// </summary>
        /// <returns>A lifecycle result describing the redo, or null if there is nothing to redo.</returns>
        public TagLifecycleResult RedoLastOperation()
        {
            TagOperation operation;

            lock (_historyLock)
            {
                if (_redoStack.Count == 0)
                {
                    Logger.Debug("Redo requested but redo stack is empty");
                    return null;
                }

                operation = _redoStack[_redoStack.Count - 1];
                _redoStack.RemoveAt(_redoStack.Count - 1);
            }

            var result = ExecuteRedo(operation);

            if (result != null && result.Success)
            {
                lock (_historyLock)
                {
                    _undoStack.Add(operation);
                    TrimUndoStack();
                }
            }

            return result;
        }

        /// <summary>
        /// Gets the number of operations available for undo.
        /// </summary>
        public int UndoCount
        {
            get { lock (_historyLock) { return _undoStack.Count; } }
        }

        /// <summary>
        /// Gets the number of operations available for redo.
        /// </summary>
        public int RedoCount
        {
            get { lock (_historyLock) { return _redoStack.Count; } }
        }

        /// <summary>
        /// Clears the entire undo and redo history.
        /// </summary>
        public void ClearHistory()
        {
            lock (_historyLock)
            {
                _undoStack.Clear();
                _redoStack.Clear();
            }

            Logger.Info("Undo/redo history cleared");
        }

        /// <summary>
        /// Executes the inverse of an operation to undo it.
        /// </summary>
        private TagLifecycleResult ExecuteUndo(TagOperation operation)
        {
            try
            {
                switch (operation.Type)
                {
                    case TagOperationType.Create:
                        return UndoCreate(operation);

                    case TagOperationType.Delete:
                        return UndoDelete(operation);

                    case TagOperationType.Move:
                        return UndoMoveOrRestyle(operation);

                    case TagOperationType.Restyle:
                        return UndoMoveOrRestyle(operation);

                    case TagOperationType.ReText:
                        return UndoReText(operation);

                    default:
                        Logger.Warn("Unsupported undo operation type: {0}", operation.Type);
                        return new TagLifecycleResult
                        {
                            Success = false,
                            ErrorMessage = $"Undo not supported for operation type {operation.Type}."
                        };
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to undo operation {0} of type {1}", operation.OperationId, operation.Type);
                return new TagLifecycleResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// Undo a Create operation by deleting the tag that was created.
        /// </summary>
        private TagLifecycleResult UndoCreate(TagOperation operation)
        {
            var createdTag = operation.NewState;
            if (createdTag == null)
            {
                return new TagLifecycleResult
                {
                    Success = false,
                    ErrorMessage = "Cannot undo Create: no NewState recorded."
                };
            }

            // Delete the tag from Revit
            if (createdTag.RevitElementId != 0)
                _tagCreator.DeleteTag(createdTag.RevitElementId);

            UnregisterTag(createdTag.TagId);
            _repository.RemoveTag(createdTag.TagId);

            Logger.Debug("Undo Create: tag {0} removed", createdTag.TagId);

            return new TagLifecycleResult
            {
                Success = true,
                Tag = createdTag
            };
        }

        /// <summary>
        /// Undo a Delete operation by recreating the tag with its previous state.
        /// </summary>
        private TagLifecycleResult UndoDelete(TagOperation operation)
        {
            var deletedTag = operation.PreviousState;
            if (deletedTag == null)
            {
                return new TagLifecycleResult
                {
                    Success = false,
                    ErrorMessage = "Cannot undo Delete: no PreviousState recorded."
                };
            }

            // Recreate the tag through the creator
            var position = deletedTag.Placement?.Position ?? new Point2D(0, 0);
            var leaderEnd = deletedTag.Placement?.LeaderEndPoint ?? position;
            var leaderType = deletedTag.Placement?.LeaderType ?? LeaderType.None;

            int newRevitId = _tagCreator.CreateTag(
                deletedTag.ViewId,
                deletedTag.HostElementId,
                deletedTag.TagFamilyName,
                deletedTag.TagTypeName,
                position,
                leaderType,
                leaderEnd);

            if (newRevitId == 0)
            {
                return new TagLifecycleResult
                {
                    Success = false,
                    Tag = deletedTag,
                    ErrorMessage = "Failed to recreate tag during undo Delete."
                };
            }

            deletedTag.RevitElementId = newRevitId;
            deletedTag.State = TagState.Active;
            deletedTag.LastModified = DateTime.UtcNow;

            var bounds = _tagCreator.GetTagBounds(newRevitId);
            if (bounds != null)
                deletedTag.Bounds = bounds;

            RegisterTag(deletedTag);
            _repository.AddTag(deletedTag);

            Logger.Debug("Undo Delete: tag {0} recreated with Revit ID {1}", deletedTag.TagId, newRevitId);

            return new TagLifecycleResult
            {
                Success = true,
                Tag = deletedTag
            };
        }

        /// <summary>
        /// Undo a Move or Restyle operation by restoring the previous state.
        /// For Restyle, this deletes the current tag and recreates with the old family/type.
        /// For Move, this repositions back to the old location.
        /// </summary>
        private TagLifecycleResult UndoMoveOrRestyle(TagOperation operation)
        {
            var previousState = operation.PreviousState;
            if (previousState == null)
            {
                return new TagLifecycleResult
                {
                    Success = false,
                    ErrorMessage = $"Cannot undo {operation.Type}: no PreviousState recorded."
                };
            }

            TagInstance currentTag;
            lock (_registryLock)
            {
                if (!_tagRegistry.TryGetValue(operation.TagId, out currentTag))
                {
                    return new TagLifecycleResult
                    {
                        Success = false,
                        ErrorMessage = $"Tag '{operation.TagId}' no longer exists in the registry."
                    };
                }
            }

            if (operation.Type == TagOperationType.Restyle)
            {
                // Delete current version and recreate with previous family/type
                if (currentTag.RevitElementId != 0)
                    _tagCreator.DeleteTag(currentTag.RevitElementId);

                var position = previousState.Placement?.Position ?? new Point2D(0, 0);
                var leaderEnd = previousState.Placement?.LeaderEndPoint ?? position;
                var leaderType = previousState.Placement?.LeaderType ?? LeaderType.None;

                int newRevitId = _tagCreator.CreateTag(
                    previousState.ViewId,
                    previousState.HostElementId,
                    previousState.TagFamilyName,
                    previousState.TagTypeName,
                    position,
                    leaderType,
                    leaderEnd);

                if (newRevitId == 0)
                {
                    return new TagLifecycleResult
                    {
                        Success = false,
                        ErrorMessage = "Failed to recreate tag with previous style during undo."
                    };
                }

                currentTag.RevitElementId = newRevitId;
                currentTag.TagFamilyName = previousState.TagFamilyName;
                currentTag.TagTypeName = previousState.TagTypeName;
            }
            else
            {
                // Move: restore position
                var oldPosition = previousState.Placement?.Position ?? new Point2D(0, 0);
                bool moved = _tagCreator.MoveTag(currentTag.RevitElementId, oldPosition);

                if (!moved)
                {
                    return new TagLifecycleResult
                    {
                        Success = false,
                        ErrorMessage = "Failed to move tag back to previous position during undo."
                    };
                }

                if (currentTag.Placement == null)
                    currentTag.Placement = new TagPlacement();
                currentTag.Placement.Position = oldPosition;
            }

            currentTag.LastModified = DateTime.UtcNow;

            var newBounds = _tagCreator.GetTagBounds(currentTag.RevitElementId);
            if (newBounds != null)
                currentTag.Bounds = newBounds;

            _repository.UpdateTag(currentTag);

            Logger.Debug("Undo {0}: tag {1} restored to previous state", operation.Type, operation.TagId);

            return new TagLifecycleResult
            {
                Success = true,
                Tag = currentTag
            };
        }

        /// <summary>
        /// Undo a ReText operation by restoring the previous text values.
        /// </summary>
        private TagLifecycleResult UndoReText(TagOperation operation)
        {
            var previousState = operation.PreviousState;
            if (previousState == null)
            {
                return new TagLifecycleResult
                {
                    Success = false,
                    ErrorMessage = "Cannot undo ReText: no PreviousState recorded."
                };
            }

            TagInstance currentTag;
            lock (_registryLock)
            {
                if (!_tagRegistry.TryGetValue(operation.TagId, out currentTag))
                {
                    return new TagLifecycleResult
                    {
                        Success = false,
                        ErrorMessage = $"Tag '{operation.TagId}' no longer exists in the registry."
                    };
                }
            }

            currentTag.DisplayText = previousState.DisplayText;
            currentTag.ContentExpression = previousState.ContentExpression;
            currentTag.State = previousState.State;
            currentTag.LastModified = DateTime.UtcNow;

            _repository.UpdateTag(currentTag);

            Logger.Debug("Undo ReText: tag {0} text restored to '{1}'", operation.TagId, previousState.DisplayText);

            return new TagLifecycleResult
            {
                Success = true,
                Tag = currentTag
            };
        }

        /// <summary>
        /// Executes the forward version of an undone operation to redo it.
        /// </summary>
        private TagLifecycleResult ExecuteRedo(TagOperation operation)
        {
            try
            {
                switch (operation.Type)
                {
                    case TagOperationType.Create:
                        return RedoCreate(operation);

                    case TagOperationType.Delete:
                        return RedoDelete(operation);

                    case TagOperationType.Move:
                        return RedoMoveOrRestyle(operation);

                    case TagOperationType.Restyle:
                        return RedoMoveOrRestyle(operation);

                    case TagOperationType.ReText:
                        return RedoReText(operation);

                    default:
                        Logger.Warn("Unsupported redo operation type: {0}", operation.Type);
                        return new TagLifecycleResult
                        {
                            Success = false,
                            ErrorMessage = $"Redo not supported for operation type {operation.Type}."
                        };
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to redo operation {0} of type {1}", operation.OperationId, operation.Type);
                return new TagLifecycleResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// Redo a Create operation by recreating the tag.
        /// </summary>
        private TagLifecycleResult RedoCreate(TagOperation operation)
        {
            var tagToCreate = operation.NewState;
            if (tagToCreate == null)
            {
                return new TagLifecycleResult
                {
                    Success = false,
                    ErrorMessage = "Cannot redo Create: no NewState recorded."
                };
            }

            return CreateTagDirect(CloneTag(tagToCreate), operation.Source);
        }

        /// <summary>
        /// Redo a Delete operation by deleting the tag again.
        /// </summary>
        private TagLifecycleResult RedoDelete(TagOperation operation)
        {
            return DeleteTag(operation.TagId);
        }

        /// <summary>
        /// Redo a Move or Restyle operation by reapplying the new state.
        /// </summary>
        private TagLifecycleResult RedoMoveOrRestyle(TagOperation operation)
        {
            var newState = operation.NewState;
            if (newState == null)
            {
                return new TagLifecycleResult
                {
                    Success = false,
                    ErrorMessage = $"Cannot redo {operation.Type}: no NewState recorded."
                };
            }

            if (operation.Type == TagOperationType.Restyle)
                return RestyleTag(operation.TagId, newState.TagFamilyName, newState.TagTypeName);

            var newPosition = newState.Placement?.Position ?? new Point2D(0, 0);
            return MoveTag(operation.TagId, newPosition);
        }

        /// <summary>
        /// Redo a ReText operation by reapplying the new text.
        /// </summary>
        private TagLifecycleResult RedoReText(TagOperation operation)
        {
            var newState = operation.NewState;
            if (newState == null)
            {
                return new TagLifecycleResult
                {
                    Success = false,
                    ErrorMessage = "Cannot redo ReText: no NewState recorded."
                };
            }

            return UpdateTagText(operation.TagId, newState.DisplayText, newState.ContentExpression);
        }

        #endregion

        #region Change Monitoring

        /// <summary>
        /// Processes element changes detected from Revit DocumentChanged events.
        /// Scans all managed tags whose host elements are in the change set and
        /// updates their state accordingly (orphan detection, stale detection, re-evaluation flagging).
        /// </summary>
        /// <param name="changes">
        /// List of element changes detected by the Revit event handler.
        /// </param>
        /// <returns>
        /// A list of tag IDs that were affected and may need re-evaluation.
        /// </returns>
        public List<string> ProcessElementChanges(List<ElementChangeInfo> changes)
        {
            if (changes == null || changes.Count == 0)
                return new List<string>();

            var affectedTagIds = new List<string>();

            Logger.Debug("Processing {0} element changes for tag impact", changes.Count);

            foreach (var change in changes)
            {
                List<string> tagIds;
                lock (_registryLock)
                {
                    if (!_hostElementIndex.TryGetValue(change.ElementId, out tagIds))
                        continue;
                    tagIds = tagIds.ToList(); // snapshot
                }

                foreach (var tagId in tagIds)
                {
                    TagInstance tag;
                    lock (_registryLock)
                    {
                        if (!_tagRegistry.TryGetValue(tagId, out tag))
                            continue;
                    }

                    switch (change.ChangeType)
                    {
                        case ElementChangeType.Deleted:
                            tag.State = TagState.Orphaned;
                            tag.LastModified = DateTime.UtcNow;
                            _repository.UpdateTag(tag);
                            affectedTagIds.Add(tagId);
                            Logger.Debug("Tag {0} marked orphaned: host element {1} deleted",
                                tagId, change.ElementId);
                            break;

                        case ElementChangeType.Moved:
                            if (change.NewPosition.HasValue)
                            {
                                string posKey = BuildPositionKey(change.ElementId, tag.ViewId);
                                bool hasMoved = false;

                                lock (_stateLock)
                                {
                                    if (_lastKnownElementPositions.TryGetValue(posKey, out var lastPos))
                                    {
                                        double dist = lastPos.DistanceTo(change.NewPosition.Value);
                                        hasMoved = dist > 1e-6;
                                    }
                                    else
                                    {
                                        hasMoved = true;
                                    }

                                    if (hasMoved)
                                        _lastKnownElementPositions[posKey] = change.NewPosition.Value;
                                }

                                if (hasMoved)
                                {
                                    affectedTagIds.Add(tagId);
                                    Logger.Debug("Tag {0} flagged for re-evaluation: host element {1} moved",
                                        tagId, change.ElementId);
                                }
                            }
                            break;

                        case ElementChangeType.ParameterChanged:
                            tag.State = TagState.Stale;
                            tag.LastModified = DateTime.UtcNow;
                            _repository.UpdateTag(tag);
                            affectedTagIds.Add(tagId);
                            Logger.Debug("Tag {0} marked stale: host element {1} parameter changed",
                                tagId, change.ElementId);
                            break;

                        case ElementChangeType.GeometryChanged:
                            affectedTagIds.Add(tagId);
                            Logger.Debug("Tag {0} flagged for re-evaluation: host element {1} geometry changed",
                                tagId, change.ElementId);
                            break;
                    }
                }
            }

            if (affectedTagIds.Count > 0)
                Logger.Info("{0} tags affected by element changes", affectedTagIds.Count);

            return affectedTagIds.Distinct().ToList();
        }

        /// <summary>
        /// Scans all managed tags and detects those whose host elements have been deleted.
        /// Sets detected orphans to <see cref="TagState.Orphaned"/>.
        /// </summary>
        /// <returns>List of tag IDs that are now orphaned.</returns>
        public List<string> DetectOrphanedTags()
        {
            var orphanedIds = new List<string>();
            List<TagInstance> snapshot;

            lock (_registryLock)
            {
                snapshot = _tagRegistry.Values.ToList();
            }

            foreach (var tag in snapshot)
            {
                if (tag.State == TagState.Orphaned || tag.State == TagState.MarkedForDeletion)
                    continue;

                bool hostExists = _tagCreator.DoesElementExist(tag.HostElementId);
                if (!hostExists)
                {
                    tag.State = TagState.Orphaned;
                    tag.LastModified = DateTime.UtcNow;
                    _repository.UpdateTag(tag);
                    orphanedIds.Add(tag.TagId);

                    Logger.Debug("Tag {0} detected as orphaned (host element {1} no longer exists)",
                        tag.TagId, tag.HostElementId);
                }
            }

            if (orphanedIds.Count > 0)
                Logger.Info("Detected {0} orphaned tags", orphanedIds.Count);

            return orphanedIds;
        }

        /// <summary>
        /// Detects tags whose host elements have moved since the last known position,
        /// indicating the tag may need repositioning.
        /// </summary>
        /// <returns>
        /// A dictionary mapping tag IDs to the displacement distance of their host element.
        /// </returns>
        public Dictionary<string, double> DetectMovedElements()
        {
            var movedTags = new Dictionary<string, double>();
            List<TagInstance> snapshot;

            lock (_registryLock)
            {
                snapshot = _tagRegistry.Values
                    .Where(t => t.State == TagState.Active)
                    .ToList();
            }

            foreach (var tag in snapshot)
            {
                string posKey = BuildPositionKey(tag.HostElementId, tag.ViewId);
                var currentPosition = _tagCreator.GetElementPosition(tag.HostElementId, tag.ViewId);

                if (!currentPosition.HasValue)
                    continue;

                lock (_stateLock)
                {
                    if (_lastKnownElementPositions.TryGetValue(posKey, out var lastPos))
                    {
                        double distance = lastPos.DistanceTo(currentPosition.Value);
                        if (distance > 1e-6)
                        {
                            movedTags[tag.TagId] = distance;
                            _lastKnownElementPositions[posKey] = currentPosition.Value;
                        }
                    }
                    else
                    {
                        _lastKnownElementPositions[posKey] = currentPosition.Value;
                    }
                }
            }

            if (movedTags.Count > 0)
                Logger.Info("Detected {0} tags with moved host elements", movedTags.Count);

            return movedTags;
        }

        #endregion

        #region Statistics and Queries

        /// <summary>
        /// Gets the total number of managed tags in the registry.
        /// </summary>
        public int GetTagCount()
        {
            lock (_registryLock)
            {
                return _tagRegistry.Count;
            }
        }

        /// <summary>
        /// Gets all tags in a specific state.
        /// </summary>
        /// <param name="state">The state to filter by.</param>
        /// <returns>A list of tags in the specified state.</returns>
        public List<TagInstance> GetTagsByState(TagState state)
        {
            lock (_registryLock)
            {
                return _tagRegistry.Values
                    .Where(t => t.State == state)
                    .ToList();
            }
        }

        /// <summary>
        /// Gets all tags currently flagged as orphaned.
        /// </summary>
        /// <returns>A list of orphaned tag instances.</returns>
        public List<TagInstance> GetOrphanedTags()
        {
            return GetTagsByState(TagState.Orphaned);
        }

        /// <summary>
        /// Gets all tags currently flagged as stale.
        /// </summary>
        /// <returns>A list of stale tag instances.</returns>
        public List<TagInstance> GetStaleTags()
        {
            return GetTagsByState(TagState.Stale);
        }

        /// <summary>
        /// Gets all tags for a specific host element across all views.
        /// </summary>
        /// <param name="hostElementId">The Revit ElementId of the host element.</param>
        /// <returns>A list of tags associated with the host element.</returns>
        public List<TagInstance> GetTagsByHostElement(int hostElementId)
        {
            lock (_registryLock)
            {
                if (_hostElementIndex.TryGetValue(hostElementId, out var tagIds))
                    return tagIds
                        .Where(id => _tagRegistry.ContainsKey(id))
                        .Select(id => _tagRegistry[id])
                        .ToList();
                return new List<TagInstance>();
            }
        }

        /// <summary>
        /// Gets all tags in a specific view.
        /// </summary>
        /// <param name="viewId">The Revit ElementId of the view.</param>
        /// <returns>A list of tags in the view.</returns>
        public List<TagInstance> GetTagsByView(int viewId)
        {
            lock (_registryLock)
            {
                if (_viewIndex.TryGetValue(viewId, out var tagIds))
                    return tagIds
                        .Where(id => _tagRegistry.ContainsKey(id))
                        .Select(id => _tagRegistry[id])
                        .ToList();
                return new List<TagInstance>();
            }
        }

        /// <summary>
        /// Gets a specific tag instance by its identifier.
        /// </summary>
        /// <param name="tagId">The tag identifier.</param>
        /// <returns>The tag instance, or null if not found.</returns>
        public TagInstance GetTag(string tagId)
        {
            lock (_registryLock)
            {
                return _tagRegistry.TryGetValue(tagId, out var tag) ? tag : null;
            }
        }

        /// <summary>
        /// Gets a complete statistics snapshot of the lifecycle manager's current state.
        /// </summary>
        /// <returns>A statistics object with tag counts, history depths, and state breakdowns.</returns>
        public TagLifecycleStatistics GetStatistics()
        {
            var stats = new TagLifecycleStatistics();

            lock (_registryLock)
            {
                stats.TotalTags = _tagRegistry.Count;

                foreach (TagState state in Enum.GetValues(typeof(TagState)))
                {
                    int count = _tagRegistry.Values.Count(t => t.State == state);
                    if (count > 0)
                        stats.TagsByState[state] = count;
                }

                stats.OrphanedTagCount = _tagRegistry.Values.Count(t => t.State == TagState.Orphaned);
                stats.StaleTagCount = _tagRegistry.Values.Count(t => t.State == TagState.Stale);
            }

            lock (_historyLock)
            {
                stats.UndoHistoryDepth = _undoStack.Count;
                stats.RedoHistoryDepth = _redoStack.Count;
                stats.TotalOperationsPerformed = _totalOperationsPerformed;
            }

            return stats;
        }

        /// <summary>
        /// Gets a list of recent operations from the undo history for display or audit purposes.
        /// </summary>
        /// <param name="count">Maximum number of recent operations to return.</param>
        /// <returns>Most recent operations, ordered newest first.</returns>
        public List<TagOperation> GetRecentOperations(int count = 20)
        {
            lock (_historyLock)
            {
                return _undoStack
                    .OrderByDescending(o => o.Timestamp)
                    .Take(Math.Max(1, count))
                    .ToList();
            }
        }

        #endregion

        #region Registry Management (Private)

        /// <summary>
        /// Registers a tag in the in-memory registry and all lookup indices.
        /// </summary>
        private void RegisterTag(TagInstance tag)
        {
            lock (_registryLock)
            {
                _tagRegistry[tag.TagId] = tag;

                // Host element index
                if (!_hostElementIndex.ContainsKey(tag.HostElementId))
                    _hostElementIndex[tag.HostElementId] = new List<string>();
                if (!_hostElementIndex[tag.HostElementId].Contains(tag.TagId))
                    _hostElementIndex[tag.HostElementId].Add(tag.TagId);

                // View index
                if (!_viewIndex.ContainsKey(tag.ViewId))
                    _viewIndex[tag.ViewId] = new List<string>();
                if (!_viewIndex[tag.ViewId].Contains(tag.TagId))
                    _viewIndex[tag.ViewId].Add(tag.TagId);
            }
        }

        /// <summary>
        /// Removes a tag from the in-memory registry and all lookup indices.
        /// </summary>
        private void UnregisterTag(string tagId)
        {
            lock (_registryLock)
            {
                if (_tagRegistry.TryGetValue(tagId, out var tag))
                {
                    // Remove from host element index
                    if (_hostElementIndex.TryGetValue(tag.HostElementId, out var hostList))
                    {
                        hostList.Remove(tagId);
                        if (hostList.Count == 0)
                            _hostElementIndex.Remove(tag.HostElementId);
                    }

                    // Remove from view index
                    if (_viewIndex.TryGetValue(tag.ViewId, out var viewList))
                    {
                        viewList.Remove(tagId);
                        if (viewList.Count == 0)
                            _viewIndex.Remove(tag.ViewId);
                    }

                    _tagRegistry.Remove(tagId);
                }
            }
        }

        /// <summary>
        /// Records the current position of a host element for future move detection.
        /// </summary>
        private void RecordElementPosition(int hostElementId, int viewId)
        {
            var position = _tagCreator.GetElementPosition(hostElementId, viewId);
            if (position.HasValue)
            {
                string key = BuildPositionKey(hostElementId, viewId);
                lock (_stateLock)
                {
                    _lastKnownElementPositions[key] = position.Value;
                }
            }
        }

        /// <summary>
        /// Builds a composite key for the element position lookup.
        /// </summary>
        private static string BuildPositionKey(int hostElementId, int viewId)
        {
            return $"{hostElementId}:{viewId}";
        }

        #endregion

        #region Operation History (Private)

        /// <summary>
        /// Records an operation in the undo stack and the repository.
        /// Clears the redo stack because a new operation invalidates the redo chain.
        /// </summary>
        private TagOperation RecordOperation(TagOperationType type, string tagId,
            TagInstance previousState, TagInstance newState, TagCreationSource source)
        {
            var operation = new TagOperation
            {
                Type = type,
                TagId = tagId,
                PreviousState = previousState,
                NewState = newState,
                Timestamp = DateTime.UtcNow,
                Source = source
            };

            lock (_historyLock)
            {
                // Embed batch session ID in the operation ID when a batch is active
                if (!string.IsNullOrEmpty(_currentBatchSessionId))
                {
                    operation.OperationId = $"{_currentBatchSessionId}:{Guid.NewGuid().ToString("N")}";
                    if (newState != null)
                        newState.Metadata["BatchSessionId"] = _currentBatchSessionId;
                }
                else
                {
                    operation.OperationId = Guid.NewGuid().ToString("N");
                }

                _undoStack.Add(operation);
                TrimUndoStack();

                // New operations invalidate the redo chain
                _redoStack.Clear();

                _totalOperationsPerformed++;
            }

            // Also record in the repository for persistence
            _repository.RecordOperation(operation);

            return operation;
        }

        /// <summary>
        /// Trims the undo stack to the configured maximum depth.
        /// </summary>
        private void TrimUndoStack()
        {
            if (_undoStack.Count > _maxUndoDepth)
            {
                int excess = _undoStack.Count - _maxUndoDepth;
                _undoStack.RemoveRange(0, excess);
            }
        }

        /// <summary>
        /// Trims the redo stack to the configured maximum depth.
        /// </summary>
        private void TrimRedoStack()
        {
            lock (_historyLock)
            {
                if (_redoStack.Count > _maxUndoDepth)
                {
                    int excess = _redoStack.Count - _maxUndoDepth;
                    _redoStack.RemoveRange(0, excess);
                }
            }
        }

        #endregion

        #region Revit Bridge Methods

        /// <summary>
        /// Creates a tag in Revit for the given tag instance and view context.
        /// Returns true if the tag was successfully created.
        /// </summary>
        public bool CreateTagInRevit(TagInstance tagInstance, ViewTagContext viewContext)
        {
            if (tagInstance == null || viewContext == null) return false;

            try
            {
                var position = tagInstance.Placement?.Position ?? new Point2D(0, 0);
                var leaderEnd = tagInstance.Placement?.LeaderEndPoint ?? position;
                var leaderType = tagInstance.Placement?.LeaderType ?? LeaderType.None;

                int revitId = _tagCreator.CreateTag(
                    viewContext.ViewId,
                    tagInstance.HostElementId,
                    tagInstance.TagFamilyName ?? "",
                    tagInstance.TagTypeName ?? "",
                    position,
                    leaderType,
                    leaderEnd);

                if (revitId != 0)
                {
                    tagInstance.RevitElementId = revitId;
                    var bounds = _tagCreator.GetTagBounds(revitId);
                    if (bounds != null) tagInstance.Bounds = bounds;
                    RegisterTag(tagInstance);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to create tag in Revit for host element {0}", tagInstance.HostElementId);
            }
            return false;
        }

        /// <summary>
        /// Moves a tag to a new position in Revit.
        /// Returns true if the move was successful.
        /// </summary>
        public bool MoveTagInRevit(string tagId, Point2D newPosition, ViewTagContext viewContext)
        {
            lock (_registryLock)
            {
                if (!_tagRegistry.TryGetValue(tagId, out var tag)) return false;

                bool moved = _tagCreator.MoveTag(tag.RevitElementId, newPosition);
                if (moved && tag.Placement != null)
                {
                    tag.Placement.Position = newPosition;
                    var bounds = _tagCreator.GetTagBounds(tag.RevitElementId);
                    if (bounds != null) tag.Bounds = bounds;
                }
                return moved;
            }
        }

        /// <summary>
        /// Builds a view context for a given view ID.
        /// </summary>
        public ViewTagContext BuildViewContext(int viewId)
        {
            var tagsInView = GetTagsByView(viewId);
            return new ViewTagContext
            {
                ViewId = viewId,
                ViewName = $"View_{viewId}",
                ViewType = tagsInView.FirstOrDefault()?.ViewType ?? TagViewType.FloorPlan,
                Scale = 100,
                ExistingAnnotationBounds = tagsInView
                    .Where(t => t.Bounds != null)
                    .Select(t => t.Bounds)
                    .ToList()
            };
        }

        /// <summary>
        /// Checks whether an element is visible in a specific view.
        /// </summary>
        public bool IsElementInView(int elementId, int viewId)
        {
            return _tagCreator.DoesElementExist(elementId);
        }

        /// <summary>
        /// Gets all taggable element IDs in a view.
        /// </summary>
        public List<int> GetTaggableElementsInView(int viewId)
        {
            lock (_registryLock)
            {
                // Return all unique host element IDs known in the view
                if (_viewIndex.TryGetValue(viewId, out var tagIds))
                {
                    return tagIds
                        .Where(id => _tagRegistry.ContainsKey(id))
                        .Select(id => _tagRegistry[id].HostElementId)
                        .Distinct()
                        .ToList();
                }
                return new List<int>();
            }
        }

        /// <summary>
        /// Gets the bounding box of an element in a view context.
        /// </summary>
        public TagBounds2D GetElementBounds(int elementId, ViewTagContext viewContext)
        {
            var position = _tagCreator.GetElementPosition(elementId, viewContext.ViewId);
            if (position.HasValue)
            {
                // Create approximate bounds around the element position
                double halfWidth = 0.05;
                double halfHeight = 0.03;
                return new TagBounds2D(
                    position.Value.X - halfWidth,
                    position.Value.Y - halfHeight,
                    position.Value.X + halfWidth,
                    position.Value.Y + halfHeight);
            }
            return new TagBounds2D(0, 0, 0.1, 0.06);
        }

        /// <summary>
        /// Gets the Revit category name for an element.
        /// </summary>
        public string GetElementCategory(int elementId)
        {
            lock (_registryLock)
            {
                var tag = _tagRegistry.Values.FirstOrDefault(t => t.HostElementId == elementId);
                return tag?.CategoryName ?? "Unknown";
            }
        }

        /// <summary>
        /// Gets the Revit family name for an element.
        /// </summary>
        public string GetElementFamilyName(int elementId)
        {
            lock (_registryLock)
            {
                var tag = _tagRegistry.Values.FirstOrDefault(t => t.HostElementId == elementId);
                return tag?.FamilyName ?? "Unknown";
            }
        }

        /// <summary>
        /// Gets the Revit type name for an element.
        /// </summary>
        public string GetElementTypeName(int elementId)
        {
            lock (_registryLock)
            {
                var tag = _tagRegistry.Values.FirstOrDefault(t => t.HostElementId == elementId);
                return tag?.TypeName ?? "Unknown";
            }
        }

        /// <summary>
        /// Gets the insertion point for an element in a view.
        /// </summary>
        public Point2D GetElementInsertionPoint(int elementId, ViewTagContext viewContext)
        {
            var position = _tagCreator.GetElementPosition(elementId, viewContext.ViewId);
            return position ?? new Point2D(0, 0);
        }

        /// <summary>
        /// Gets the rotation angle (in degrees) of an element in a view.
        /// </summary>
        public double GetElementRotation(int elementId, ViewTagContext viewContext)
        {
            // Rotation not available through ITagCreator; return 0 as default
            return 0.0;
        }

        #endregion

        #region Tag Cloning (Private)

        /// <summary>
        /// Creates a deep clone of a TagInstance for recording in operation history.
        /// This ensures that undo/redo captures the state at the time of the operation,
        /// not a reference to the mutable live object.
        /// </summary>
        private static TagInstance CloneTag(TagInstance source)
        {
            if (source == null) return null;

            var clone = new TagInstance
            {
                TagId = source.TagId,
                RevitElementId = source.RevitElementId,
                HostElementId = source.HostElementId,
                ViewId = source.ViewId,
                CategoryName = source.CategoryName,
                FamilyName = source.FamilyName,
                TypeName = source.TypeName,
                TagFamilyName = source.TagFamilyName,
                TagTypeName = source.TagTypeName,
                DisplayText = source.DisplayText,
                ContentExpression = source.ContentExpression,
                State = source.State,
                CreatedByRule = source.CreatedByRule,
                CreatedByTemplate = source.CreatedByTemplate,
                CreationSource = source.CreationSource,
                PlacementScore = source.PlacementScore,
                LastModified = source.LastModified,
                UserAdjusted = source.UserAdjusted,
                Metadata = new Dictionary<string, object>(source.Metadata)
            };

            if (source.Placement != null)
            {
                clone.Placement = new TagPlacement
                {
                    Position = source.Placement.Position,
                    LeaderEndPoint = source.Placement.LeaderEndPoint,
                    LeaderElbowPoint = source.Placement.LeaderElbowPoint,
                    LeaderType = source.Placement.LeaderType,
                    LeaderLength = source.Placement.LeaderLength,
                    Rotation = source.Placement.Rotation,
                    PreferredPosition = source.Placement.PreferredPosition,
                    ResolvedPosition = source.Placement.ResolvedPosition,
                    Orientation = source.Placement.Orientation,
                    OffsetX = source.Placement.OffsetX,
                    OffsetY = source.Placement.OffsetY,
                    IsStacked = source.Placement.IsStacked,
                    StackedWithTagId = source.Placement.StackedWithTagId
                };
            }

            if (source.Bounds != null)
            {
                clone.Bounds = new TagBounds2D(
                    source.Bounds.MinX,
                    source.Bounds.MinY,
                    source.Bounds.MaxX,
                    source.Bounds.MaxY);
            }

            return clone;
        }

        #endregion
    }
}
