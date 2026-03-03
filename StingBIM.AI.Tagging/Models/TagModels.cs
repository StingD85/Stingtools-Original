// StingBIM.AI.Tagging - SuperIntelligent Tagging System
// TagModels.cs - Core data transfer objects for the tagging system
// Merges capabilities from Smart Annotation, Naviate, and Ideate with multiplied intelligence

using System;
using System.Collections.Generic;

namespace StingBIM.AI.Tagging.Models
{
    #region Geometry Primitives (2D View-Space)

    /// <summary>
    /// 2D point in view coordinate space. Tags exist in 2D, not 3D.
    /// </summary>
    public struct Point2D
    {
        public double X { get; set; }
        public double Y { get; set; }

        public Point2D(double x, double y) { X = x; Y = y; }

        public double DistanceTo(Point2D other)
        {
            double dx = X - other.X;
            double dy = Y - other.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public Point2D Offset(double dx, double dy) => new Point2D(X + dx, Y + dy);

        public static Point2D Midpoint(Point2D a, Point2D b) =>
            new Point2D((a.X + b.X) / 2.0, (a.Y + b.Y) / 2.0);

        public override string ToString() => $"({X:F4}, {Y:F4})";
    }

    /// <summary>
    /// 2D vector for displacement and direction calculations.
    /// </summary>
    public struct Vector2D
    {
        public double X { get; set; }
        public double Y { get; set; }

        public Vector2D(double x, double y) { X = x; Y = y; }

        public double Length => Math.Sqrt(X * X + Y * Y);

        public Vector2D Normalize()
        {
            double len = Length;
            return len > 1e-10 ? new Vector2D(X / len, Y / len) : new Vector2D(0, 0);
        }

        public Vector2D Scale(double factor) => new Vector2D(X * factor, Y * factor);

        public static Vector2D FromPoints(Point2D from, Point2D to) =>
            new Vector2D(to.X - from.X, to.Y - from.Y);
    }

    /// <summary>
    /// Axis-aligned 2D bounding box in view coordinates.
    /// Used for collision detection between tags and annotations.
    /// </summary>
    public class TagBounds2D
    {
        public double MinX { get; set; }
        public double MinY { get; set; }
        public double MaxX { get; set; }
        public double MaxY { get; set; }

        public TagBounds2D() { }

        public TagBounds2D(double minX, double minY, double maxX, double maxY)
        {
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }

        public double Width => MaxX - MinX;
        public double Height => MaxY - MinY;
        public double Area => Width * Height;
        public Point2D Center => new Point2D((MinX + MaxX) / 2.0, (MinY + MaxY) / 2.0);

        // Alias properties for spatial indexing compatibility
        public double Left => MinX;
        public double Right => MaxX;
        public double Top => MaxY;
        public double Bottom => MinY;

        /// <summary>
        /// Tests whether this bounds overlaps with another, with optional clearance margin.
        /// </summary>
        public bool Intersects(TagBounds2D other, double clearance = 0.0)
        {
            return MinX - clearance < other.MaxX + clearance &&
                   MaxX + clearance > other.MinX - clearance &&
                   MinY - clearance < other.MaxY + clearance &&
                   MaxY + clearance > other.MinY - clearance;
        }

        /// <summary>
        /// Calculates the overlap area with another bounds.
        /// </summary>
        public double OverlapArea(TagBounds2D other)
        {
            double overlapX = Math.Max(0, Math.Min(MaxX, other.MaxX) - Math.Max(MinX, other.MinX));
            double overlapY = Math.Max(0, Math.Min(MaxY, other.MaxY) - Math.Max(MinY, other.MinY));
            return overlapX * overlapY;
        }

        /// <summary>
        /// Returns expanded bounds by the given margin on all sides.
        /// </summary>
        public TagBounds2D Expand(double margin)
        {
            return new TagBounds2D(MinX - margin, MinY - margin, MaxX + margin, MaxY + margin);
        }

        /// <summary>
        /// Computes the minimum displacement vector to separate this from another bounds.
        /// </summary>
        public Vector2D MinimumSeparationVector(TagBounds2D other)
        {
            double left = other.MinX - MaxX;
            double right = other.MaxX - MinX;
            double down = other.MinY - MaxY;
            double up = other.MaxY - MinY;

            double minAbsX = Math.Abs(left) < Math.Abs(right) ? left : right;
            double minAbsY = Math.Abs(down) < Math.Abs(up) ? down : up;

            if (Math.Abs(minAbsX) < Math.Abs(minAbsY))
                return new Vector2D(-minAbsX, 0);
            else
                return new Vector2D(0, -minAbsY);
        }
    }

    #endregion

    #region Core Tag Representations

    /// <summary>
    /// Canonical representation of a managed tag in the model.
    /// This is the central DTO passed between all subsystems.
    /// </summary>
    public class TagInstance
    {
        /// <summary>Unique identifier within the tagging system.</summary>
        public string TagId { get; set; }

        /// <summary>Revit ElementId of the IndependentTag element (0 if not yet created).</summary>
        public int RevitElementId { get; set; }

        /// <summary>Revit ElementId of the host element being tagged.</summary>
        public int HostElementId { get; set; }

        /// <summary>Revit ElementId of the view containing this tag.</summary>
        public int ViewId { get; set; }

        /// <summary>Revit category name of the host element (e.g., "Doors", "Walls").</summary>
        public string CategoryName { get; set; }

        /// <summary>Family name of the host element.</summary>
        public string FamilyName { get; set; }

        /// <summary>Type name of the host element.</summary>
        public string TypeName { get; set; }

        /// <summary>Name of the Revit tag family used for annotation.</summary>
        public string TagFamilyName { get; set; }

        /// <summary>Name of the specific tag type within the family.</summary>
        public string TagTypeName { get; set; }

        /// <summary>Current placement data for this tag.</summary>
        public TagPlacement Placement { get; set; }

        /// <summary>Current text content displayed by the tag.</summary>
        public string DisplayText { get; set; }

        /// <summary>Content expression that generates the display text.</summary>
        public string ContentExpression { get; set; }

        /// <summary>Current state of this tag.</summary>
        public TagState State { get; set; }

        /// <summary>Bounding box of this tag in view coordinates.</summary>
        public TagBounds2D Bounds { get; set; }

        /// <summary>Name of the rule that created this tag.</summary>
        public string CreatedByRule { get; set; }

        /// <summary>Name of the template used for placement.</summary>
        public string CreatedByTemplate { get; set; }

        /// <summary>How this tag was created.</summary>
        public TagCreationSource CreationSource { get; set; }

        /// <summary>Placement quality score from 0.0 (worst) to 1.0 (optimal).</summary>
        public double PlacementScore { get; set; }

        /// <summary>When this tag was last modified.</summary>
        public DateTime LastModified { get; set; }

        /// <summary>Whether this tag was manually adjusted by the user after placement.</summary>
        public bool UserAdjusted { get; set; }

        /// <summary>Additional metadata for extensibility.</summary>
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Placement data for a tag - position, leader, rotation.
    /// </summary>
    public class TagPlacement
    {
        /// <summary>Position of the tag anchor point in view coordinates.</summary>
        public Point2D Position { get; set; }

        /// <summary>Point on the host element where the leader connects (if applicable).</summary>
        public Point2D LeaderEndPoint { get; set; }

        /// <summary>Leader elbow point for elbow-style leaders.</summary>
        public Point2D? LeaderElbowPoint { get; set; }

        /// <summary>Leader line type.</summary>
        public LeaderType LeaderType { get; set; }

        /// <summary>Leader length in model units.</summary>
        public double LeaderLength { get; set; }

        /// <summary>Tag rotation angle in radians.</summary>
        public double Rotation { get; set; }

        /// <summary>Preferred position relative to host element.</summary>
        public TagPosition PreferredPosition { get; set; }

        /// <summary>Actual resolved position relative to host element.</summary>
        public TagPosition ResolvedPosition { get; set; }

        /// <summary>Orientation of the tag text.</summary>
        public TagOrientation Orientation { get; set; }

        /// <summary>X offset from the preferred position in model units.</summary>
        public double OffsetX { get; set; }

        /// <summary>Y offset from the preferred position in model units.</summary>
        public double OffsetY { get; set; }

        /// <summary>Whether this tag is stacked with another tag.</summary>
        public bool IsStacked { get; set; }

        /// <summary>TagId of the tag this is stacked with (if any).</summary>
        public string StackedWithTagId { get; set; }
    }

    #endregion

    #region Placement Engine DTOs

    /// <summary>
    /// Request to the tag placement engine for processing.
    /// </summary>
    public class TagPlacementRequest
    {
        /// <summary>Element IDs to tag. If empty, tags all applicable elements in the views.</summary>
        public List<int> ElementIds { get; set; } = new List<int>();

        /// <summary>View IDs to process.</summary>
        public List<int> ViewIds { get; set; } = new List<int>();

        /// <summary>Overall placement strategy.</summary>
        public PlacementStrategy Strategy { get; set; } = PlacementStrategy.Automatic;

        /// <summary>Name of the rule group to apply (null for default).</summary>
        public string RuleGroupName { get; set; }

        /// <summary>Specific template names to use (null for auto-selection).</summary>
        public List<string> TemplateNames { get; set; }

        /// <summary>Alignment mode for this batch.</summary>
        public AlignmentMode Alignment { get; set; } = AlignmentMode.Relaxed;

        /// <summary>Whether to replace existing tags on the same elements.</summary>
        public bool ReplaceExisting { get; set; }

        /// <summary>Whether to run quality checks after placement.</summary>
        public bool RunQualityCheck { get; set; } = true;

        /// <summary>Whether to tag elements in linked files.</summary>
        public bool IncludeLinkedFiles { get; set; }

        /// <summary>Whether to detect and handle clustered elements.</summary>
        public bool EnableClusterDetection { get; set; } = true;

        /// <summary>Minimum collision clearance in model units.</summary>
        public double CollisionClearance { get; set; } = 0.002; // ~2mm

        /// <summary>Additional options for extensibility.</summary>
        public Dictionary<string, object> Options { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// A candidate position being evaluated by the placement engine.
    /// </summary>
    public class PlacementCandidate
    {
        /// <summary>Candidate position in view coordinates.</summary>
        public Point2D Position { get; set; }

        /// <summary>Position relative to host element.</summary>
        public TagPosition RelativePosition { get; set; }

        /// <summary>Leader type if needed at this position.</summary>
        public LeaderType LeaderType { get; set; }

        /// <summary>Leader length if needed.</summary>
        public double LeaderLength { get; set; }

        /// <summary>Composite score for this candidate (higher is better).</summary>
        public double Score { get; set; }

        /// <summary>Individual score components for analysis.</summary>
        public PlacementScoreBreakdown ScoreBreakdown { get; set; }

        /// <summary>Priority level from the template (lower = try first).</summary>
        public int TemplatePriority { get; set; }

        /// <summary>Whether this candidate collides with existing annotations.</summary>
        public bool HasCollision { get; set; }

        /// <summary>Number of collisions at this position.</summary>
        public int CollisionCount { get; set; }
    }

    /// <summary>
    /// Breakdown of the score components for a placement candidate.
    /// Each component is weighted and summed to produce the final score.
    /// </summary>
    public class PlacementScoreBreakdown
    {
        /// <summary>Score component for proximity to host element (closer = higher).</summary>
        public double ProximityScore { get; set; }

        /// <summary>Score component for collision freedom (no collisions = higher).</summary>
        public double CollisionScore { get; set; }

        /// <summary>Score component for alignment with neighboring tags.</summary>
        public double AlignmentScore { get; set; }

        /// <summary>Score component for leader line quality (shorter/simpler = higher).</summary>
        public double LeaderScore { get; set; }

        /// <summary>Score component for readability (clear background, good orientation).</summary>
        public double ReadabilityScore { get; set; }

        /// <summary>Score component from user preference learning.</summary>
        public double PreferenceScore { get; set; }

        /// <summary>Score component for template priority compliance.</summary>
        public double TemplatePriorityScore { get; set; }

        /// <summary>Composite weighted score.</summary>
        public double TotalScore { get; set; }
    }

    /// <summary>
    /// Result of a placement operation for a single tag.
    /// </summary>
    public class TagPlacementResult
    {
        /// <summary>The placed tag instance.</summary>
        public TagInstance Tag { get; set; }

        /// <summary>Whether placement was successful.</summary>
        public bool Success { get; set; }

        /// <summary>All candidates evaluated with their scores.</summary>
        public List<PlacementCandidate> EvaluatedCandidates { get; set; } = new List<PlacementCandidate>();

        /// <summary>The winning candidate.</summary>
        public PlacementCandidate SelectedCandidate { get; set; }

        /// <summary>Collision actions taken during placement.</summary>
        public List<CollisionAction> CollisionActionsApplied { get; set; } = new List<CollisionAction>();

        /// <summary>Reason for failure if Success is false.</summary>
        public string FailureReason { get; set; }

        /// <summary>Fallback level reached (0 = primary, higher = more fallback).</summary>
        public int FallbackLevel { get; set; }
    }

    /// <summary>
    /// Result of a batch placement operation across multiple elements/views.
    /// </summary>
    public class BatchPlacementResult
    {
        /// <summary>Individual results for each tag.</summary>
        public List<TagPlacementResult> Results { get; set; } = new List<TagPlacementResult>();

        /// <summary>Total elements processed.</summary>
        public int TotalElements { get; set; }

        /// <summary>Successfully tagged elements.</summary>
        public int SuccessCount { get; set; }

        /// <summary>Failed placements.</summary>
        public int FailureCount { get; set; }

        /// <summary>Elements skipped (already tagged, filtered out, etc.).</summary>
        public int SkippedCount { get; set; }

        /// <summary>Clusters detected and handled.</summary>
        public int ClustersDetected { get; set; }

        /// <summary>Collisions resolved during placement.</summary>
        public int CollisionsResolved { get; set; }

        /// <summary>Tags flagged for manual review.</summary>
        public int ManualReviewCount { get; set; }

        /// <summary>Views processed.</summary>
        public int ViewsProcessed { get; set; }

        /// <summary>Total time for the batch operation.</summary>
        public TimeSpan Duration { get; set; }

        /// <summary>Overall placement quality score (0-100).</summary>
        public double QualityScore { get; set; }

        /// <summary>Quality report if quality check was enabled.</summary>
        public TagQualityReport QualityReport { get; set; }
    }

    /// <summary>
    /// Progress information during placement operations.
    /// </summary>
    public class PlacementProgress
    {
        public int CurrentElement { get; set; }
        public int TotalElements { get; set; }
        public int CurrentView { get; set; }
        public int TotalViews { get; set; }
        public string CurrentOperation { get; set; }
        public double PercentComplete => TotalElements > 0
            ? (double)CurrentElement / TotalElements * 100.0
            : 0.0;
    }

    #endregion

    #region Collision Detection DTOs

    /// <summary>
    /// Result of a collision detection query.
    /// </summary>
    public class TagCollision
    {
        /// <summary>The tag being tested.</summary>
        public string TagId { get; set; }

        /// <summary>The conflicting annotation's identifier.</summary>
        public string ConflictId { get; set; }

        /// <summary>Type of the conflicting annotation (Tag, Dimension, TextNote, DetailItem).</summary>
        public string ConflictType { get; set; }

        /// <summary>Overlap area in square model units.</summary>
        public double OverlapArea { get; set; }

        /// <summary>Overlap percentage relative to the tag's area.</summary>
        public double OverlapPercentage { get; set; }

        /// <summary>Minimum displacement vector to resolve the collision.</summary>
        public Vector2D SeparationVector { get; set; }

        /// <summary>Severity of the collision.</summary>
        public IssueSeverity Severity { get; set; }
    }

    #endregion

    #region Cluster Detection DTOs

    /// <summary>
    /// A detected cluster of similar elements.
    /// </summary>
    public class ElementCluster
    {
        /// <summary>Unique identifier for this cluster.</summary>
        public string ClusterId { get; set; }

        /// <summary>Element IDs in this cluster.</summary>
        public List<int> ElementIds { get; set; } = new List<int>();

        /// <summary>Category of elements in the cluster.</summary>
        public string CategoryName { get; set; }

        /// <summary>Family/type shared by cluster members.</summary>
        public string SharedFamilyType { get; set; }

        /// <summary>Type of geometric pattern detected.</summary>
        public ClusterType Type { get; set; }

        /// <summary>Recommended tagging strategy for this cluster.</summary>
        public ClusterTagStrategy RecommendedStrategy { get; set; }

        /// <summary>The representative element ID for typical tagging.</summary>
        public int RepresentativeElementId { get; set; }

        /// <summary>Number of elements in the cluster.</summary>
        public int Count => ElementIds.Count;

        /// <summary>Center point of the cluster.</summary>
        public Point2D ClusterCenter { get; set; }

        /// <summary>Spacing between elements (for regular patterns).</summary>
        public double AverageSpacing { get; set; }

        /// <summary>Direction vector of the pattern (for linear clusters).</summary>
        public Vector2D PatternDirection { get; set; }
    }

    #endregion

    #region Quality Assurance DTOs

    /// <summary>
    /// A quality issue detected by the TagQualityAnalyzer.
    /// </summary>
    public class TagQualityIssue
    {
        /// <summary>Unique identifier for this issue.</summary>
        public string IssueId { get; set; }

        /// <summary>Type of quality issue.</summary>
        public QualityIssueType IssueType { get; set; }

        /// <summary>Severity of the issue.</summary>
        public IssueSeverity Severity { get; set; }

        /// <summary>TagId of the affected tag.</summary>
        public string AffectedTagId { get; set; }

        /// <summary>Revit ElementId of the affected tag.</summary>
        public int AffectedElementId { get; set; }

        /// <summary>View ID where the issue occurs.</summary>
        public int ViewId { get; set; }

        /// <summary>Human-readable description of the issue.</summary>
        public string Description { get; set; }

        /// <summary>Whether this issue can be automatically fixed.</summary>
        public bool IsAutoFixable { get; set; }

        /// <summary>Suggested fix action description.</summary>
        public string SuggestedFix { get; set; }

        /// <summary>Whether this issue was dismissed by the user.</summary>
        public bool IsDismissed { get; set; }

        /// <summary>Location of the issue in view coordinates.</summary>
        public Point2D Location { get; set; }

        /// <summary>When the issue was first detected.</summary>
        public DateTime DetectedAt { get; set; }
    }

    /// <summary>
    /// Comprehensive quality report for tagged views.
    /// </summary>
    public class TagQualityReport
    {
        /// <summary>All issues found.</summary>
        public List<TagQualityIssue> Issues { get; set; } = new List<TagQualityIssue>();

        /// <summary>Overall quality score from 0 (worst) to 100 (perfect).</summary>
        public double QualityScore { get; set; }

        /// <summary>Total tags analyzed.</summary>
        public int TotalTagsAnalyzed { get; set; }

        /// <summary>Total views analyzed.</summary>
        public int TotalViewsAnalyzed { get; set; }

        /// <summary>Issue counts by type.</summary>
        public Dictionary<QualityIssueType, int> IssueCountsByType { get; set; } =
            new Dictionary<QualityIssueType, int>();

        /// <summary>Issue counts by severity.</summary>
        public Dictionary<IssueSeverity, int> IssueCountsBySeverity { get; set; } =
            new Dictionary<IssueSeverity, int>();

        /// <summary>Per-view quality scores.</summary>
        public Dictionary<int, double> ViewScores { get; set; } = new Dictionary<int, double>();

        /// <summary>Number of issues automatically fixable.</summary>
        public int AutoFixableCount { get; set; }

        /// <summary>When the report was generated.</summary>
        public DateTime GeneratedAt { get; set; }

        /// <summary>Duration of the quality analysis.</summary>
        public TimeSpan AnalysisDuration { get; set; }
    }

    /// <summary>
    /// Result of applying auto-fixes to quality issues.
    /// </summary>
    public class AutoFixResult
    {
        /// <summary>Issues that were successfully fixed.</summary>
        public List<string> FixedIssueIds { get; set; } = new List<string>();

        /// <summary>Issues that could not be fixed.</summary>
        public List<string> FailedIssueIds { get; set; } = new List<string>();

        /// <summary>Tags deleted during fixing.</summary>
        public int TagsDeleted { get; set; }

        /// <summary>Tags repositioned during fixing.</summary>
        public int TagsRepositioned { get; set; }

        /// <summary>Tags refreshed (content updated) during fixing.</summary>
        public int TagsRefreshed { get; set; }
    }

    #endregion

    #region Rule Engine DTOs

    /// <summary>
    /// A tagging rule that determines whether and how elements get tagged.
    /// Surpasses Ideate tag rules with richer conditions and priority system.
    /// </summary>
    public class TagRule
    {
        /// <summary>Unique identifier for this rule.</summary>
        public string RuleId { get; set; }

        /// <summary>Human-readable name.</summary>
        public string Name { get; set; }

        /// <summary>Description of what this rule does.</summary>
        public string Description { get; set; }

        /// <summary>Revit category filter (e.g., "Doors", "Walls"). Null matches all.</summary>
        public string CategoryFilter { get; set; }

        /// <summary>Family name filter (supports wildcards). Null matches all.</summary>
        public string FamilyFilter { get; set; }

        /// <summary>Type name filter (supports wildcards). Null matches all.</summary>
        public string TypeFilter { get; set; }

        /// <summary>Parameter conditions that must be met.</summary>
        public List<RuleCondition> Conditions { get; set; } = new List<RuleCondition>();

        /// <summary>How conditions combine (And/Or).</summary>
        public RuleLogic ConditionLogic { get; set; } = RuleLogic.And;

        /// <summary>Name of the tag template to use when this rule matches.</summary>
        public string TemplateName { get; set; }

        /// <summary>Priority (lower number = higher priority). Used for conflict resolution.</summary>
        public int Priority { get; set; } = 100;

        /// <summary>Whether this rule is currently enabled.</summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>View types this rule applies to. Null means all view types.</summary>
        public List<TagViewType> ApplicableViewTypes { get; set; }

        /// <summary>Rule group this belongs to (if any).</summary>
        public string GroupName { get; set; }

        /// <summary>Whether to include elements from linked files.</summary>
        public bool IncludeLinkedFiles { get; set; }

        /// <summary>Phase filter for the rule.</summary>
        public string PhaseFilter { get; set; }
    }

    /// <summary>
    /// A parameter-based condition within a tagging rule.
    /// </summary>
    public class RuleCondition
    {
        /// <summary>Name of the parameter to evaluate.</summary>
        public string ParameterName { get; set; }

        /// <summary>Comparison operator.</summary>
        public RuleOperator Operator { get; set; }

        /// <summary>Value to compare against.</summary>
        public string Value { get; set; }

        /// <summary>Whether this is a type parameter (vs. instance parameter).</summary>
        public bool IsTypeParameter { get; set; }
    }

    /// <summary>
    /// A named group of rules that can be activated as a unit.
    /// Surpasses Naviate "Tag Settings with Groups".
    /// </summary>
    public class RuleGroup
    {
        /// <summary>Group name.</summary>
        public string Name { get; set; }

        /// <summary>Description of what this group covers.</summary>
        public string Description { get; set; }

        /// <summary>Rule IDs in this group, in priority order.</summary>
        public List<string> RuleIds { get; set; } = new List<string>();

        /// <summary>Parent group name for inheritance (if any).</summary>
        public string InheritsFrom { get; set; }

        /// <summary>Whether this group is currently active.</summary>
        public bool IsActive { get; set; }
    }

    #endregion

    #region Tag Template DTOs

    /// <summary>
    /// Reusable tag template defining how tags are placed and styled.
    /// Surpasses Naviate "Tag from Template" with richer configuration.
    /// </summary>
    public class TagTemplateDefinition
    {
        /// <summary>Unique template name.</summary>
        public string Name { get; set; }

        /// <summary>Description of this template.</summary>
        public string Description { get; set; }

        /// <summary>Revit category this template applies to.</summary>
        public string CategoryName { get; set; }

        /// <summary>View types this template is designed for.</summary>
        public List<TagViewType> ViewTypes { get; set; } = new List<TagViewType>();

        /// <summary>Revit tag family name to use.</summary>
        public string TagFamilyName { get; set; }

        /// <summary>Revit tag type name within the family.</summary>
        public string TagTypeName { get; set; }

        /// <summary>Ordered list of preferred positions (first = highest priority).
        /// Surpasses Naviate NV Priority system with unlimited priority levels.</summary>
        public List<TagPosition> PreferredPositions { get; set; } = new List<TagPosition>();

        /// <summary>Leader line configuration.</summary>
        public LeaderType LeaderType { get; set; } = LeaderType.Auto;

        /// <summary>Minimum leader length in model units.</summary>
        public double MinLeaderLength { get; set; }

        /// <summary>Maximum leader length in model units.</summary>
        public double MaxLeaderLength { get; set; } = 0.05; // ~50mm

        /// <summary>Distance threshold beyond which a leader is applied
        /// (surpasses Naviate NVVectorMoveTolerance).</summary>
        public double LeaderDistanceThreshold { get; set; } = 0.01;

        /// <summary>Tag text orientation.</summary>
        public TagOrientation Orientation { get; set; } = TagOrientation.Auto;

        /// <summary>Whether tag rotation follows the host element
        /// (surpasses Naviate NVTranslation).</summary>
        public bool FollowElementRotation { get; set; }

        /// <summary>Content expression for tag text. Uses parameter references like {DoorNumber}.
        /// Supports conditionals, formatting, formulas.</summary>
        public string ContentExpression { get; set; }

        /// <summary>X offset from the preferred position in model units.</summary>
        public double OffsetX { get; set; }

        /// <summary>Y offset from the preferred position in model units.</summary>
        public double OffsetY { get; set; }

        /// <summary>Whether multiple tags for the same element can stack.</summary>
        public bool AllowStacking { get; set; }

        /// <summary>Whether tag can overlap its host element.</summary>
        public bool AllowHostOverlap { get; set; }

        /// <summary>Preferred alignment mode for this template.</summary>
        public AlignmentMode Alignment { get; set; } = AlignmentMode.Relaxed;

        /// <summary>Fallback chain: ordered collision actions to try.</summary>
        public List<CollisionAction> FallbackChain { get; set; } = new List<CollisionAction>
        {
            CollisionAction.Reposition,
            CollisionAction.Nudge,
            CollisionAction.LeaderReroute,
            CollisionAction.Stack,
            CollisionAction.FlagManual
        };

        /// <summary>Parent template name for inheritance.</summary>
        public string InheritsFrom { get; set; }
    }

    #endregion

    #region View Coordination DTOs

    /// <summary>
    /// Context information for tagging within a specific view.
    /// </summary>
    public class ViewTagContext
    {
        /// <summary>Revit view ElementId.</summary>
        public int ViewId { get; set; }

        /// <summary>View name.</summary>
        public string ViewName { get; set; }

        /// <summary>Classified view type.</summary>
        public TagViewType ViewType { get; set; }

        /// <summary>View scale (e.g., 100 for 1:100).</summary>
        public double Scale { get; set; }

        /// <summary>View crop region bounds.</summary>
        public TagBounds2D CropRegion { get; set; }

        /// <summary>All existing annotation bounds in this view.</summary>
        public List<TagBounds2D> ExistingAnnotationBounds { get; set; } = new List<TagBounds2D>();

        /// <summary>Sheet ID if this view is placed on a sheet (0 if not).</summary>
        public int SheetId { get; set; }

        /// <summary>Viewport bounds on the sheet (if placed).</summary>
        public TagBounds2D ViewportBounds { get; set; }
    }

    /// <summary>
    /// Tag inventory summary for the project or a subset of views.
    /// Surpasses Ideate's annotation browsing with richer coverage metrics.
    /// </summary>
    public class TagInventory
    {
        /// <summary>Total managed tags in scope.</summary>
        public int TotalTags { get; set; }

        /// <summary>Tag counts by category.</summary>
        public Dictionary<string, int> TagsByCategory { get; set; } = new Dictionary<string, int>();

        /// <summary>Tag counts by view.</summary>
        public Dictionary<int, int> TagsByView { get; set; } = new Dictionary<int, int>();

        /// <summary>Tag counts by state.</summary>
        public Dictionary<TagState, int> TagsByState { get; set; } = new Dictionary<TagState, int>();

        /// <summary>Number of taggable elements that are not yet tagged.</summary>
        public int UntaggedElements { get; set; }

        /// <summary>Coverage percentage (tagged / total taggable).</summary>
        public double CoveragePercentage { get; set; }

        /// <summary>Categories with zero tags.</summary>
        public List<string> UntaggedCategories { get; set; } = new List<string>();
    }

    #endregion

    #region Intelligence & Learning DTOs

    /// <summary>
    /// Record of a user correction to an automated tag placement.
    /// Used by TagIntelligenceEngine for learning.
    /// </summary>
    public class PlacementCorrection
    {
        /// <summary>Tag that was corrected.</summary>
        public string TagId { get; set; }

        /// <summary>Category of the tagged element.</summary>
        public string CategoryName { get; set; }

        /// <summary>View type where the correction occurred.</summary>
        public TagViewType ViewType { get; set; }

        /// <summary>Original automated position.</summary>
        public TagPlacement OriginalPlacement { get; set; }

        /// <summary>User's corrected position.</summary>
        public TagPlacement CorrectedPlacement { get; set; }

        /// <summary>When the correction was made.</summary>
        public DateTime CorrectedAt { get; set; }

        /// <summary>View scale at the time of correction.</summary>
        public double ViewScale { get; set; }
    }

    /// <summary>
    /// Learned placement pattern from user corrections.
    /// </summary>
    public class PlacementPattern
    {
        /// <summary>Pattern identifier.</summary>
        public string PatternId { get; set; }

        /// <summary>Category this pattern applies to.</summary>
        public string CategoryName { get; set; }

        /// <summary>View type this pattern applies to.</summary>
        public TagViewType ViewType { get; set; }

        /// <summary>Learned preferred position.</summary>
        public TagPosition PreferredPosition { get; set; }

        /// <summary>Learned preferred offsets.</summary>
        public double PreferredOffsetX { get; set; }
        public double PreferredOffsetY { get; set; }

        /// <summary>Learned preferred leader type.</summary>
        public LeaderType PreferredLeaderType { get; set; }

        /// <summary>Confidence level (0.0 to 1.0) based on number of observations.</summary>
        public double Confidence { get; set; }

        /// <summary>Number of observations this pattern is based on.</summary>
        public int ObservationCount { get; set; }

        /// <summary>When this pattern was last reinforced.</summary>
        public DateTime LastReinforced { get; set; }
    }

    /// <summary>
    /// Prediction result from the intelligence engine.
    /// </summary>
    public class PlacementPrediction
    {
        /// <summary>Predicted optimal position.</summary>
        public TagPlacement PredictedPlacement { get; set; }

        /// <summary>Confidence in the prediction (0.0 to 1.0).</summary>
        public double Confidence { get; set; }

        /// <summary>Pattern that generated this prediction.</summary>
        public string PatternId { get; set; }

        /// <summary>Whether the prediction was based on sufficient observations.</summary>
        public bool IsReliable { get; set; }
    }

    #endregion

    #region Tag Operation History

    /// <summary>
    /// Record of a tag operation for undo/redo support.
    /// </summary>
    public class TagOperation
    {
        /// <summary>Operation identifier.</summary>
        public string OperationId { get; set; }

        /// <summary>Type of operation.</summary>
        public TagOperationType Type { get; set; }

        /// <summary>Tag ID affected.</summary>
        public string TagId { get; set; }

        /// <summary>State before the operation (for undo).</summary>
        public TagInstance PreviousState { get; set; }

        /// <summary>State after the operation.</summary>
        public TagInstance NewState { get; set; }

        /// <summary>When the operation occurred.</summary>
        public DateTime Timestamp { get; set; }

        /// <summary>Source of the operation.</summary>
        public TagCreationSource Source { get; set; }
    }

    #endregion
}
