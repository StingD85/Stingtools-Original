// StingBIM.AI.Tagging - SuperIntelligent Tagging System
// ITagPlacementStrategy.cs - Strategy pattern interface for pluggable placement algorithms

using System.Collections.Generic;

namespace StingBIM.AI.Tagging.Models
{
    /// <summary>
    /// Strategy interface for pluggable tag placement algorithms.
    /// Implementations provide different approaches to position scoring and candidate generation.
    /// The default implementation in TagPlacementEngine uses a priority-weighted constraint solver.
    /// Custom strategies can be registered for specialized placement behaviors.
    /// </summary>
    public interface ITagPlacementStrategy
    {
        /// <summary>
        /// Gets the unique name of this placement strategy.
        /// </summary>
        string StrategyName { get; }

        /// <summary>
        /// Generates candidate positions for a tag around its host element.
        /// </summary>
        /// <param name="tag">The tag instance being placed.</param>
        /// <param name="hostElementBounds">Bounding box of the host element in view coordinates.</param>
        /// <param name="template">The tag template governing placement preferences.</param>
        /// <param name="context">View context with existing annotations and constraints.</param>
        /// <returns>Ordered list of candidate positions to evaluate.</returns>
        List<PlacementCandidate> GetCandidatePositions(
            TagInstance tag,
            TagBounds2D hostElementBounds,
            TagTemplateDefinition template,
            ViewTagContext context);

        /// <summary>
        /// Scores a candidate position considering all placement factors.
        /// </summary>
        /// <param name="candidate">The candidate to score.</param>
        /// <param name="tag">The tag being placed.</param>
        /// <param name="hostElementBounds">Bounding box of the host element.</param>
        /// <param name="existingTags">Already-placed tags in the view.</param>
        /// <param name="context">View context with constraints.</param>
        /// <returns>Score breakdown with composite total (higher is better).</returns>
        PlacementScoreBreakdown ScorePosition(
            PlacementCandidate candidate,
            TagInstance tag,
            TagBounds2D hostElementBounds,
            List<TagInstance> existingTags,
            ViewTagContext context);

        /// <summary>
        /// Post-processes a set of placed tags to improve global alignment and spacing.
        /// Called after all individual placements are resolved.
        /// </summary>
        /// <param name="placedTags">All tags placed in this batch.</param>
        /// <param name="context">View context.</param>
        /// <returns>Adjusted tag placements.</returns>
        List<TagInstance> OptimizeGlobalLayout(
            List<TagInstance> placedTags,
            ViewTagContext context);
    }
}
