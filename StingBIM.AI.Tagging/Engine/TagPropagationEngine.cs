// StingBIM.AI.Tagging - SuperIntelligent Tagging System
// TagPropagationEngine.cs - Intelligent cross-view tag propagation engine
// Propagates tags from one view to another while adapting content, position, and formatting.
// Supports bidirectional sync, conflict resolution, change tracking, and propagation analytics.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using StingBIM.AI.Tagging.Data;
using StingBIM.AI.Tagging.Models;

namespace StingBIM.AI.Tagging.Engine
{
    #region Enumerations

    /// <summary>
    /// Defines the scope of view discovery when propagating a tag to other views.
    /// Controls how broadly the engine searches for target views.
    /// </summary>
    public enum PropagationScope
    {
        /// <summary>No propagation. Tag remains only in the source view.</summary>
        None,

        /// <summary>Propagate only to views placed on the same sheet as the source view.</summary>
        SameSheet,

        /// <summary>Propagate to views that share the same building level as the source view.</summary>
        SameLevel,

        /// <summary>Propagate to all views in the project where the tagged element is visible.</summary>
        AllViewsShowingElement,

        /// <summary>Propagate according to a caller-supplied list of target view IDs.</summary>
        Custom
    }

    /// <summary>
    /// Policy controlling when and how tags are automatically propagated.
    /// Assigned per rule or per propagation mapping to govern lifecycle behavior.
    /// </summary>
    public enum PropagationPolicy
    {
        /// <summary>Always propagate: any tag change in any linked view is synchronized immediately.</summary>
        Always,

        /// <summary>Propagate only when the source tag is first created. Subsequent edits are not synced.</summary>
        OnCreate,

        /// <summary>Propagation is only triggered by explicit user action or API call.</summary>
        Manual,

        /// <summary>Never propagate. The rule or mapping is dormant.</summary>
        Never
    }

    /// <summary>
    /// Strategy for resolving conflicts when propagated tag data conflicts with existing data
    /// in the target view.
    /// </summary>
    public enum ConflictResolutionStrategy
    {
        /// <summary>Source view data always wins. Target view data is overwritten.</summary>
        SourceWins,

        /// <summary>Target view data is preserved. Propagation is skipped for conflicting fields.</summary>
        TargetWins,

        /// <summary>Merge non-conflicting fields from both. Conflicting fields use source.</summary>
        Merge,

        /// <summary>Flag the conflict for manual user review without applying changes.</summary>
        AskUser
    }

    /// <summary>
    /// Type of change detected during synchronization analysis.
    /// </summary>
    public enum ChangeType
    {
        /// <summary>Tag content expression was modified.</summary>
        ContentChanged,

        /// <summary>Tag position or placement was moved.</summary>
        PositionChanged,

        /// <summary>Tag style, family, or type was changed.</summary>
        StyleChanged,

        /// <summary>Tag was deleted from the source view.</summary>
        Deleted,

        /// <summary>New tag was created in the source view.</summary>
        Created,

        /// <summary>Tag state changed (e.g., Active to Hidden).</summary>
        StateChanged
    }

    #endregion

    #region Propagation DTOs

    /// <summary>
    /// A rule that governs when and how tags propagate from a source view type
    /// to one or more target view types. Each rule specifies category filters,
    /// content adaptation mappings, and a position translation strategy.
    /// </summary>
    public class PropagationRule
    {
        /// <summary>Unique identifier for this propagation rule.</summary>
        public string RuleId { get; set; }

        /// <summary>Human-readable name for this rule.</summary>
        public string Name { get; set; }

        /// <summary>Description of what this rule does.</summary>
        public string Description { get; set; }

        /// <summary>The source view type this rule applies to.</summary>
        public TagViewType SourceViewType { get; set; }

        /// <summary>Target view types that should receive propagated tags.</summary>
        public List<TagViewType> TargetViewTypes { get; set; } = new List<TagViewType>();

        /// <summary>
        /// Category filter (e.g., "Doors", "Columns"). Null matches all categories.
        /// Supports wildcard patterns with * and ?.
        /// </summary>
        public string CategoryFilter { get; set; }

        /// <summary>
        /// Content expression mappings keyed by target view type.
        /// Each entry maps a target view type to the content expression that should be
        /// used when propagating to that view type.
        /// </summary>
        public Dictionary<TagViewType, string> ContentMapping { get; set; }
            = new Dictionary<TagViewType, string>();

        /// <summary>
        /// Position translation strategy name. Controls how the tag position is
        /// mapped from source to target view coordinates.
        /// Built-in strategies: "MaintainRelative", "ProjectedCenter", "ElementFaceCenter".
        /// </summary>
        public string PositionStrategy { get; set; } = "MaintainRelative";

        /// <summary>The propagation scope for this rule.</summary>
        public PropagationScope Scope { get; set; } = PropagationScope.AllViewsShowingElement;

        /// <summary>Lifecycle policy governing automatic propagation behavior.</summary>
        public PropagationPolicy Policy { get; set; } = PropagationPolicy.OnCreate;

        /// <summary>Conflict resolution strategy when target view has existing different data.</summary>
        public ConflictResolutionStrategy ConflictStrategy { get; set; } = ConflictResolutionStrategy.SourceWins;

        /// <summary>Whether to enable bidirectional sync (changes in target propagate back to source).</summary>
        public bool BidirectionalSync { get; set; }

        /// <summary>Priority for this rule (lower = higher priority).</summary>
        public int Priority { get; set; } = 100;

        /// <summary>Whether this rule is currently enabled.</summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>Whether to propagate to views showing linked file instances of the element.</summary>
        public bool IncludeLinkedFileViews { get; set; }

        /// <summary>Propagation group name for linking related views together.</summary>
        public string GroupName { get; set; }

        /// <summary>Selective parameter names to propagate. Null means propagate all.</summary>
        public List<string> SelectiveParameters { get; set; }
    }

    /// <summary>
    /// Defines a persistent mapping between a source view and one or more target views
    /// for ongoing tag propagation and synchronization.
    /// </summary>
    public class PropagationMapping
    {
        /// <summary>Unique identifier for this mapping.</summary>
        public string MappingId { get; set; }

        /// <summary>Human-readable name for this mapping.</summary>
        public string Name { get; set; }

        /// <summary>The source (master) view ID.</summary>
        public int SourceViewId { get; set; }

        /// <summary>The source view type.</summary>
        public TagViewType SourceViewType { get; set; }

        /// <summary>Target view IDs and their types.</summary>
        public List<MappingTarget> Targets { get; set; } = new List<MappingTarget>();

        /// <summary>Lifecycle policy for this mapping.</summary>
        public PropagationPolicy Policy { get; set; } = PropagationPolicy.OnCreate;

        /// <summary>Conflict resolution strategy for this mapping.</summary>
        public ConflictResolutionStrategy ConflictStrategy { get; set; } = ConflictResolutionStrategy.SourceWins;

        /// <summary>Whether this mapping enables bidirectional sync.</summary>
        public bool BidirectionalSync { get; set; }

        /// <summary>Category filter. Null matches all.</summary>
        public string CategoryFilter { get; set; }

        /// <summary>Whether local overrides in target views are preserved during sync.</summary>
        public bool PreserveLocalOverrides { get; set; } = true;

        /// <summary>When this mapping was created.</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>When this mapping was last synchronized.</summary>
        public DateTime? LastSyncedAt { get; set; }

        /// <summary>Whether this mapping is currently active.</summary>
        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// A single target within a propagation mapping.
    /// </summary>
    public class MappingTarget
    {
        /// <summary>The target view ID.</summary>
        public int ViewId { get; set; }

        /// <summary>The target view type.</summary>
        public TagViewType ViewType { get; set; }

        /// <summary>Content expression override for this target. Null uses default adaptation.</summary>
        public string ContentExpressionOverride { get; set; }

        /// <summary>Whether this specific target is enabled.</summary>
        public bool IsEnabled { get; set; } = true;
    }

    /// <summary>
    /// Represents a conflict detected during tag propagation or synchronization.
    /// </summary>
    public class PropagationConflict
    {
        /// <summary>Unique identifier for this conflict.</summary>
        public string ConflictId { get; set; }

        /// <summary>The source tag that triggered the conflict.</summary>
        public string SourceTagId { get; set; }

        /// <summary>The target tag that conflicts with the source.</summary>
        public string TargetTagId { get; set; }

        /// <summary>The host element ID.</summary>
        public int HostElementId { get; set; }

        /// <summary>Source view ID.</summary>
        public int SourceViewId { get; set; }

        /// <summary>Target view ID where the conflict occurs.</summary>
        public int TargetViewId { get; set; }

        /// <summary>Type of conflict: content, position, or style.</summary>
        public ConflictType Type { get; set; }

        /// <summary>Description of what differs between source and target.</summary>
        public string Description { get; set; }

        /// <summary>The source value (content, position string, etc.).</summary>
        public string SourceValue { get; set; }

        /// <summary>The target value that conflicts.</summary>
        public string TargetValue { get; set; }

        /// <summary>The resolution strategy applied or pending.</summary>
        public ConflictResolutionStrategy Resolution { get; set; }

        /// <summary>Whether this conflict has been resolved.</summary>
        public bool IsResolved { get; set; }

        /// <summary>When this conflict was detected.</summary>
        public DateTime DetectedAt { get; set; } = DateTime.UtcNow;

        /// <summary>When this conflict was resolved (if applicable).</summary>
        public DateTime? ResolvedAt { get; set; }
    }

    /// <summary>
    /// Type of propagation conflict.
    /// </summary>
    public enum ConflictType
    {
        /// <summary>Tag content differs between source and target.</summary>
        Content,

        /// <summary>Tag position conflicts with existing annotations in target view.</summary>
        Position,

        /// <summary>Tag style or formatting differs.</summary>
        Style,

        /// <summary>Same element tagged with different tag families in different views.</summary>
        TagFamily,

        /// <summary>Target tag was manually adjusted by user (local override).</summary>
        LocalOverride
    }

    /// <summary>
    /// Options controlling propagation behavior for a single operation or batch.
    /// </summary>
    public class PropagationOptions
    {
        /// <summary>The propagation scope. Overrides individual rule scopes when not null.</summary>
        public PropagationScope? ScopeOverride { get; set; }

        /// <summary>Explicit list of target view IDs for Custom scope.</summary>
        public List<int> TargetViewIds { get; set; }

        /// <summary>Whether to skip target views that already have a tag for the same element.</summary>
        public bool SkipDuplicates { get; set; } = true;

        /// <summary>Whether to record all propagated tags for batch undo support.</summary>
        public bool EnableUndo { get; set; } = true;

        /// <summary>Whether to adapt tag content for each target view type.</summary>
        public bool AdaptContent { get; set; } = true;

        /// <summary>Whether to translate tag position for each target view type.</summary>
        public bool TranslatePosition { get; set; } = true;

        /// <summary>Whether to include linked file views in propagation.</summary>
        public bool IncludeLinkedFileViews { get; set; }

        /// <summary>Minimum collision clearance in model units for propagated tags.</summary>
        public double CollisionClearance { get; set; } = 0.002;

        /// <summary>Conflict resolution strategy override. Null uses per-rule strategies.</summary>
        public ConflictResolutionStrategy? ConflictStrategyOverride { get; set; }

        /// <summary>Whether to preserve local user overrides in target views.</summary>
        public bool PreserveLocalOverrides { get; set; } = true;

        /// <summary>Whether this is an incremental propagation (only new/changed tags).</summary>
        public bool IncrementalOnly { get; set; }

        /// <summary>Discipline filter for discipline-based propagation (e.g., "MEP", "Structural").</summary>
        public string DisciplineFilter { get; set; }
    }

    /// <summary>
    /// Result of propagating a single source tag to other views.
    /// </summary>
    public class PropagationResult
    {
        /// <summary>The source tag that was propagated.</summary>
        public TagInstance SourceTag { get; set; }

        /// <summary>Tags created in target views, keyed by view ID.</summary>
        public Dictionary<int, List<TagInstance>> PropagatedTags { get; set; }
            = new Dictionary<int, List<TagInstance>>();

        /// <summary>Views that were skipped during propagation, with the reason.</summary>
        public Dictionary<int, string> SkippedViews { get; set; }
            = new Dictionary<int, string>();

        /// <summary>Conflicts detected during propagation.</summary>
        public List<PropagationConflict> Conflicts { get; set; } = new List<PropagationConflict>();

        /// <summary>Total tags successfully created across all target views.</summary>
        public int TotalPropagated => PropagatedTags.Values.Sum(list => list.Count);

        /// <summary>Total views skipped.</summary>
        public int TotalSkipped => SkippedViews.Count;

        /// <summary>Whether the propagation completed without errors.</summary>
        public bool Success { get; set; }

        /// <summary>Error message if propagation failed entirely.</summary>
        public string ErrorMessage { get; set; }

        /// <summary>Duration of the propagation operation.</summary>
        public TimeSpan Duration { get; set; }

        /// <summary>All propagated tag IDs for undo support.</summary>
        public List<string> UndoTagIds { get; set; } = new List<string>();

        /// <summary>The operation ID for this propagation (used for undo and changelog).</summary>
        public string OperationId { get; set; }
    }

    /// <summary>
    /// Result of propagating multiple source tags in a batch.
    /// </summary>
    public class BatchPropagationResult
    {
        /// <summary>Per-source-tag propagation results.</summary>
        public List<PropagationResult> Results { get; set; } = new List<PropagationResult>();

        /// <summary>Total source tags processed.</summary>
        public int TotalSourceTags { get; set; }

        /// <summary>Total tags created across all source tags and target views.</summary>
        public int TotalPropagated => Results.Sum(r => r.TotalPropagated);

        /// <summary>Number of source tags with at least one successful propagation.</summary>
        public int SuccessCount => Results.Count(r => r.Success && r.TotalPropagated > 0);

        /// <summary>Number of source tags that failed propagation entirely.</summary>
        public int FailureCount => Results.Count(r => !r.Success);

        /// <summary>All conflicts detected across all results.</summary>
        public List<PropagationConflict> AllConflicts => Results.SelectMany(r => r.Conflicts).ToList();

        /// <summary>Duration of the entire batch operation.</summary>
        public TimeSpan Duration { get; set; }

        /// <summary>All propagated tag IDs from all results, for batch undo.</summary>
        public List<string> AllUndoTagIds => Results.SelectMany(r => r.UndoTagIds).ToList();
    }

    /// <summary>
    /// Describes a target view and the propagation rule that applies to it.
    /// </summary>
    public class PropagationTarget
    {
        /// <summary>The target view ID.</summary>
        public int ViewId { get; set; }

        /// <summary>The view type of the target.</summary>
        public TagViewType ViewType { get; set; }

        /// <summary>The propagation rule that matched this target.</summary>
        public PropagationRule ApplicableRule { get; set; }

        /// <summary>The adapted content expression for this target view type.</summary>
        public string AdaptedContentExpression { get; set; }

        /// <summary>Whether the element already has a tag in this target view.</summary>
        public bool AlreadyTagged { get; set; }
    }

    /// <summary>
    /// A detected change in a source tag requiring synchronization to targets.
    /// </summary>
    public class TagChange
    {
        /// <summary>The tag that changed.</summary>
        public string TagId { get; set; }

        /// <summary>The host element ID.</summary>
        public int HostElementId { get; set; }

        /// <summary>The view containing the changed tag.</summary>
        public int ViewId { get; set; }

        /// <summary>Type of change detected.</summary>
        public ChangeType Type { get; set; }

        /// <summary>Previous value (serialized).</summary>
        public string PreviousValue { get; set; }

        /// <summary>New value (serialized).</summary>
        public string NewValue { get; set; }

        /// <summary>When the change was detected.</summary>
        public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Entry in the propagation changelog for audit tracking.
    /// </summary>
    public class ChangelogEntry
    {
        /// <summary>Unique entry identifier.</summary>
        public string EntryId { get; set; }

        /// <summary>The operation ID that generated this entry.</summary>
        public string OperationId { get; set; }

        /// <summary>Type of change.</summary>
        public ChangeType ChangeType { get; set; }

        /// <summary>Source tag ID.</summary>
        public string SourceTagId { get; set; }

        /// <summary>Target tag IDs affected.</summary>
        public List<string> TargetTagIds { get; set; } = new List<string>();

        /// <summary>Source view ID.</summary>
        public int SourceViewId { get; set; }

        /// <summary>Target view IDs affected.</summary>
        public List<int> TargetViewIds { get; set; } = new List<int>();

        /// <summary>Description of the change.</summary>
        public string Description { get; set; }

        /// <summary>When the change was applied.</summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>Whether the change was applied successfully.</summary>
        public bool Success { get; set; }
    }

    /// <summary>
    /// Progress information during batch propagation.
    /// </summary>
    public class PropagationProgress
    {
        /// <summary>Index of the current source tag being processed (1-based).</summary>
        public int CurrentSourceTag { get; set; }

        /// <summary>Total source tags in the batch.</summary>
        public int TotalSourceTags { get; set; }

        /// <summary>Number of tags propagated so far.</summary>
        public int TagsPropagatedSoFar { get; set; }

        /// <summary>Current operation description.</summary>
        public string CurrentOperation { get; set; }

        /// <summary>Percent complete (0-100).</summary>
        public double PercentComplete => TotalSourceTags > 0
            ? (double)CurrentSourceTag / TotalSourceTags * 100.0
            : 0.0;
    }

    /// <summary>
    /// Analytics data for propagation coverage and consistency across the project.
    /// </summary>
    public class PropagationAnalytics
    {
        /// <summary>Total views analyzed.</summary>
        public int TotalViews { get; set; }

        /// <summary>Views that have received propagated tags.</summary>
        public int ViewsWithPropagatedTags { get; set; }

        /// <summary>Coverage ratio: views with propagated tags / total views (0.0-1.0).</summary>
        public double CoverageRatio => TotalViews > 0
            ? (double)ViewsWithPropagatedTags / TotalViews
            : 0.0;

        /// <summary>
        /// Consistency score (0.0-1.0): measures how consistently the same elements
        /// are tagged across views. 1.0 means every tagged element appears in all
        /// applicable views with matching content.
        /// </summary>
        public double ConsistencyScore { get; set; }

        /// <summary>
        /// Orphaned propagated tags: tags in target views whose source tag has been deleted.
        /// </summary>
        public List<string> OrphanedTagIds { get; set; } = new List<string>();

        /// <summary>
        /// Stale propagated tags: tags in target views that have not been updated since
        /// their source tag was last modified.
        /// </summary>
        public List<StaleTagInfo> StaleTags { get; set; } = new List<StaleTagInfo>();

        /// <summary>Per-view propagation status.</summary>
        public Dictionary<int, ViewPropagationStatus> ViewStatuses { get; set; }
            = new Dictionary<int, ViewPropagationStatus>();

        /// <summary>Unresolved conflicts requiring user review.</summary>
        public List<PropagationConflict> UnresolvedConflicts { get; set; }
            = new List<PropagationConflict>();

        /// <summary>Total active propagation mappings.</summary>
        public int ActiveMappings { get; set; }

        /// <summary>When this analytics report was generated.</summary>
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Duration of the analysis.</summary>
        public TimeSpan AnalysisDuration { get; set; }
    }

    /// <summary>
    /// Information about a stale propagated tag.
    /// </summary>
    public class StaleTagInfo
    {
        /// <summary>The stale target tag ID.</summary>
        public string TagId { get; set; }

        /// <summary>The source tag ID it was propagated from.</summary>
        public string SourceTagId { get; set; }

        /// <summary>View ID containing the stale tag.</summary>
        public int ViewId { get; set; }

        /// <summary>How long since the source was last modified.</summary>
        public TimeSpan StaleDuration { get; set; }
    }

    /// <summary>
    /// Propagation status for a single view.
    /// </summary>
    public class ViewPropagationStatus
    {
        /// <summary>The view ID.</summary>
        public int ViewId { get; set; }

        /// <summary>The view type.</summary>
        public TagViewType ViewType { get; set; }

        /// <summary>Number of propagated tags in this view.</summary>
        public int PropagatedTagCount { get; set; }

        /// <summary>Number of locally created (non-propagated) tags.</summary>
        public int LocalTagCount { get; set; }

        /// <summary>Number of stale propagated tags.</summary>
        public int StaleCount { get; set; }

        /// <summary>Number of orphaned propagated tags.</summary>
        public int OrphanCount { get; set; }

        /// <summary>Whether this view acts as a source (master) for any mapping.</summary>
        public bool IsSourceView { get; set; }
    }

    #endregion

    #region ViewCoordinateTransformer

    /// <summary>
    /// Transforms tag positions between different view coordinate systems.
    /// Handles plan-to-plan, plan-to-section, plan-to-elevation, section-to-detail,
    /// and any-view-to-sheet transformations with scale-aware offset computation.
    /// </summary>
    public class ViewCoordinateTransformer
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        #region Public Transform Methods

        /// <summary>
        /// Transforms a tag placement from source view space to target view space.
        /// Selects the appropriate transformation strategy based on view type pair.
        /// </summary>
        /// <param name="source">Source tag placement.</param>
        /// <param name="sourceType">Source view type.</param>
        /// <param name="targetType">Target view type.</param>
        /// <param name="sourceScale">Source view scale factor (e.g., 100 for 1:100).</param>
        /// <param name="targetScale">Target view scale factor.</param>
        /// <param name="elementCenter">Optional element center point in the target view for anchor-based transforms.</param>
        /// <returns>Translated placement for the target view.</returns>
        public TagPlacement Transform(
            TagPlacement source,
            TagViewType sourceType,
            TagViewType targetType,
            double sourceScale,
            double targetScale,
            Point2D? elementCenter = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            double scaleRatio = sourceScale > 0 ? targetScale / sourceScale : 1.0;

            if (IsPlanType(sourceType) && IsPlanType(targetType))
                return TransformPlanToPlan(source, scaleRatio);

            if (IsPlanType(sourceType) && targetType == TagViewType.Section)
                return TransformPlanToSection(source, scaleRatio, elementCenter);

            if (IsPlanType(sourceType) && targetType == TagViewType.Elevation)
                return TransformPlanToElevation(source, scaleRatio, elementCenter);

            if (IsPlanType(sourceType) && targetType == TagViewType.ThreeDimensional)
                return TransformPlanTo3D(source, scaleRatio);

            if (targetType == TagViewType.Detail)
                return TransformToDetail(source, sourceType, scaleRatio);

            if (sourceType == TagViewType.Section && targetType == TagViewType.Elevation)
                return TransformSectionToElevation(source, scaleRatio);

            if (sourceType == TagViewType.Elevation && targetType == TagViewType.Section)
                return TransformElevationToSection(source, scaleRatio);

            if (sourceType == TagViewType.Elevation && targetType == TagViewType.Elevation)
                return TransformElevationToElevation(source, scaleRatio);

            if (sourceType == TagViewType.Section && targetType == TagViewType.Section)
                return TransformSectionToSection(source, scaleRatio);

            return TransformDefault(source, scaleRatio);
        }

        /// <summary>
        /// Transforms a placement for sheet-space positioning, accounting for viewport
        /// scale and position on the sheet.
        /// </summary>
        /// <param name="source">Source placement in view coordinates.</param>
        /// <param name="viewportCenter">Center of the viewport on the sheet.</param>
        /// <param name="viewportScale">Scale factor of the viewport.</param>
        /// <returns>Placement in sheet space.</returns>
        public TagPlacement TransformToSheet(
            TagPlacement source,
            Point2D viewportCenter,
            double viewportScale)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            double invScale = viewportScale > 0 ? 1.0 / viewportScale : 1.0;

            var sheetPosition = new Point2D(
                viewportCenter.X + source.Position.X * invScale,
                viewportCenter.Y + source.Position.Y * invScale);

            var sheetLeaderEnd = new Point2D(
                viewportCenter.X + source.LeaderEndPoint.X * invScale,
                viewportCenter.Y + source.LeaderEndPoint.Y * invScale);

            return new TagPlacement
            {
                Position = sheetPosition,
                LeaderEndPoint = sheetLeaderEnd,
                LeaderElbowPoint = null,
                LeaderType = source.LeaderType,
                LeaderLength = source.LeaderLength * invScale,
                Rotation = source.Rotation,
                PreferredPosition = source.PreferredPosition,
                ResolvedPosition = source.ResolvedPosition,
                Orientation = TagOrientation.Horizontal,
                OffsetX = source.OffsetX * invScale,
                OffsetY = source.OffsetY * invScale,
                IsStacked = false,
                StackedWithTagId = null
            };
        }

        /// <summary>
        /// Adjusts a placement to snap to structural grid lines when placing tags
        /// in structural views.
        /// </summary>
        /// <param name="placement">The placement to adjust.</param>
        /// <param name="gridLines">Available grid line positions (X or Y coordinates).</param>
        /// <param name="snapTolerance">Maximum distance to snap to a grid line.</param>
        /// <returns>Grid-snapped placement.</returns>
        public TagPlacement SnapToGrid(
            TagPlacement placement,
            List<double> gridLines,
            double snapTolerance = 0.005)
        {
            if (placement == null || gridLines == null || gridLines.Count == 0)
                return placement;

            var result = ClonePlacement(placement);

            double nearestX = FindNearestGridLine(result.Position.X, gridLines, snapTolerance);
            double nearestY = FindNearestGridLine(result.Position.Y, gridLines, snapTolerance);

            if (!double.IsNaN(nearestX))
            {
                double deltaX = nearestX - result.Position.X;
                result.Position = new Point2D(nearestX, result.Position.Y);
                result.OffsetX += deltaX;
            }

            if (!double.IsNaN(nearestY))
            {
                double deltaY = nearestY - result.Position.Y;
                result.Position = new Point2D(result.Position.X, nearestY);
                result.OffsetY += deltaY;
            }

            return result;
        }

        #endregion

        #region Private Transform Strategies

        private TagPlacement TransformPlanToPlan(TagPlacement source, double scaleRatio)
        {
            return new TagPlacement
            {
                Position = new Point2D(source.Position.X, source.Position.Y),
                LeaderEndPoint = source.LeaderEndPoint,
                LeaderElbowPoint = source.LeaderElbowPoint,
                LeaderType = source.LeaderType,
                LeaderLength = source.LeaderLength * scaleRatio,
                Rotation = source.Rotation,
                PreferredPosition = source.PreferredPosition,
                ResolvedPosition = source.ResolvedPosition,
                Orientation = source.Orientation,
                OffsetX = source.OffsetX * scaleRatio,
                OffsetY = source.OffsetY * scaleRatio,
                IsStacked = false,
                StackedWithTagId = null
            };
        }

        private TagPlacement TransformPlanToSection(TagPlacement source, double scaleRatio, Point2D? elementCenter)
        {
            double tagOffsetAbove = 0.015 * scaleRatio;
            var anchor = elementCenter ?? source.LeaderEndPoint;

            return new TagPlacement
            {
                Position = new Point2D(anchor.X, anchor.Y + tagOffsetAbove),
                LeaderEndPoint = anchor,
                LeaderElbowPoint = null,
                LeaderType = LeaderType.Straight,
                LeaderLength = tagOffsetAbove,
                Rotation = 0.0,
                PreferredPosition = TagPosition.Top,
                ResolvedPosition = TagPosition.Top,
                Orientation = TagOrientation.Horizontal,
                OffsetX = 0.0,
                OffsetY = tagOffsetAbove,
                IsStacked = false,
                StackedWithTagId = null
            };
        }

        private TagPlacement TransformPlanToElevation(TagPlacement source, double scaleRatio, Point2D? elementCenter)
        {
            double tagOffsetRight = 0.02 * scaleRatio;
            var anchor = elementCenter ?? source.LeaderEndPoint;

            return new TagPlacement
            {
                Position = new Point2D(anchor.X + tagOffsetRight, anchor.Y),
                LeaderEndPoint = anchor,
                LeaderElbowPoint = null,
                LeaderType = LeaderType.Straight,
                LeaderLength = tagOffsetRight,
                Rotation = 0.0,
                PreferredPosition = TagPosition.Right,
                ResolvedPosition = TagPosition.Right,
                Orientation = TagOrientation.Horizontal,
                OffsetX = tagOffsetRight,
                OffsetY = 0.0,
                IsStacked = false,
                StackedWithTagId = null
            };
        }

        private TagPlacement TransformPlanTo3D(TagPlacement source, double scaleRatio)
        {
            double tagOffset = 0.025 * scaleRatio;

            return new TagPlacement
            {
                Position = new Point2D(
                    source.LeaderEndPoint.X + tagOffset,
                    source.LeaderEndPoint.Y + tagOffset),
                LeaderEndPoint = source.LeaderEndPoint,
                LeaderElbowPoint = null,
                LeaderType = LeaderType.Straight,
                LeaderLength = tagOffset * 1.414,
                Rotation = 0.0,
                PreferredPosition = TagPosition.TopRight,
                ResolvedPosition = TagPosition.TopRight,
                Orientation = TagOrientation.Horizontal,
                OffsetX = tagOffset,
                OffsetY = tagOffset,
                IsStacked = false,
                StackedWithTagId = null
            };
        }

        private TagPlacement TransformToDetail(TagPlacement source, TagViewType sourceType, double scaleRatio)
        {
            double detailOffset = 0.008 * scaleRatio;

            return new TagPlacement
            {
                Position = new Point2D(
                    source.LeaderEndPoint.X + detailOffset,
                    source.LeaderEndPoint.Y + detailOffset),
                LeaderEndPoint = source.LeaderEndPoint,
                LeaderElbowPoint = null,
                LeaderType = LeaderType.Elbow,
                LeaderLength = detailOffset * 1.414,
                Rotation = 0.0,
                PreferredPosition = TagPosition.TopRight,
                ResolvedPosition = TagPosition.TopRight,
                Orientation = TagOrientation.Horizontal,
                OffsetX = detailOffset,
                OffsetY = detailOffset,
                IsStacked = false,
                StackedWithTagId = null
            };
        }

        private TagPlacement TransformSectionToElevation(TagPlacement source, double scaleRatio)
        {
            double tagOffsetRight = 0.02 * scaleRatio;

            return new TagPlacement
            {
                Position = new Point2D(source.LeaderEndPoint.X + tagOffsetRight, source.LeaderEndPoint.Y),
                LeaderEndPoint = source.LeaderEndPoint,
                LeaderElbowPoint = null,
                LeaderType = LeaderType.Straight,
                LeaderLength = tagOffsetRight,
                Rotation = 0.0,
                PreferredPosition = TagPosition.Right,
                ResolvedPosition = TagPosition.Right,
                Orientation = TagOrientation.Horizontal,
                OffsetX = tagOffsetRight,
                OffsetY = 0.0,
                IsStacked = false,
                StackedWithTagId = null
            };
        }

        private TagPlacement TransformElevationToSection(TagPlacement source, double scaleRatio)
        {
            double tagOffsetAbove = 0.015 * scaleRatio;

            return new TagPlacement
            {
                Position = new Point2D(source.LeaderEndPoint.X, source.LeaderEndPoint.Y + tagOffsetAbove),
                LeaderEndPoint = source.LeaderEndPoint,
                LeaderElbowPoint = null,
                LeaderType = LeaderType.Straight,
                LeaderLength = tagOffsetAbove,
                Rotation = 0.0,
                PreferredPosition = TagPosition.Top,
                ResolvedPosition = TagPosition.Top,
                Orientation = TagOrientation.Horizontal,
                OffsetX = 0.0,
                OffsetY = tagOffsetAbove,
                IsStacked = false,
                StackedWithTagId = null
            };
        }

        private TagPlacement TransformElevationToElevation(TagPlacement source, double scaleRatio)
        {
            return new TagPlacement
            {
                Position = new Point2D(
                    source.LeaderEndPoint.X + source.OffsetX * scaleRatio,
                    source.LeaderEndPoint.Y + source.OffsetY * scaleRatio),
                LeaderEndPoint = source.LeaderEndPoint,
                LeaderElbowPoint = source.LeaderElbowPoint,
                LeaderType = source.LeaderType,
                LeaderLength = source.LeaderLength * scaleRatio,
                Rotation = source.Rotation,
                PreferredPosition = source.PreferredPosition,
                ResolvedPosition = source.ResolvedPosition,
                Orientation = source.Orientation,
                OffsetX = source.OffsetX * scaleRatio,
                OffsetY = source.OffsetY * scaleRatio,
                IsStacked = false,
                StackedWithTagId = null
            };
        }

        private TagPlacement TransformSectionToSection(TagPlacement source, double scaleRatio)
        {
            double tagOffsetAbove = 0.015 * scaleRatio;

            return new TagPlacement
            {
                Position = new Point2D(source.LeaderEndPoint.X, source.LeaderEndPoint.Y + tagOffsetAbove),
                LeaderEndPoint = source.LeaderEndPoint,
                LeaderElbowPoint = null,
                LeaderType = LeaderType.Straight,
                LeaderLength = tagOffsetAbove,
                Rotation = 0.0,
                PreferredPosition = TagPosition.Top,
                ResolvedPosition = TagPosition.Top,
                Orientation = TagOrientation.Horizontal,
                OffsetX = 0.0,
                OffsetY = tagOffsetAbove,
                IsStacked = false,
                StackedWithTagId = null
            };
        }

        private TagPlacement TransformDefault(TagPlacement source, double scaleRatio)
        {
            return new TagPlacement
            {
                Position = new Point2D(
                    source.Position.X + (source.OffsetX * scaleRatio - source.OffsetX),
                    source.Position.Y + (source.OffsetY * scaleRatio - source.OffsetY)),
                LeaderEndPoint = source.LeaderEndPoint,
                LeaderElbowPoint = source.LeaderElbowPoint,
                LeaderType = source.LeaderType,
                LeaderLength = source.LeaderLength * scaleRatio,
                Rotation = source.Rotation,
                PreferredPosition = source.PreferredPosition,
                ResolvedPosition = source.ResolvedPosition,
                Orientation = TagOrientation.Horizontal,
                OffsetX = source.OffsetX * scaleRatio,
                OffsetY = source.OffsetY * scaleRatio,
                IsStacked = false,
                StackedWithTagId = null
            };
        }

        #endregion

        #region Helpers

        private static bool IsPlanType(TagViewType viewType)
        {
            return viewType == TagViewType.FloorPlan ||
                   viewType == TagViewType.CeilingPlan ||
                   viewType == TagViewType.StructuralPlan ||
                   viewType == TagViewType.AreaPlan;
        }

        private static double FindNearestGridLine(double coordinate, List<double> gridLines, double tolerance)
        {
            double nearest = double.NaN;
            double minDist = double.MaxValue;

            foreach (double gridLine in gridLines)
            {
                double dist = Math.Abs(coordinate - gridLine);
                if (dist < tolerance && dist < minDist)
                {
                    minDist = dist;
                    nearest = gridLine;
                }
            }

            return nearest;
        }

        private static TagPlacement ClonePlacement(TagPlacement source)
        {
            if (source == null)
            {
                return new TagPlacement
                {
                    Position = new Point2D(0, 0),
                    LeaderEndPoint = new Point2D(0, 0),
                    LeaderType = LeaderType.None,
                    Orientation = TagOrientation.Horizontal,
                    PreferredPosition = TagPosition.Center,
                    ResolvedPosition = TagPosition.Center
                };
            }

            return new TagPlacement
            {
                Position = new Point2D(source.Position.X, source.Position.Y),
                LeaderEndPoint = new Point2D(source.LeaderEndPoint.X, source.LeaderEndPoint.Y),
                LeaderElbowPoint = source.LeaderElbowPoint.HasValue
                    ? new Point2D(source.LeaderElbowPoint.Value.X, source.LeaderElbowPoint.Value.Y)
                    : (Point2D?)null,
                LeaderType = source.LeaderType,
                LeaderLength = source.LeaderLength,
                Rotation = source.Rotation,
                PreferredPosition = source.PreferredPosition,
                ResolvedPosition = source.ResolvedPosition,
                Orientation = source.Orientation,
                OffsetX = source.OffsetX,
                OffsetY = source.OffsetY,
                IsStacked = source.IsStacked,
                StackedWithTagId = source.StackedWithTagId
            };
        }

        #endregion
    }

    #endregion

    #region TagPropagationEngine

    /// <summary>
    /// Intelligent cross-view tag propagation engine. When a tag is placed in one view,
    /// this engine determines which other views should also receive tags for the same
    /// element, adapting content expressions and tag positions for each target view type.
    ///
    /// <para>
    /// Combines a rule-based propagation system with intelligent content adaptation,
    /// position translation, bidirectional synchronization, conflict resolution,
    /// change tracking, and propagation analytics. Default rules handle common
    /// scenarios while custom rules and mappings extend behavior for project-specific
    /// workflows.
    /// </para>
    ///
    /// Thread-safe. All mutable shared state is guarded by locks.
    /// </summary>
    public class TagPropagationEngine
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        #region Fields

        private readonly TagRepository _repository;
        private readonly TagConfiguration _configuration;
        private readonly object _lockObject = new object();

        /// <summary>All registered propagation rules keyed by RuleId.</summary>
        private readonly Dictionary<string, PropagationRule> _rules;
        private readonly object _rulesLock = new object();

        /// <summary>Persistent propagation mappings keyed by MappingId.</summary>
        private readonly Dictionary<string, PropagationMapping> _mappings;
        private readonly object _mappingsLock = new object();

        /// <summary>Content adapter for view-type-specific content expression mapping.</summary>
        private readonly ContentAdapter _contentAdapter;

        /// <summary>Coordinate transformer for cross-view position translation.</summary>
        private readonly ViewCoordinateTransformer _coordinateTransformer;

        /// <summary>Undo history: operation ID -> list of tag IDs created.</summary>
        private readonly Dictionary<string, List<string>> _undoHistory;
        private readonly object _undoLock = new object();

        /// <summary>Changelog for audit tracking.</summary>
        private readonly List<ChangelogEntry> _changelog;
        private readonly object _changelogLock = new object();

        /// <summary>Conflict log for review.</summary>
        private readonly List<PropagationConflict> _conflictLog;
        private readonly object _conflictLock = new object();

        /// <summary>Tag snapshot cache for change detection: tagId -> last known content hash.</summary>
        private readonly Dictionary<string, string> _tagSnapshots;
        private readonly object _snapshotLock = new object();

        /// <summary>Element-to-views index for view discovery.</summary>
        private readonly Dictionary<int, HashSet<int>> _elementViewIndex;
        private readonly object _elementViewLock = new object();

        /// <summary>View context cache: view ID -> ViewTagContext.</summary>
        private readonly Dictionary<int, ViewTagContext> _viewContextCache;
        private readonly object _contextCacheLock = new object();

        private const int MaxUndoHistorySize = 100;
        private const int MaxChangelogSize = 5000;
        private const int MaxConflictLogSize = 1000;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="TagPropagationEngine"/> class.
        /// Registers default propagation rules for common BIM categories.
        /// </summary>
        /// <param name="repository">Tag repository for querying and persisting tags.</param>
        /// <param name="configuration">Optional configuration override. Uses singleton when null.</param>
        public TagPropagationEngine(
            TagRepository repository,
            TagConfiguration configuration = null)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _configuration = configuration ?? TagConfiguration.Instance;

            _rules = new Dictionary<string, PropagationRule>(StringComparer.OrdinalIgnoreCase);
            _mappings = new Dictionary<string, PropagationMapping>(StringComparer.OrdinalIgnoreCase);
            _contentAdapter = new ContentAdapter();
            _coordinateTransformer = new ViewCoordinateTransformer();
            _undoHistory = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            _changelog = new List<ChangelogEntry>();
            _conflictLog = new List<PropagationConflict>();
            _tagSnapshots = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _elementViewIndex = new Dictionary<int, HashSet<int>>();
            _viewContextCache = new Dictionary<int, ViewTagContext>();

            RegisterDefaultRules();
            RegisterDefaultContentMappings();

            Logger.Info("TagPropagationEngine initialized with {0} default rules", _rules.Count);
        }

        #endregion

        #region Propagation Rule Management

        /// <summary>
        /// Registers the default propagation rules for common BIM tagging scenarios.
        /// </summary>
        private void RegisterDefaultRules()
        {
            AddRule(new PropagationRule
            {
                RuleId = "default_door_plan_propagate",
                Name = "Door Plan to Other Views",
                Description = "Propagate door tags from floor plan to ceiling plans, elevations, and sections",
                SourceViewType = TagViewType.FloorPlan,
                TargetViewTypes = new List<TagViewType>
                    { TagViewType.CeilingPlan, TagViewType.Elevation, TagViewType.Section },
                CategoryFilter = "Doors",
                ContentMapping = new Dictionary<TagViewType, string>
                {
                    { TagViewType.CeilingPlan, "{Door Number}" },
                    { TagViewType.Elevation, "{Door Number}\n{Width} x {Height}" },
                    { TagViewType.Section, "{Door Number}\n{Head Height}\n{Frame Type}" }
                },
                PositionStrategy = "MaintainRelative",
                Policy = PropagationPolicy.OnCreate,
                ConflictStrategy = ConflictResolutionStrategy.SourceWins,
                Priority = 50,
                IsEnabled = true
            });

            AddRule(new PropagationRule
            {
                RuleId = "default_column_structural_propagate",
                Name = "Structural Column to Sections",
                Description = "Propagate column tags from structural plan to section views",
                SourceViewType = TagViewType.StructuralPlan,
                TargetViewTypes = new List<TagViewType> { TagViewType.Section },
                CategoryFilter = "Structural Columns",
                ContentMapping = new Dictionary<TagViewType, string>
                {
                    { TagViewType.Section, "{Mark}\n{Structural Material}\n{Cross-Section}" }
                },
                PositionStrategy = "ProjectedCenter",
                Priority = 50,
                IsEnabled = true
            });

            AddRule(new PropagationRule
            {
                RuleId = "default_duct_mep_propagate",
                Name = "Duct Plan to Sections",
                Description = "Propagate duct tags from floor plan to section views",
                SourceViewType = TagViewType.FloorPlan,
                TargetViewTypes = new List<TagViewType> { TagViewType.Section },
                CategoryFilter = "Ducts",
                ContentMapping = new Dictionary<TagViewType, string>
                {
                    { TagViewType.Section, "{System Type}\n{Size}\n{Flow}" }
                },
                PositionStrategy = "ProjectedCenter",
                Priority = 60,
                IsEnabled = true
            });

            AddRule(new PropagationRule
            {
                RuleId = "default_window_plan_propagate",
                Name = "Window Plan to Elevations and Sections",
                Description = "Propagate window tags from floor plan to elevations and sections",
                SourceViewType = TagViewType.FloorPlan,
                TargetViewTypes = new List<TagViewType> { TagViewType.Elevation, TagViewType.Section },
                CategoryFilter = "Windows",
                ContentMapping = new Dictionary<TagViewType, string>
                {
                    { TagViewType.Elevation, "{Type Mark}\n{Width} x {Height}" },
                    { TagViewType.Section, "{Type Mark}\n{Sill Height}\n{Head Height}" }
                },
                PositionStrategy = "ElementFaceCenter",
                Priority = 50,
                IsEnabled = true
            });

            AddRule(new PropagationRule
            {
                RuleId = "default_wall_plan_propagate",
                Name = "Wall Plan to Sections",
                Description = "Propagate wall tags from floor plan to section views",
                SourceViewType = TagViewType.FloorPlan,
                TargetViewTypes = new List<TagViewType> { TagViewType.Section },
                CategoryFilter = "Walls",
                ContentMapping = new Dictionary<TagViewType, string>
                {
                    { TagViewType.Section, "{Type Mark}\n{Width}\n{Assembly Description}" }
                },
                PositionStrategy = "ProjectedCenter",
                Priority = 70,
                IsEnabled = true
            });

            AddRule(new PropagationRule
            {
                RuleId = "default_elevation_cross_propagate",
                Name = "Elevation Cross-Propagation",
                Description = "Propagate tags between elevation and section views",
                SourceViewType = TagViewType.Elevation,
                TargetViewTypes = new List<TagViewType> { TagViewType.Elevation, TagViewType.Section },
                CategoryFilter = null,
                ContentMapping = new Dictionary<TagViewType, string>(),
                PositionStrategy = "ElementFaceCenter",
                Priority = 90,
                IsEnabled = true
            });

            AddRule(new PropagationRule
            {
                RuleId = "default_room_plan_propagate",
                Name = "Room Plan to Ceiling Plan",
                Description = "Propagate room tags from floor plan to ceiling plan",
                SourceViewType = TagViewType.FloorPlan,
                TargetViewTypes = new List<TagViewType> { TagViewType.CeilingPlan },
                CategoryFilter = "Rooms",
                ContentMapping = new Dictionary<TagViewType, string>
                {
                    { TagViewType.CeilingPlan, "{Name}\n{Number}\n{Ceiling Finish}" }
                },
                PositionStrategy = "MaintainRelative",
                Scope = PropagationScope.SameLevel,
                Priority = 40,
                IsEnabled = true
            });

            AddRule(new PropagationRule
            {
                RuleId = "default_pipe_mep_propagate",
                Name = "Pipe Plan to Sections",
                Description = "Propagate pipe tags from floor plan to section views",
                SourceViewType = TagViewType.FloorPlan,
                TargetViewTypes = new List<TagViewType> { TagViewType.Section },
                CategoryFilter = "Pipes",
                ContentMapping = new Dictionary<TagViewType, string>
                {
                    { TagViewType.Section, "{System Type}\n{Diameter}\n{Flow}" }
                },
                PositionStrategy = "ProjectedCenter",
                Priority = 60,
                IsEnabled = true
            });
        }

        /// <summary>
        /// Adds or updates a propagation rule.
        /// </summary>
        public void AddRule(PropagationRule rule)
        {
            if (rule == null) throw new ArgumentNullException(nameof(rule));
            if (string.IsNullOrWhiteSpace(rule.RuleId))
                rule.RuleId = Guid.NewGuid().ToString("N");

            lock (_rulesLock)
            {
                _rules[rule.RuleId] = rule;
            }

            Logger.Debug("Propagation rule '{0}' ({1}) registered: {2} -> [{3}], category={4}",
                rule.Name, rule.RuleId, rule.SourceViewType,
                string.Join(", ", rule.TargetViewTypes),
                rule.CategoryFilter ?? "*");
        }

        /// <summary>
        /// Removes a propagation rule by its identifier.
        /// </summary>
        public bool RemoveRule(string ruleId)
        {
            if (string.IsNullOrWhiteSpace(ruleId)) return false;

            lock (_rulesLock)
            {
                bool removed = _rules.Remove(ruleId);
                if (removed) Logger.Info("Propagation rule '{0}' removed", ruleId);
                return removed;
            }
        }

        /// <summary>
        /// Gets a propagation rule by its identifier.
        /// </summary>
        public PropagationRule GetRule(string ruleId)
        {
            if (string.IsNullOrWhiteSpace(ruleId)) return null;
            lock (_rulesLock)
            {
                return _rules.TryGetValue(ruleId, out var rule) ? rule : null;
            }
        }

        /// <summary>
        /// Returns all registered propagation rules sorted by priority.
        /// </summary>
        public List<PropagationRule> GetAllRules()
        {
            lock (_rulesLock)
            {
                return _rules.Values.OrderBy(r => r.Priority).ThenBy(r => r.Name).ToList();
            }
        }

        /// <summary>
        /// Enables or disables a propagation rule.
        /// </summary>
        public bool SetRuleEnabled(string ruleId, bool enabled)
        {
            lock (_rulesLock)
            {
                if (_rules.TryGetValue(ruleId, out var rule))
                {
                    if (rule.IsEnabled != enabled)
                    {
                        rule.IsEnabled = enabled;
                        Logger.Info("Propagation rule '{0}' {1}", rule.Name, enabled ? "enabled" : "disabled");
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Finds all enabled propagation rules matching the given source view type and category.
        /// </summary>
        private List<PropagationRule> FindMatchingRules(TagViewType sourceViewType, string categoryName)
        {
            lock (_rulesLock)
            {
                return _rules.Values
                    .Where(r => r.IsEnabled &&
                                r.SourceViewType == sourceViewType &&
                                MatchesCategory(categoryName, r.CategoryFilter))
                    .OrderBy(r => r.Priority)
                    .ThenBy(r => r.Name)
                    .ToList();
            }
        }

        /// <summary>
        /// Tests whether a category name matches a rule's category filter.
        /// </summary>
        private static bool MatchesCategory(string categoryName, string filter)
        {
            if (string.IsNullOrEmpty(filter)) return true;
            if (string.IsNullOrEmpty(categoryName)) return false;
            if (filter == "*") return true;

            if (string.Equals(categoryName, filter, StringComparison.OrdinalIgnoreCase))
                return true;

            if (filter.Contains('*') || filter.Contains('?'))
            {
                string regexPattern = "^" +
                    System.Text.RegularExpressions.Regex.Escape(filter)
                        .Replace("\\*", ".*")
                        .Replace("\\?", ".") +
                    "$";
                return System.Text.RegularExpressions.Regex.IsMatch(
                    categoryName, regexPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            return false;
        }

        #endregion

        #region Propagation Mapping Management

        /// <summary>
        /// Creates or updates a persistent propagation mapping between a source view
        /// and target views.
        /// </summary>
        public void AddMapping(PropagationMapping mapping)
        {
            if (mapping == null) throw new ArgumentNullException(nameof(mapping));
            if (string.IsNullOrWhiteSpace(mapping.MappingId))
                mapping.MappingId = Guid.NewGuid().ToString("N");

            lock (_mappingsLock)
            {
                _mappings[mapping.MappingId] = mapping;
            }

            Logger.Info("Propagation mapping '{0}' registered: view {1} -> [{2}]",
                mapping.Name ?? mapping.MappingId,
                mapping.SourceViewId,
                string.Join(", ", mapping.Targets.Select(t => t.ViewId)));
        }

        /// <summary>
        /// Removes a propagation mapping.
        /// </summary>
        public bool RemoveMapping(string mappingId)
        {
            if (string.IsNullOrWhiteSpace(mappingId)) return false;

            lock (_mappingsLock)
            {
                bool removed = _mappings.Remove(mappingId);
                if (removed) Logger.Info("Propagation mapping '{0}' removed", mappingId);
                return removed;
            }
        }

        /// <summary>
        /// Gets all active mappings for a given source view.
        /// </summary>
        public List<PropagationMapping> GetMappingsForSourceView(int sourceViewId)
        {
            lock (_mappingsLock)
            {
                return _mappings.Values
                    .Where(m => m.IsActive && m.SourceViewId == sourceViewId)
                    .ToList();
            }
        }

        /// <summary>
        /// Gets all active mappings where a given view is a target (for bidirectional sync).
        /// </summary>
        public List<PropagationMapping> GetMappingsForTargetView(int targetViewId)
        {
            lock (_mappingsLock)
            {
                return _mappings.Values
                    .Where(m => m.IsActive && m.Targets.Any(t => t.ViewId == targetViewId && t.IsEnabled))
                    .ToList();
            }
        }

        /// <summary>
        /// Gets all registered propagation mappings.
        /// </summary>
        public List<PropagationMapping> GetAllMappings()
        {
            lock (_mappingsLock)
            {
                return _mappings.Values.ToList();
            }
        }

        #endregion

        #region View Context and Discovery

        /// <summary>
        /// Registers a view context for use in propagation. Call this to populate
        /// view metadata that the engine uses for target discovery.
        /// </summary>
        public void RegisterViewContext(ViewTagContext context)
        {
            if (context == null) return;

            lock (_contextCacheLock)
            {
                _viewContextCache[context.ViewId] = context;
            }
        }

        /// <summary>
        /// Gets the cached view context for a view ID.
        /// </summary>
        public ViewTagContext GetViewContext(int viewId)
        {
            lock (_contextCacheLock)
            {
                return _viewContextCache.TryGetValue(viewId, out var ctx) ? ctx : null;
            }
        }

        /// <summary>
        /// Registers an element as visible in a view, updating the element-to-views index.
        /// </summary>
        public void RegisterElementInView(int elementId, int viewId)
        {
            lock (_elementViewLock)
            {
                if (!_elementViewIndex.ContainsKey(elementId))
                    _elementViewIndex[elementId] = new HashSet<int>();
                _elementViewIndex[elementId].Add(viewId);
            }
        }

        /// <summary>
        /// Gets all view IDs where a given element is visible.
        /// </summary>
        public List<int> GetViewsForElement(int elementId)
        {
            lock (_elementViewLock)
            {
                if (_elementViewIndex.TryGetValue(elementId, out var views))
                    return views.ToList();
                return new List<int>();
            }
        }

        /// <summary>
        /// Given a source tag, finds all target views eligible for propagation.
        /// </summary>
        public List<PropagationTarget> GetPropagationTargets(TagInstance sourceTag)
        {
            if (sourceTag == null) throw new ArgumentNullException(nameof(sourceTag));

            var targets = new List<PropagationTarget>();

            ViewTagContext sourceContext = GetViewContext(sourceTag.ViewId);
            if (sourceContext == null)
            {
                Logger.Warn("Cannot resolve view context for source view {0}", sourceTag.ViewId);
                return targets;
            }

            List<PropagationRule> matchingRules = FindMatchingRules(
                sourceContext.ViewType, sourceTag.CategoryName);

            if (matchingRules.Count == 0)
            {
                Logger.Debug("No propagation rules match source view type {0} and category '{1}'",
                    sourceContext.ViewType, sourceTag.CategoryName);
                return targets;
            }

            List<int> allViewsShowingElement = GetViewsForElement(sourceTag.HostElementId);

            List<TagInstance> existingTags = _repository.GetTagsByHostElement(sourceTag.HostElementId);
            var viewsWithExistingTags = new HashSet<int>(
                existingTags.Where(t => t.State == TagState.Active).Select(t => t.ViewId));

            var processedViewIds = new HashSet<int>();

            foreach (var rule in matchingRules)
            {
                List<int> candidateViewIds = ResolveScopeViews(
                    rule.Scope, sourceTag, sourceContext, allViewsShowingElement);

                foreach (int viewId in candidateViewIds)
                {
                    if (viewId == sourceTag.ViewId) continue;
                    if (processedViewIds.Contains(viewId)) continue;

                    ViewTagContext targetContext = GetViewContext(viewId);
                    if (targetContext == null) continue;
                    if (!rule.TargetViewTypes.Contains(targetContext.ViewType)) continue;

                    string adaptedContent = AdaptContent(
                        sourceTag.ContentExpression,
                        sourceContext.ViewType,
                        targetContext.ViewType,
                        sourceTag.CategoryName);

                    targets.Add(new PropagationTarget
                    {
                        ViewId = viewId,
                        ViewType = targetContext.ViewType,
                        ApplicableRule = rule,
                        AdaptedContentExpression = adaptedContent,
                        AlreadyTagged = viewsWithExistingTags.Contains(viewId)
                    });

                    processedViewIds.Add(viewId);
                }
            }

            Logger.Debug("Found {0} propagation targets for tag {1} (element {2})",
                targets.Count, sourceTag.TagId, sourceTag.HostElementId);

            return targets;
        }

        /// <summary>
        /// Classifies all views showing a specific element by their view types.
        /// </summary>
        public Dictionary<TagViewType, List<int>> ClassifyViewsForElement(int elementId)
        {
            var classified = new Dictionary<TagViewType, List<int>>();
            List<int> allViews = GetViewsForElement(elementId);

            foreach (int viewId in allViews)
            {
                ViewTagContext context = GetViewContext(viewId);
                if (context == null) continue;

                if (!classified.ContainsKey(context.ViewType))
                    classified[context.ViewType] = new List<int>();
                classified[context.ViewType].Add(viewId);
            }

            return classified;
        }

        private List<int> ResolveScopeViews(
            PropagationScope scope,
            TagInstance sourceTag,
            ViewTagContext sourceContext,
            List<int> allViewsShowingElement)
        {
            switch (scope)
            {
                case PropagationScope.None:
                    return new List<int>();

                case PropagationScope.SameSheet:
                    return ResolveSameSheetViews(sourceContext, allViewsShowingElement);

                case PropagationScope.SameLevel:
                    return ResolveSameLevelViews(sourceTag, allViewsShowingElement);

                case PropagationScope.AllViewsShowingElement:
                    return new List<int>(allViewsShowingElement);

                case PropagationScope.Custom:
                    return new List<int>(allViewsShowingElement);

                default:
                    Logger.Warn("Unknown propagation scope: {0}", scope);
                    return new List<int>(allViewsShowingElement);
            }
        }

        private List<int> ResolveSameSheetViews(
            ViewTagContext sourceContext,
            List<int> allViewsShowingElement)
        {
            if (sourceContext.SheetId <= 0) return new List<int>();

            var result = new List<int>();
            foreach (int viewId in allViewsShowingElement)
            {
                ViewTagContext targetContext = GetViewContext(viewId);
                if (targetContext != null && targetContext.SheetId == sourceContext.SheetId)
                    result.Add(viewId);
            }
            return result;
        }

        private List<int> ResolveSameLevelViews(
            TagInstance sourceTag,
            List<int> allViewsShowingElement)
        {
            string sourceLevel = null;
            if (sourceTag.Metadata.TryGetValue("Level", out object levelObj))
                sourceLevel = levelObj?.ToString();

            if (string.IsNullOrEmpty(sourceLevel))
                return new List<int>(allViewsShowingElement);

            var result = new List<int>();
            foreach (int viewId in allViewsShowingElement)
            {
                List<TagInstance> viewTags = _repository.GetTagsByView(viewId);
                bool sameLevel = viewTags.Any(t =>
                    t.Metadata.TryGetValue("Level", out object vLevel) &&
                    string.Equals(vLevel?.ToString(), sourceLevel, StringComparison.OrdinalIgnoreCase));

                if (sameLevel)
                {
                    result.Add(viewId);
                }
                else
                {
                    ViewTagContext ctx = GetViewContext(viewId);
                    if (ctx != null && (ctx.ViewType == TagViewType.Section || ctx.ViewType == TagViewType.Elevation))
                        result.Add(viewId);
                }
            }
            return result;
        }

        #endregion

        #region Content Adaptation

        /// <summary>
        /// Registers default content expression mappings for common categories and view types.
        /// </summary>
        private void RegisterDefaultContentMappings()
        {
            _contentAdapter.RegisterMapping("Doors", TagViewType.FloorPlan, "{Door Number}\n{Swing Direction}");
            _contentAdapter.RegisterMapping("Doors", TagViewType.CeilingPlan, "{Door Number}");
            _contentAdapter.RegisterMapping("Doors", TagViewType.Section, "{Door Number}\n{Head Height}\n{Frame Detail}");
            _contentAdapter.RegisterMapping("Doors", TagViewType.Elevation, "{Door Number}\n{Width} x {Height}");
            _contentAdapter.RegisterMapping("Doors", TagViewType.Detail, "{Door Number}\n{Type Mark}");

            _contentAdapter.RegisterMapping("Windows", TagViewType.FloorPlan, "{Type Mark}");
            _contentAdapter.RegisterMapping("Windows", TagViewType.CeilingPlan, "{Type Mark}");
            _contentAdapter.RegisterMapping("Windows", TagViewType.Section, "{Type Mark}\n{Sill Height}\n{Head Height}");
            _contentAdapter.RegisterMapping("Windows", TagViewType.Elevation, "{Type Mark}\n{Width} x {Height}");

            _contentAdapter.RegisterMapping("Walls", TagViewType.FloorPlan, "{Type Mark}\n{Width}");
            _contentAdapter.RegisterMapping("Walls", TagViewType.Section, "{Type Mark}\n{Width}\n{Assembly Description}");
            _contentAdapter.RegisterMapping("Walls", TagViewType.Elevation, "{Type Mark}");

            _contentAdapter.RegisterMapping("Structural Columns", TagViewType.StructuralPlan, "{Mark}\n{Cross-Section}");
            _contentAdapter.RegisterMapping("Structural Columns", TagViewType.Section, "{Mark}\n{Structural Material}\n{Cross-Section}");
            _contentAdapter.RegisterMapping("Structural Columns", TagViewType.Elevation, "{Mark}\n{Height}");

            _contentAdapter.RegisterMapping("Structural Framing", TagViewType.StructuralPlan, "{Mark}\n{Size}");
            _contentAdapter.RegisterMapping("Structural Framing", TagViewType.Section, "{Mark}\n{Size}\n{Structural Material}");

            _contentAdapter.RegisterMapping("Rooms", TagViewType.FloorPlan, "{Name}\n{Number}\n{Area}");
            _contentAdapter.RegisterMapping("Rooms", TagViewType.CeilingPlan, "{Name}\n{Number}\n{Ceiling Finish}");

            _contentAdapter.RegisterMapping("Ducts", TagViewType.FloorPlan, "{System Type}\n{Size}");
            _contentAdapter.RegisterMapping("Ducts", TagViewType.Section, "{System Type}\n{Size}\n{Flow}");

            _contentAdapter.RegisterMapping("Pipes", TagViewType.FloorPlan, "{System Type}\n{Diameter}");
            _contentAdapter.RegisterMapping("Pipes", TagViewType.Section, "{System Type}\n{Diameter}\n{Flow}");

            _contentAdapter.RegisterMapping("Floors", TagViewType.FloorPlan, "{Type Mark}\n{Thickness}");
            _contentAdapter.RegisterMapping("Floors", TagViewType.Section, "{Type Mark}\n{Thickness}\n{Assembly Description}");

            _contentAdapter.RegisterMapping("Ceilings", TagViewType.CeilingPlan, "{Type Mark}\n{Height}");
            _contentAdapter.RegisterMapping("Ceilings", TagViewType.Section, "{Type Mark}\n{Height}\n{Assembly Description}");
        }

        /// <summary>
        /// Adapts a source content expression for a target view type.
        /// </summary>
        public string AdaptContent(
            string sourceExpression,
            TagViewType sourceType,
            TagViewType targetType,
            string category)
        {
            if (sourceType == targetType) return sourceExpression;

            List<PropagationRule> matchingRules = FindMatchingRules(sourceType, category);
            foreach (var rule in matchingRules)
            {
                if (rule.ContentMapping != null &&
                    rule.ContentMapping.TryGetValue(targetType, out string ruleMapping))
                {
                    return ruleMapping;
                }
            }

            string adapterResult = _contentAdapter.GetMapping(category, targetType);
            if (adapterResult != null) return adapterResult;

            return sourceExpression;
        }

        /// <summary>
        /// Gets the ContentAdapter for external registration of custom mappings.
        /// </summary>
        public ContentAdapter ContentAdapterInstance => _contentAdapter;

        /// <summary>
        /// Gets the ViewCoordinateTransformer for external use.
        /// </summary>
        public ViewCoordinateTransformer CoordinateTransformer => _coordinateTransformer;

        #endregion

        #region Propagation Execution

        /// <summary>
        /// Propagates a single source tag to all applicable target views.
        /// Creates tags in each eligible target view with adapted content and translated positions.
        /// </summary>
        public async Task<PropagationResult> PropagateTagAsync(
            TagInstance sourceTag,
            PropagationOptions options = null,
            CancellationToken cancellationToken = default)
        {
            if (sourceTag == null) throw new ArgumentNullException(nameof(sourceTag));

            options ??= new PropagationOptions();
            var stopwatch = Stopwatch.StartNew();
            string operationId = Guid.NewGuid().ToString("N");
            var result = new PropagationResult
            {
                SourceTag = sourceTag,
                Success = true,
                OperationId = operationId
            };

            Logger.Info("Propagating tag {0} (element {1}, category '{2}') from view {3}",
                sourceTag.TagId, sourceTag.HostElementId, sourceTag.CategoryName, sourceTag.ViewId);

            try
            {
                ViewTagContext sourceContext = GetViewContext(sourceTag.ViewId);
                if (sourceContext == null)
                {
                    result.Success = false;
                    result.ErrorMessage = $"Cannot resolve view context for source view {sourceTag.ViewId}";
                    Logger.Error(result.ErrorMessage);
                    return result;
                }

                List<PropagationTarget> targets = GetPropagationTargets(sourceTag);

                if (options.ScopeOverride.HasValue && options.ScopeOverride.Value == PropagationScope.Custom
                    && options.TargetViewIds != null)
                {
                    var targetSet = new HashSet<int>(options.TargetViewIds);
                    targets = targets.Where(t => targetSet.Contains(t.ViewId)).ToList();
                }

                if (options.IncrementalOnly)
                {
                    targets = targets.Where(t => !t.AlreadyTagged).ToList();
                }

                if (targets.Count == 0)
                {
                    Logger.Info("No propagation targets for tag {0}", sourceTag.TagId);
                    stopwatch.Stop();
                    result.Duration = stopwatch.Elapsed;
                    return result;
                }

                await Task.Run(() =>
                {
                    foreach (var target in targets)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        try
                        {
                            PropagateToTarget(sourceTag, sourceContext, target, options, result, operationId);
                        }
                        catch (Exception ex)
                        {
                            Logger.Error(ex, "Failed to propagate to view {0}", target.ViewId);
                            result.SkippedViews[target.ViewId] = $"Error: {ex.Message}";
                        }
                    }
                }, cancellationToken);

                if (options.EnableUndo && result.UndoTagIds.Count > 0)
                {
                    RecordUndoOperation(operationId, result.UndoTagIds);
                }

                TakeSnapshot(sourceTag);
            }
            catch (OperationCanceledException)
            {
                Logger.Info("Tag propagation cancelled for tag {0}", sourceTag.TagId);
                throw;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"Propagation failed: {ex.Message}";
                Logger.Error(ex, "Tag propagation failed for tag {0}", sourceTag.TagId);
            }

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;

            Logger.Info("Tag propagation complete: {0} created, {1} skipped, {2} conflicts, duration={3}ms",
                result.TotalPropagated, result.TotalSkipped, result.Conflicts.Count,
                result.Duration.TotalMilliseconds);

            return result;
        }

        /// <summary>
        /// Propagates a source tag to a single target, handling deduplication,
        /// conflict detection, and tag creation.
        /// </summary>
        private void PropagateToTarget(
            TagInstance sourceTag,
            ViewTagContext sourceContext,
            PropagationTarget target,
            PropagationOptions options,
            PropagationResult result,
            string operationId)
        {
            // Deduplication check
            if (options.SkipDuplicates && target.AlreadyTagged)
            {
                // Check for conflicts with existing tag
                if (target.ApplicableRule?.ConflictStrategy != ConflictResolutionStrategy.SourceWins)
                {
                    result.SkippedViews[target.ViewId] = "Element already tagged; deduplication active";
                    return;
                }

                // SourceWins: check if content differs and resolve
                var existingTags = _repository.GetTagsByHostElement(sourceTag.HostElementId)
                    .Where(t => t.ViewId == target.ViewId && t.State == TagState.Active)
                    .ToList();

                if (existingTags.Count > 0)
                {
                    var conflict = DetectConflict(sourceTag, existingTags[0], target);
                    if (conflict != null)
                    {
                        var resolution = options.ConflictStrategyOverride
                            ?? target.ApplicableRule?.ConflictStrategy
                            ?? ConflictResolutionStrategy.SourceWins;

                        ResolveConflict(conflict, resolution, sourceTag, existingTags[0]);
                        result.Conflicts.Add(conflict);
                    }

                    result.SkippedViews[target.ViewId] = "Element already tagged; conflict resolved";
                    return;
                }
            }

            // Check for local overrides
            if (options.PreserveLocalOverrides && target.AlreadyTagged)
            {
                var existingTags = _repository.GetTagsByHostElement(sourceTag.HostElementId)
                    .Where(t => t.ViewId == target.ViewId && t.State == TagState.Active && t.UserAdjusted)
                    .ToList();

                if (existingTags.Count > 0)
                {
                    result.SkippedViews[target.ViewId] = "Local user override preserved";
                    return;
                }
            }

            ViewTagContext targetContext = GetViewContext(target.ViewId);
            if (targetContext == null)
            {
                result.SkippedViews[target.ViewId] = "Cannot resolve view context";
                return;
            }

            // Translate position
            TagPlacement targetPlacement;
            if (options.TranslatePosition && sourceTag.Placement != null)
            {
                targetPlacement = _coordinateTransformer.Transform(
                    sourceTag.Placement,
                    sourceContext.ViewType,
                    targetContext.ViewType,
                    sourceContext.Scale,
                    targetContext.Scale);
            }
            else
            {
                targetPlacement = ClonePlacement(sourceTag.Placement);
            }

            // Adapt content
            string targetContent = options.AdaptContent
                ? (target.AdaptedContentExpression ?? sourceTag.ContentExpression)
                : sourceTag.ContentExpression;

            // Create the propagated tag
            var propagatedTag = new TagInstance
            {
                TagId = $"prop_{Guid.NewGuid():N}",
                RevitElementId = 0,
                HostElementId = sourceTag.HostElementId,
                ViewId = target.ViewId,
                CategoryName = sourceTag.CategoryName,
                FamilyName = sourceTag.FamilyName,
                TypeName = sourceTag.TypeName,
                TagFamilyName = sourceTag.TagFamilyName,
                TagTypeName = sourceTag.TagTypeName,
                Placement = targetPlacement,
                DisplayText = null,
                ContentExpression = targetContent,
                State = TagState.Active,
                Bounds = null,
                CreatedByRule = target.ApplicableRule?.RuleId,
                CreatedByTemplate = sourceTag.CreatedByTemplate,
                CreationSource = TagCreationSource.BatchProcessing,
                PlacementScore = 0.8,
                LastModified = DateTime.UtcNow,
                UserAdjusted = false,
                Metadata = new Dictionary<string, object>
                {
                    { "PropagatedFrom", sourceTag.TagId },
                    { "PropagationOperationId", operationId },
                    { "SourceViewId", sourceTag.ViewId },
                    { "PropagationRule", target.ApplicableRule?.Name ?? "default" }
                }
            };

            if (sourceTag.Metadata.ContainsKey("Level"))
                propagatedTag.Metadata["Level"] = sourceTag.Metadata["Level"];

            _repository.AddTag(propagatedTag);
            RegisterElementInView(propagatedTag.HostElementId, propagatedTag.ViewId);

            if (!result.PropagatedTags.ContainsKey(target.ViewId))
                result.PropagatedTags[target.ViewId] = new List<TagInstance>();
            result.PropagatedTags[target.ViewId].Add(propagatedTag);
            result.UndoTagIds.Add(propagatedTag.TagId);

            RecordChangelog(new ChangelogEntry
            {
                EntryId = Guid.NewGuid().ToString("N"),
                OperationId = operationId,
                ChangeType = ChangeType.Created,
                SourceTagId = sourceTag.TagId,
                TargetTagIds = new List<string> { propagatedTag.TagId },
                SourceViewId = sourceTag.ViewId,
                TargetViewIds = new List<int> { target.ViewId },
                Description = $"Propagated tag for element {sourceTag.HostElementId} to view {target.ViewId}",
                Success = true
            });
        }

        /// <summary>
        /// Propagates multiple source tags in a batch.
        /// </summary>
        public async Task<BatchPropagationResult> PropagateBatchAsync(
            List<TagInstance> sourceTags,
            PropagationOptions options = null,
            IProgress<PropagationProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            if (sourceTags == null) throw new ArgumentNullException(nameof(sourceTags));

            options ??= new PropagationOptions();
            var stopwatch = Stopwatch.StartNew();
            var batchResult = new BatchPropagationResult { TotalSourceTags = sourceTags.Count };

            Logger.Info("Starting batch propagation for {0} source tags", sourceTags.Count);

            int processed = 0;
            int totalPropagated = 0;

            foreach (var sourceTag in sourceTags)
            {
                cancellationToken.ThrowIfCancellationRequested();
                processed++;

                progress?.Report(new PropagationProgress
                {
                    CurrentSourceTag = processed,
                    TotalSourceTags = sourceTags.Count,
                    TagsPropagatedSoFar = totalPropagated,
                    CurrentOperation = $"Propagating tag for element {sourceTag.HostElementId}"
                });

                try
                {
                    PropagationResult tagResult = await PropagateTagAsync(
                        sourceTag, options, cancellationToken);
                    batchResult.Results.Add(tagResult);
                    totalPropagated += tagResult.TotalPropagated;
                }
                catch (OperationCanceledException)
                {
                    Logger.Info("Batch propagation cancelled at tag {0}/{1}", processed, sourceTags.Count);
                    throw;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to propagate tag {0}", sourceTag.TagId);
                    batchResult.Results.Add(new PropagationResult
                    {
                        SourceTag = sourceTag,
                        Success = false,
                        ErrorMessage = ex.Message
                    });
                }
            }

            stopwatch.Stop();
            batchResult.Duration = stopwatch.Elapsed;

            Logger.Info("Batch propagation complete: {0} tags, {1} propagated, {2} succeeded, {3} failed",
                batchResult.TotalSourceTags, batchResult.TotalPropagated,
                batchResult.SuccessCount, batchResult.FailureCount);

            return batchResult;
        }

        /// <summary>
        /// Propagates tags level-by-level for multi-story buildings.
        /// Groups source tags by their level metadata and propagates each level sequentially.
        /// </summary>
        public async Task<BatchPropagationResult> PropagateLevelByLevelAsync(
            List<TagInstance> sourceTags,
            PropagationOptions options = null,
            IProgress<PropagationProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            if (sourceTags == null) throw new ArgumentNullException(nameof(sourceTags));

            var tagsByLevel = new Dictionary<string, List<TagInstance>>(StringComparer.OrdinalIgnoreCase);

            foreach (var tag in sourceTags)
            {
                string level = "Unknown";
                if (tag.Metadata.TryGetValue("Level", out object levelObj) && levelObj != null)
                    level = levelObj.ToString();

                if (!tagsByLevel.ContainsKey(level))
                    tagsByLevel[level] = new List<TagInstance>();
                tagsByLevel[level].Add(tag);
            }

            Logger.Info("Level-by-level propagation: {0} levels, {1} total tags",
                tagsByLevel.Count, sourceTags.Count);

            var combinedResult = new BatchPropagationResult { TotalSourceTags = sourceTags.Count };
            int processedSoFar = 0;

            foreach (var kvp in tagsByLevel.OrderBy(x => x.Key))
            {
                cancellationToken.ThrowIfCancellationRequested();

                Logger.Info("Propagating level '{0}': {1} tags", kvp.Key, kvp.Value.Count);

                var levelOptions = options ?? new PropagationOptions();
                var levelResult = await PropagateBatchAsync(
                    kvp.Value, levelOptions, null, cancellationToken);

                combinedResult.Results.AddRange(levelResult.Results);
                processedSoFar += kvp.Value.Count;

                progress?.Report(new PropagationProgress
                {
                    CurrentSourceTag = processedSoFar,
                    TotalSourceTags = sourceTags.Count,
                    TagsPropagatedSoFar = combinedResult.TotalPropagated,
                    CurrentOperation = $"Completed level '{kvp.Key}'"
                });
            }

            return combinedResult;
        }

        /// <summary>
        /// Propagates tags filtered by discipline (e.g., all MEP views at once).
        /// </summary>
        public async Task<BatchPropagationResult> PropagateByDisciplineAsync(
            string discipline,
            List<TagInstance> sourceTags,
            PropagationOptions options = null,
            CancellationToken cancellationToken = default)
        {
            if (sourceTags == null) throw new ArgumentNullException(nameof(sourceTags));

            var disciplineCategories = GetCategoriesForDiscipline(discipline);
            var filteredTags = sourceTags
                .Where(t => disciplineCategories.Contains(t.CategoryName, StringComparer.OrdinalIgnoreCase))
                .ToList();

            Logger.Info("Discipline-based propagation for '{0}': {1} of {2} tags match",
                discipline, filteredTags.Count, sourceTags.Count);

            options ??= new PropagationOptions();
            options.DisciplineFilter = discipline;

            return await PropagateBatchAsync(filteredTags, options, null, cancellationToken);
        }

        /// <summary>
        /// Maps discipline names to relevant Revit categories.
        /// </summary>
        private static List<string> GetCategoriesForDiscipline(string discipline)
        {
            if (string.IsNullOrEmpty(discipline)) return new List<string>();

            switch (discipline.ToUpperInvariant())
            {
                case "MEP":
                case "MECHANICAL":
                    return new List<string>
                    {
                        "Ducts", "Duct Fittings", "Duct Accessories", "Duct Insulations",
                        "Pipes", "Pipe Fittings", "Pipe Accessories", "Pipe Insulations",
                        "Mechanical Equipment", "Air Terminals", "Flex Ducts", "Flex Pipes",
                        "Plumbing Fixtures", "Sprinklers"
                    };
                case "ELECTRICAL":
                    return new List<string>
                    {
                        "Electrical Equipment", "Electrical Fixtures", "Lighting Fixtures",
                        "Lighting Devices", "Communication Devices", "Data Devices",
                        "Fire Alarm Devices", "Nurse Call Devices", "Security Devices",
                        "Telephone Devices", "Cable Trays", "Cable Tray Fittings",
                        "Conduits", "Conduit Fittings"
                    };
                case "STRUCTURAL":
                    return new List<string>
                    {
                        "Structural Columns", "Structural Framing", "Structural Foundations",
                        "Structural Connections", "Structural Rebar", "Structural Fabric Areas",
                        "Structural Fabric Reinforcement", "Structural Stiffeners"
                    };
                case "ARCHITECTURAL":
                    return new List<string>
                    {
                        "Doors", "Windows", "Walls", "Floors", "Ceilings", "Roofs",
                        "Rooms", "Stairs", "Railings", "Ramps", "Curtain Panels",
                        "Curtain Wall Mullions", "Curtain Systems", "Columns",
                        "Furniture", "Casework", "Generic Models"
                    };
                default:
                    return new List<string>();
            }
        }

        #endregion

        #region Change Synchronization

        /// <summary>
        /// Detects changes in source tags since the last snapshot and determines
        /// which target views need updating.
        /// </summary>
        public async Task<List<TagChange>> DetectChangesAsync(
            List<TagInstance> sourceTags,
            CancellationToken cancellationToken = default)
        {
            if (sourceTags == null) throw new ArgumentNullException(nameof(sourceTags));

            var changes = new List<TagChange>();

            await Task.Run(() =>
            {
                foreach (var tag in sourceTags)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    string currentHash = ComputeTagHash(tag);
                    string previousHash;

                    lock (_snapshotLock)
                    {
                        _tagSnapshots.TryGetValue(tag.TagId, out previousHash);
                    }

                    if (previousHash == null)
                    {
                        changes.Add(new TagChange
                        {
                            TagId = tag.TagId,
                            HostElementId = tag.HostElementId,
                            ViewId = tag.ViewId,
                            Type = ChangeType.Created,
                            NewValue = currentHash
                        });
                    }
                    else if (previousHash != currentHash)
                    {
                        var changeType = DetermineChangeType(tag, previousHash, currentHash);
                        changes.Add(new TagChange
                        {
                            TagId = tag.TagId,
                            HostElementId = tag.HostElementId,
                            ViewId = tag.ViewId,
                            Type = changeType,
                            PreviousValue = previousHash,
                            NewValue = currentHash
                        });
                    }
                }
            }, cancellationToken);

            Logger.Info("Change detection complete: {0} changes found in {1} tags",
                changes.Count, sourceTags.Count);

            return changes;
        }

        /// <summary>
        /// Synchronizes detected changes to all affected target views.
        /// Computes the minimal update set and applies changes with conflict resolution.
        /// </summary>
        public async Task<BatchPropagationResult> SynchronizeChangesAsync(
            List<TagChange> changes,
            PropagationOptions options = null,
            CancellationToken cancellationToken = default)
        {
            if (changes == null) throw new ArgumentNullException(nameof(changes));

            options ??= new PropagationOptions();
            var stopwatch = Stopwatch.StartNew();
            var result = new BatchPropagationResult { TotalSourceTags = changes.Count };

            Logger.Info("Synchronizing {0} tag changes", changes.Count);

            foreach (var change in changes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    TagInstance sourceTag = _repository.GetTag(change.TagId);
                    if (sourceTag == null)
                    {
                        Logger.Warn("Source tag {0} not found for synchronization", change.TagId);
                        continue;
                    }

                    // Find all propagated tags linked to this source
                    var propagatedTags = FindPropagatedTagsFromSource(change.TagId);

                    if (change.Type == ChangeType.Deleted)
                    {
                        // Handle source deletion: mark propagated tags as orphaned
                        foreach (var propTag in propagatedTags)
                        {
                            propTag.State = TagState.Orphaned;
                            _repository.UpdateTag(propTag);
                        }
                        continue;
                    }

                    // Sync content and position changes to propagated tags
                    foreach (var propTag in propagatedTags)
                    {
                        if (options.PreserveLocalOverrides && propTag.UserAdjusted)
                            continue;

                        bool updated = false;

                        if (change.Type == ChangeType.ContentChanged || change.Type == ChangeType.Created)
                        {
                            ViewTagContext targetContext = GetViewContext(propTag.ViewId);
                            ViewTagContext sourceContext = GetViewContext(sourceTag.ViewId);
                            if (targetContext != null && sourceContext != null)
                            {
                                string newContent = AdaptContent(
                                    sourceTag.ContentExpression,
                                    sourceContext.ViewType,
                                    targetContext.ViewType,
                                    sourceTag.CategoryName);
                                propTag.ContentExpression = newContent;
                                updated = true;
                            }
                        }

                        if (change.Type == ChangeType.PositionChanged && sourceTag.Placement != null)
                        {
                            ViewTagContext targetContext = GetViewContext(propTag.ViewId);
                            ViewTagContext sourceContext = GetViewContext(sourceTag.ViewId);
                            if (targetContext != null && sourceContext != null)
                            {
                                propTag.Placement = _coordinateTransformer.Transform(
                                    sourceTag.Placement,
                                    sourceContext.ViewType,
                                    targetContext.ViewType,
                                    sourceContext.Scale,
                                    targetContext.Scale);
                                updated = true;
                            }
                        }

                        if (updated)
                        {
                            propTag.LastModified = DateTime.UtcNow;
                            _repository.UpdateTag(propTag);
                        }
                    }

                    // Update snapshot
                    TakeSnapshot(sourceTag);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to synchronize change for tag {0}", change.TagId);
                }
            }

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;

            Logger.Info("Change synchronization complete in {0}ms", result.Duration.TotalMilliseconds);
            return result;
        }

        /// <summary>
        /// Finds all tags that were propagated from a given source tag.
        /// </summary>
        private List<TagInstance> FindPropagatedTagsFromSource(string sourceTagId)
        {
            var allTags = _repository.GetAllTags();
            return allTags
                .Where(t => t.Metadata.TryGetValue("PropagatedFrom", out object src) &&
                            string.Equals(src?.ToString(), sourceTagId, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        private void TakeSnapshot(TagInstance tag)
        {
            if (tag == null) return;
            string hash = ComputeTagHash(tag);
            lock (_snapshotLock)
            {
                _tagSnapshots[tag.TagId] = hash;
            }
        }

        private static string ComputeTagHash(TagInstance tag)
        {
            string content = $"{tag.ContentExpression}|{tag.Placement?.Position.X:F6}|" +
                             $"{tag.Placement?.Position.Y:F6}|{tag.TagFamilyName}|{tag.TagTypeName}|{tag.State}";
            int hash = content.GetHashCode();
            return hash.ToString("X8");
        }

        private static ChangeType DetermineChangeType(TagInstance tag, string previousHash, string currentHash)
        {
            if (tag.State == TagState.MarkedForDeletion) return ChangeType.Deleted;
            return ChangeType.ContentChanged;
        }

        #endregion

        #region Conflict Resolution

        /// <summary>
        /// Detects a conflict between a source tag and an existing target tag.
        /// </summary>
        private PropagationConflict DetectConflict(
            TagInstance sourceTag,
            TagInstance targetTag,
            PropagationTarget target)
        {
            bool contentDiffers = !string.Equals(
                sourceTag.ContentExpression,
                targetTag.ContentExpression,
                StringComparison.Ordinal);

            bool styleDiffers = !string.Equals(
                sourceTag.TagFamilyName,
                targetTag.TagFamilyName,
                StringComparison.OrdinalIgnoreCase);

            bool isLocalOverride = targetTag.UserAdjusted;

            if (!contentDiffers && !styleDiffers && !isLocalOverride)
                return null;

            ConflictType conflictType;
            string sourceValue, targetValue;

            if (isLocalOverride)
            {
                conflictType = ConflictType.LocalOverride;
                sourceValue = sourceTag.ContentExpression;
                targetValue = $"[User adjusted] {targetTag.ContentExpression}";
            }
            else if (styleDiffers)
            {
                conflictType = ConflictType.TagFamily;
                sourceValue = sourceTag.TagFamilyName;
                targetValue = targetTag.TagFamilyName;
            }
            else
            {
                conflictType = ConflictType.Content;
                sourceValue = sourceTag.ContentExpression;
                targetValue = targetTag.ContentExpression;
            }

            var conflict = new PropagationConflict
            {
                ConflictId = Guid.NewGuid().ToString("N"),
                SourceTagId = sourceTag.TagId,
                TargetTagId = targetTag.TagId,
                HostElementId = sourceTag.HostElementId,
                SourceViewId = sourceTag.ViewId,
                TargetViewId = targetTag.ViewId,
                Type = conflictType,
                Description = $"Conflict in {conflictType}: source='{sourceValue}', target='{targetValue}'",
                SourceValue = sourceValue,
                TargetValue = targetValue
            };

            lock (_conflictLock)
            {
                if (_conflictLog.Count >= MaxConflictLogSize)
                    _conflictLog.RemoveAt(0);
                _conflictLog.Add(conflict);
            }

            return conflict;
        }

        /// <summary>
        /// Resolves a conflict using the specified strategy.
        /// </summary>
        private void ResolveConflict(
            PropagationConflict conflict,
            ConflictResolutionStrategy strategy,
            TagInstance sourceTag,
            TagInstance targetTag)
        {
            conflict.Resolution = strategy;

            switch (strategy)
            {
                case ConflictResolutionStrategy.SourceWins:
                    targetTag.ContentExpression = sourceTag.ContentExpression;
                    targetTag.TagFamilyName = sourceTag.TagFamilyName;
                    targetTag.TagTypeName = sourceTag.TagTypeName;
                    targetTag.LastModified = DateTime.UtcNow;
                    _repository.UpdateTag(targetTag);
                    conflict.IsResolved = true;
                    conflict.ResolvedAt = DateTime.UtcNow;
                    break;

                case ConflictResolutionStrategy.TargetWins:
                    conflict.IsResolved = true;
                    conflict.ResolvedAt = DateTime.UtcNow;
                    break;

                case ConflictResolutionStrategy.Merge:
                    if (!targetTag.UserAdjusted)
                    {
                        targetTag.ContentExpression = sourceTag.ContentExpression;
                        targetTag.LastModified = DateTime.UtcNow;
                        _repository.UpdateTag(targetTag);
                    }
                    conflict.IsResolved = true;
                    conflict.ResolvedAt = DateTime.UtcNow;
                    break;

                case ConflictResolutionStrategy.AskUser:
                    conflict.IsResolved = false;
                    Logger.Info("Conflict {0} flagged for user review: {1}", conflict.ConflictId, conflict.Description);
                    break;
            }
        }

        /// <summary>
        /// Gets all unresolved conflicts requiring user review.
        /// </summary>
        public List<PropagationConflict> GetUnresolvedConflicts()
        {
            lock (_conflictLock)
            {
                return _conflictLog.Where(c => !c.IsResolved).ToList();
            }
        }

        /// <summary>
        /// Resolves a conflict by ID with a user-chosen strategy.
        /// </summary>
        public bool ResolveConflictById(string conflictId, ConflictResolutionStrategy strategy)
        {
            PropagationConflict conflict;
            lock (_conflictLock)
            {
                conflict = _conflictLog.FirstOrDefault(c => c.ConflictId == conflictId);
            }

            if (conflict == null) return false;

            TagInstance sourceTag = _repository.GetTag(conflict.SourceTagId);
            TagInstance targetTag = _repository.GetTag(conflict.TargetTagId);

            if (sourceTag == null || targetTag == null) return false;

            ResolveConflict(conflict, strategy, sourceTag, targetTag);
            return true;
        }

        /// <summary>
        /// Gets all conflicts from the conflict log.
        /// </summary>
        public List<PropagationConflict> GetConflictLog()
        {
            lock (_conflictLock)
            {
                return _conflictLog.ToList();
            }
        }

        #endregion

        #region Propagation Analytics

        /// <summary>
        /// Generates a comprehensive analytics report on propagation coverage, consistency,
        /// orphan detection, and staleness across all views.
        /// </summary>
        public async Task<PropagationAnalytics> GenerateAnalyticsAsync(
            CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            var analytics = new PropagationAnalytics();

            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var allTags = _repository.GetAllTags();
                var propagatedTags = allTags
                    .Where(t => t.Metadata.ContainsKey("PropagatedFrom"))
                    .ToList();

                // View coverage
                var allViewIds = new HashSet<int>();
                var viewsWithPropagation = new HashSet<int>();

                foreach (var tag in allTags)
                {
                    allViewIds.Add(tag.ViewId);
                    if (tag.Metadata.ContainsKey("PropagatedFrom"))
                        viewsWithPropagation.Add(tag.ViewId);
                }

                analytics.TotalViews = allViewIds.Count;
                analytics.ViewsWithPropagatedTags = viewsWithPropagation.Count;

                // Consistency score
                analytics.ConsistencyScore = ComputeConsistencyScore(allTags, propagatedTags);

                // Orphan detection
                foreach (var propTag in propagatedTags)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (propTag.Metadata.TryGetValue("PropagatedFrom", out object sourceIdObj))
                    {
                        string sourceId = sourceIdObj?.ToString();
                        TagInstance sourceTag = _repository.GetTag(sourceId);

                        if (sourceTag == null || sourceTag.State == TagState.MarkedForDeletion ||
                            sourceTag.State == TagState.Orphaned)
                        {
                            analytics.OrphanedTagIds.Add(propTag.TagId);
                        }
                    }
                }

                // Staleness tracking
                foreach (var propTag in propagatedTags)
                {
                    if (propTag.Metadata.TryGetValue("PropagatedFrom", out object sourceIdObj))
                    {
                        string sourceId = sourceIdObj?.ToString();
                        TagInstance sourceTag = _repository.GetTag(sourceId);

                        if (sourceTag != null && sourceTag.LastModified > propTag.LastModified)
                        {
                            analytics.StaleTags.Add(new StaleTagInfo
                            {
                                TagId = propTag.TagId,
                                SourceTagId = sourceId,
                                ViewId = propTag.ViewId,
                                StaleDuration = sourceTag.LastModified - propTag.LastModified
                            });
                        }
                    }
                }

                // Per-view status
                foreach (int viewId in allViewIds)
                {
                    var viewTags = allTags.Where(t => t.ViewId == viewId).ToList();
                    var viewPropTags = viewTags.Where(t => t.Metadata.ContainsKey("PropagatedFrom")).ToList();
                    var viewLocalTags = viewTags.Where(t => !t.Metadata.ContainsKey("PropagatedFrom")).ToList();

                    ViewTagContext ctx = GetViewContext(viewId);

                    analytics.ViewStatuses[viewId] = new ViewPropagationStatus
                    {
                        ViewId = viewId,
                        ViewType = ctx?.ViewType ?? TagViewType.FloorPlan,
                        PropagatedTagCount = viewPropTags.Count,
                        LocalTagCount = viewLocalTags.Count,
                        StaleCount = analytics.StaleTags.Count(s => s.ViewId == viewId),
                        OrphanCount = analytics.OrphanedTagIds
                            .Count(id => allTags.Any(t => t.TagId == id && t.ViewId == viewId)),
                        IsSourceView = viewLocalTags.Any(t =>
                            propagatedTags.Any(p =>
                                p.Metadata.TryGetValue("PropagatedFrom", out object src) &&
                                string.Equals(src?.ToString(), t.TagId, StringComparison.OrdinalIgnoreCase)))
                    };
                }

                // Unresolved conflicts
                lock (_conflictLock)
                {
                    analytics.UnresolvedConflicts = _conflictLog.Where(c => !c.IsResolved).ToList();
                }

                // Active mappings
                lock (_mappingsLock)
                {
                    analytics.ActiveMappings = _mappings.Values.Count(m => m.IsActive);
                }

            }, cancellationToken);

            stopwatch.Stop();
            analytics.AnalysisDuration = stopwatch.Elapsed;

            Logger.Info("Propagation analytics: {0} views, {1:P0} coverage, {2:F2} consistency, " +
                        "{3} orphans, {4} stale, {5} unresolved conflicts",
                analytics.TotalViews, analytics.CoverageRatio, analytics.ConsistencyScore,
                analytics.OrphanedTagIds.Count, analytics.StaleTags.Count,
                analytics.UnresolvedConflicts.Count);

            return analytics;
        }

        /// <summary>
        /// Computes a consistency score measuring how uniformly elements are tagged
        /// across all views where they appear.
        /// </summary>
        private double ComputeConsistencyScore(List<TagInstance> allTags, List<TagInstance> propagatedTags)
        {
            if (allTags.Count == 0) return 1.0;

            var elementGroups = allTags
                .Where(t => t.State == TagState.Active)
                .GroupBy(t => t.HostElementId)
                .Where(g => g.Count() > 1)
                .ToList();

            if (elementGroups.Count == 0) return 1.0;

            double totalScore = 0.0;
            int groupCount = 0;

            foreach (var group in elementGroups)
            {
                var tags = group.ToList();
                var contentExpressions = tags.Select(t => t.ContentExpression ?? "").Distinct().ToList();

                // Elements with consistent content across views score higher
                double contentConsistency = 1.0 / contentExpressions.Count;

                // Check if all views where the element appears have tags
                var elementViews = GetViewsForElement(group.Key);
                double coverageRatio = elementViews.Count > 0
                    ? (double)tags.Count / elementViews.Count
                    : 1.0;

                totalScore += (contentConsistency * 0.6 + coverageRatio * 0.4);
                groupCount++;
            }

            return groupCount > 0 ? totalScore / groupCount : 1.0;
        }

        #endregion

        #region Undo Support

        /// <summary>
        /// Undoes a propagation operation by deleting all tags created by that operation.
        /// </summary>
        public int UndoPropagation(string operationId)
        {
            if (string.IsNullOrWhiteSpace(operationId)) return 0;

            List<string> tagIds;
            lock (_undoLock)
            {
                if (!_undoHistory.TryGetValue(operationId, out tagIds))
                {
                    Logger.Warn("No undo history for operation {0}", operationId);
                    return 0;
                }
            }

            int deletedCount = 0;
            foreach (string tagId in tagIds)
            {
                TagInstance tag = _repository.GetTag(tagId);
                if (tag != null)
                {
                    tag.State = TagState.MarkedForDeletion;
                    _repository.UpdateTag(tag);
                    _repository.RemoveTag(tagId);
                    deletedCount++;
                }
            }

            lock (_undoLock)
            {
                _undoHistory.Remove(operationId);
            }

            Logger.Info("Undid propagation operation {0}: {1} tags deleted", operationId, deletedCount);
            return deletedCount;
        }

        /// <summary>
        /// Undoes all tags created during a propagation result.
        /// </summary>
        public int UndoPropagation(PropagationResult result)
        {
            if (result?.OperationId != null)
                return UndoPropagation(result.OperationId);

            if (result?.UndoTagIds == null || result.UndoTagIds.Count == 0) return 0;

            int deletedCount = 0;
            foreach (string tagId in result.UndoTagIds)
            {
                TagInstance tag = _repository.GetTag(tagId);
                if (tag != null)
                {
                    tag.State = TagState.MarkedForDeletion;
                    _repository.UpdateTag(tag);
                    _repository.RemoveTag(tagId);
                    deletedCount++;
                }
            }

            Logger.Info("Undid propagation result: {0} tags deleted", deletedCount);
            return deletedCount;
        }

        private void RecordUndoOperation(string operationId, List<string> tagIds)
        {
            lock (_undoLock)
            {
                while (_undoHistory.Count >= MaxUndoHistorySize)
                {
                    string oldestKey = _undoHistory.Keys.First();
                    _undoHistory.Remove(oldestKey);
                }
                _undoHistory[operationId] = new List<string>(tagIds);
            }
        }

        #endregion

        #region Changelog

        /// <summary>
        /// Records an entry in the propagation changelog for audit tracking.
        /// </summary>
        private void RecordChangelog(ChangelogEntry entry)
        {
            lock (_changelogLock)
            {
                if (_changelog.Count >= MaxChangelogSize)
                    _changelog.RemoveAt(0);
                _changelog.Add(entry);
            }
        }

        /// <summary>
        /// Gets the changelog, optionally filtered by operation ID or time range.
        /// </summary>
        public List<ChangelogEntry> GetChangelog(
            string operationId = null,
            DateTime? since = null,
            int maxEntries = 100)
        {
            lock (_changelogLock)
            {
                IEnumerable<ChangelogEntry> query = _changelog;

                if (!string.IsNullOrEmpty(operationId))
                    query = query.Where(e => e.OperationId == operationId);

                if (since.HasValue)
                    query = query.Where(e => e.Timestamp >= since.Value);

                return query
                    .OrderByDescending(e => e.Timestamp)
                    .Take(maxEntries)
                    .ToList();
            }
        }

        /// <summary>
        /// Clears the changelog.
        /// </summary>
        public void ClearChangelog()
        {
            lock (_changelogLock)
            {
                _changelog.Clear();
            }
            Logger.Debug("Changelog cleared");
        }

        #endregion

        #region Diagnostics

        /// <summary>
        /// Gets the number of registered propagation rules.
        /// </summary>
        public int RuleCount
        {
            get { lock (_rulesLock) { return _rules.Count; } }
        }

        /// <summary>
        /// Gets the number of active propagation mappings.
        /// </summary>
        public int MappingCount
        {
            get { lock (_mappingsLock) { return _mappings.Count; } }
        }

        /// <summary>
        /// Gets the number of undo operations currently tracked.
        /// </summary>
        public int UndoHistoryCount
        {
            get { lock (_undoLock) { return _undoHistory.Count; } }
        }

        /// <summary>
        /// Gets the number of entries in the changelog.
        /// </summary>
        public int ChangelogCount
        {
            get { lock (_changelogLock) { return _changelog.Count; } }
        }

        /// <summary>
        /// Gets the number of entries in the conflict log.
        /// </summary>
        public int ConflictLogCount
        {
            get { lock (_conflictLock) { return _conflictLog.Count; } }
        }

        /// <summary>
        /// Clears all undo history.
        /// </summary>
        public void ClearUndoHistory()
        {
            lock (_undoLock) { _undoHistory.Clear(); }
            Logger.Debug("Undo history cleared");
        }

        /// <summary>
        /// Clears all tag snapshots, forcing full change detection on next sync.
        /// </summary>
        public void ClearSnapshots()
        {
            lock (_snapshotLock) { _tagSnapshots.Clear(); }
            Logger.Debug("Tag snapshots cleared");
        }

        /// <summary>
        /// Clears the conflict log.
        /// </summary>
        public void ClearConflictLog()
        {
            lock (_conflictLock) { _conflictLog.Clear(); }
            Logger.Debug("Conflict log cleared");
        }

        #endregion

        #region Helpers

        private static TagPlacement ClonePlacement(TagPlacement source)
        {
            if (source == null)
            {
                return new TagPlacement
                {
                    Position = new Point2D(0, 0),
                    LeaderEndPoint = new Point2D(0, 0),
                    LeaderType = LeaderType.None,
                    Orientation = TagOrientation.Horizontal,
                    PreferredPosition = TagPosition.Center,
                    ResolvedPosition = TagPosition.Center
                };
            }

            return new TagPlacement
            {
                Position = new Point2D(source.Position.X, source.Position.Y),
                LeaderEndPoint = new Point2D(source.LeaderEndPoint.X, source.LeaderEndPoint.Y),
                LeaderElbowPoint = source.LeaderElbowPoint.HasValue
                    ? new Point2D(source.LeaderElbowPoint.Value.X, source.LeaderElbowPoint.Value.Y)
                    : (Point2D?)null,
                LeaderType = source.LeaderType,
                LeaderLength = source.LeaderLength,
                Rotation = source.Rotation,
                PreferredPosition = source.PreferredPosition,
                ResolvedPosition = source.ResolvedPosition,
                Orientation = source.Orientation,
                OffsetX = source.OffsetX,
                OffsetY = source.OffsetY,
                IsStacked = source.IsStacked,
                StackedWithTagId = source.StackedWithTagId
            };
        }

        #endregion
    }

    #endregion

    #region ContentAdapter

    /// <summary>
    /// Manages content expression mappings by category and view type.
    /// When propagating a tag, the ContentAdapter determines the appropriate
    /// content expression for the target view type based on the element's category.
    /// </summary>
    public class ContentAdapter
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly Dictionary<string, string> _mappings;
        private readonly object _mappingsLock = new object();

        public ContentAdapter()
        {
            _mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Registers a content expression mapping for a category and view type.
        /// </summary>
        public void RegisterMapping(string categoryName, TagViewType viewType, string contentExpression)
        {
            if (string.IsNullOrWhiteSpace(categoryName))
                throw new ArgumentException("Category name is required.", nameof(categoryName));
            if (string.IsNullOrWhiteSpace(contentExpression))
                throw new ArgumentException("Content expression is required.", nameof(contentExpression));

            string key = BuildKey(categoryName, viewType);
            lock (_mappingsLock)
            {
                _mappings[key] = contentExpression;
            }
        }

        /// <summary>
        /// Removes a content expression mapping.
        /// </summary>
        public bool RemoveMapping(string categoryName, TagViewType viewType)
        {
            string key = BuildKey(categoryName, viewType);
            lock (_mappingsLock)
            {
                return _mappings.Remove(key);
            }
        }

        /// <summary>
        /// Gets the registered content expression for a category and view type.
        /// </summary>
        public string GetMapping(string categoryName, TagViewType viewType)
        {
            if (string.IsNullOrEmpty(categoryName)) return null;
            string key = BuildKey(categoryName, viewType);
            lock (_mappingsLock)
            {
                return _mappings.TryGetValue(key, out string expression) ? expression : null;
            }
        }

        /// <summary>
        /// Gets all registered mappings for a specific category.
        /// </summary>
        public Dictionary<TagViewType, string> GetMappingsForCategory(string categoryName)
        {
            if (string.IsNullOrEmpty(categoryName))
                return new Dictionary<TagViewType, string>();

            var result = new Dictionary<TagViewType, string>();
            string prefix = categoryName.ToUpperInvariant() + "|";

            lock (_mappingsLock)
            {
                foreach (var kvp in _mappings)
                {
                    if (kvp.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        string viewTypePart = kvp.Key.Substring(prefix.Length);
                        if (Enum.TryParse<TagViewType>(viewTypePart, true, out var viewType))
                            result[viewType] = kvp.Value;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Gets the total number of registered content mappings.
        /// </summary>
        public int MappingCount
        {
            get { lock (_mappingsLock) { return _mappings.Count; } }
        }

        /// <summary>
        /// Clears all registered content mappings.
        /// </summary>
        public void ClearAll()
        {
            lock (_mappingsLock) { _mappings.Clear(); }
            Logger.Debug("All content mappings cleared");
        }

        private static string BuildKey(string categoryName, TagViewType viewType)
        {
            return $"{categoryName}|{viewType}";
        }
    }

    #endregion
}
