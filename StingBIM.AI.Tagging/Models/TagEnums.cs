// StingBIM.AI.Tagging - SuperIntelligent Tagging System
// TagEnums.cs - All enumerations for the tagging system
// Merges capabilities from Smart Annotation, Naviate, and Ideate with multiplied intelligence

namespace StingBIM.AI.Tagging.Models
{
    /// <summary>
    /// Current state of a managed tag instance in the model.
    /// </summary>
    public enum TagState
    {
        /// <summary>Tag is active and visible in its view.</summary>
        Active,

        /// <summary>Tag exists but is hidden by view filter, crop, or override.</summary>
        Hidden,

        /// <summary>Tag's host element has been deleted or is no longer in the view.</summary>
        Orphaned,

        /// <summary>Tag text no longer matches current parameter values.</summary>
        Stale,

        /// <summary>Tag was flagged by quality analyzer and needs manual review.</summary>
        PendingReview,

        /// <summary>Tag is being processed (placement in progress).</summary>
        Processing,

        /// <summary>Tag has been scheduled for deletion.</summary>
        MarkedForDeletion
    }

    /// <summary>
    /// Strategy for determining tag placement positions.
    /// Surpasses BIMLOGIQ's Full/Quick modes with a richer strategy hierarchy.
    /// </summary>
    public enum PlacementStrategy
    {
        /// <summary>Full AI optimization: rules + templates + collision + intelligence + learning.</summary>
        Automatic,

        /// <summary>Apply template positions first, then resolve collisions.</summary>
        TemplateFirst,

        /// <summary>Apply rule-based positions first, then optimize.</summary>
        RuleFirst,

        /// <summary>Apply learned user preferences first, falling back to templates.</summary>
        IntelligenceFirst,

        /// <summary>Strict grid alignment mode - snap to alignment rails for clean documentation.</summary>
        StrictAlignment,

        /// <summary>Relaxed mode - minimize leader lengths with non-overlap guarantee.</summary>
        RelaxedAlignment,

        /// <summary>Compact mode - place tags as close to elements as possible.</summary>
        Compact,

        /// <summary>Manual hint mode - user provides rough position, system refines.</summary>
        ManualHint
    }

    /// <summary>
    /// Leader line types for tag-to-element connections.
    /// Surpasses BIMLOGIQ leader customization and Naviate NVVectorMoveTolerance system.
    /// </summary>
    public enum LeaderType
    {
        /// <summary>No leader line - tag placed directly on/near element.</summary>
        None,

        /// <summary>Straight line from tag to element.</summary>
        Straight,

        /// <summary>Leader with a horizontal elbow segment.</summary>
        Elbow,

        /// <summary>Curved arc leader for aesthetic placement.</summary>
        Arc,

        /// <summary>System automatically selects the best leader type based on distance and context.</summary>
        Auto
    }

    /// <summary>
    /// Tag alignment mode controlling how tags relate to neighboring tags.
    /// Surpasses BIMLOGIQ Strict/Relaxed with additional modes.
    /// </summary>
    public enum AlignmentMode
    {
        /// <summary>No alignment - each tag positioned independently.</summary>
        None,

        /// <summary>Tags snap to horizontal/vertical alignment rails for grid-like layout.</summary>
        Strict,

        /// <summary>Tags prefer alignment but prioritize proximity to host element.</summary>
        Relaxed,

        /// <summary>Tags align along the axis of their host elements (e.g., along a duct run).</summary>
        ElementAxis,

        /// <summary>Tags form columns/rows parallel to the dominant element direction.</summary>
        Columnar
    }

    /// <summary>
    /// Action to take when a tag collision is detected.
    /// Surpasses BIMLOGIQ collision avoidance with 6 resolution strategies.
    /// </summary>
    public enum CollisionAction
    {
        /// <summary>Shift tag by minimum displacement vector to clear the overlap.</summary>
        Nudge,

        /// <summary>Try the next candidate position from the template's priority list.</summary>
        Reposition,

        /// <summary>Stack vertically with the conflicting tag under a shared leader.</summary>
        Stack,

        /// <summary>Abbreviate tag text to reduce its footprint.</summary>
        Abbreviate,

        /// <summary>Extend/reroute leader to reach a clear zone further from the element.</summary>
        LeaderReroute,

        /// <summary>Flag the tag for manual placement by the user.</summary>
        FlagManual
    }

    /// <summary>
    /// Quality issue types detected by the TagQualityAnalyzer.
    /// Surpasses Ideate Review with 8 comprehensive check types.
    /// </summary>
    public enum QualityIssueType
    {
        /// <summary>Tag bounding box overlaps another annotation's bounding box.</summary>
        Clash,

        /// <summary>Tag's host element has been deleted or moved out of view.</summary>
        Orphan,

        /// <summary>Same element tagged multiple times in the same view.</summary>
        Duplicate,

        /// <summary>Tag displays empty, null, or "?" text.</summary>
        Blank,

        /// <summary>Tag exists but is not visible due to filters, crop, or overrides.</summary>
        Hidden,

        /// <summary>Tag text does not match expected format pattern.</summary>
        UnexpectedValue,

        /// <summary>Tag breaks alignment with neighboring tags of the same category.</summary>
        Misaligned,

        /// <summary>Tag text no longer matches current element parameter values.</summary>
        Stale
    }

    /// <summary>
    /// Severity level for quality issues.
    /// </summary>
    public enum IssueSeverity
    {
        /// <summary>Informational - cosmetic or preference-based issue.</summary>
        Info,

        /// <summary>Warning - should be fixed but won't cause documentation errors.</summary>
        Warning,

        /// <summary>Error - significant issue that needs correction.</summary>
        Error,

        /// <summary>Critical - will cause incorrect documentation or missing information.</summary>
        Critical
    }

    /// <summary>
    /// Scope for view-level tagging operations.
    /// Surpasses BIMLOGIQ batch processing and Ideate multi-view tagging.
    /// </summary>
    public enum ViewTagMode
    {
        /// <summary>Process only the active view.</summary>
        SingleView,

        /// <summary>Process a user-selected set of views.</summary>
        SelectedViews,

        /// <summary>Process all views currently open in the Revit session.</summary>
        OpenViews,

        /// <summary>Process all views placed on a specific sheet.</summary>
        AllViewsOnSheet,

        /// <summary>Process all views placed on any sheet in the project.</summary>
        AllPlacedViews,

        /// <summary>Process every view in the entire project.</summary>
        AllProjectViews
    }

    /// <summary>
    /// Preferred position for tag relative to its host element.
    /// 9-position grid + insertion point (surpasses Naviate's 9+1 system).
    /// </summary>
    public enum TagPosition
    {
        TopLeft,
        Top,
        TopRight,
        Left,
        Center,
        Right,
        BottomLeft,
        Bottom,
        BottomRight,

        /// <summary>Place at top-center of the element (alias for Top).</summary>
        TopCenter,

        /// <summary>Place at bottom-center of the element (alias for Bottom).</summary>
        BottomCenter,

        /// <summary>Place at middle-left of the element (alias for Left).</summary>
        MiddleLeft,

        /// <summary>Place at middle-right of the element (alias for Right).</summary>
        MiddleRight,

        /// <summary>Place at the element's Revit insertion point.</summary>
        InsertionPoint,

        /// <summary>System determines optimal position based on available space.</summary>
        Auto
    }

    /// <summary>
    /// Tag text orientation relative to the view.
    /// Surpasses BIMLOGIQ Auto/Horizontal/Vertical with element-following mode.
    /// </summary>
    public enum TagOrientation
    {
        /// <summary>Always horizontal regardless of element rotation.</summary>
        Horizontal,

        /// <summary>Always vertical regardless of element rotation.</summary>
        Vertical,

        /// <summary>Rotate to match host element orientation.</summary>
        FollowElement,

        /// <summary>System selects best orientation for readability.</summary>
        Auto
    }

    /// <summary>
    /// Type of element cluster detected by ClusterDetector.
    /// Surpasses BIMLOGIQ "Typical Tag Filter" with pattern classification.
    /// </summary>
    public enum ClusterType
    {
        /// <summary>Elements arranged in a straight line with regular spacing.</summary>
        Linear,

        /// <summary>Elements arranged in a rectangular grid pattern.</summary>
        Grid,

        /// <summary>Elements arranged radially around a center point.</summary>
        Radial,

        /// <summary>Elements grouped by proximity but without a regular geometric pattern.</summary>
        Irregular,

        /// <summary>Single element, no cluster detected.</summary>
        None
    }

    /// <summary>
    /// Strategy for tagging a detected cluster of elements.
    /// </summary>
    public enum ClusterTagStrategy
    {
        /// <summary>Tag every element individually.</summary>
        TagAll,

        /// <summary>Tag one representative element with a "(typical)" note.</summary>
        TagTypical,

        /// <summary>Tag one representative with count written to a parameter.</summary>
        TagWithCount,

        /// <summary>Tag first and last elements with a range note.</summary>
        TagRange,

        /// <summary>Tag one representative with aligned/stacked formatting for the group.</summary>
        TagGrouped
    }

    /// <summary>
    /// Rule condition operators for the TagRuleEngine.
    /// Surpasses Ideate's rule-based tagging with richer predicate types.
    /// </summary>
    public enum RuleOperator
    {
        Equals,
        NotEquals,
        Contains,
        StartsWith,
        EndsWith,
        GreaterThan,
        LessThan,
        GreaterThanOrEqual,
        LessThanOrEqual,
        RegexMatch,
        IsNull,
        IsNotNull,
        In,
        NotIn
    }

    /// <summary>
    /// How multiple rule conditions combine.
    /// </summary>
    public enum RuleLogic
    {
        /// <summary>All conditions must be true.</summary>
        And,

        /// <summary>Any condition can be true.</summary>
        Or
    }

    /// <summary>
    /// Type of tag operation for undo/redo tracking.
    /// </summary>
    public enum TagOperationType
    {
        Create,
        Move,
        Delete,
        Restyle,
        ReText,
        AddLeader,
        RemoveLeader,
        Stack,
        Unstack
    }

    /// <summary>
    /// Revit view types relevant to tagging behavior.
    /// Different view types get different tag templates and rules.
    /// </summary>
    public enum TagViewType
    {
        FloorPlan,
        CeilingPlan,
        StructuralPlan,
        Section,
        Elevation,
        Detail,
        ThreeDimensional,
        Drafting,
        AreaPlan,
        Legend
    }

    /// <summary>
    /// Source that triggered tag creation for audit trail.
    /// </summary>
    public enum TagCreationSource
    {
        /// <summary>Created by the automated placement engine.</summary>
        AutomatedPlacement,

        /// <summary>Created by a rule evaluation.</summary>
        RuleEngine,

        /// <summary>Created from a template application.</summary>
        Template,

        /// <summary>Created by the intelligence engine's predictive placement.</summary>
        IntelligencePrediction,

        /// <summary>Created manually by the user with system assistance.</summary>
        UserAssisted,

        /// <summary>Imported from an external source.</summary>
        Imported,

        /// <summary>Created during a batch view processing operation.</summary>
        BatchProcessing
    }
}
