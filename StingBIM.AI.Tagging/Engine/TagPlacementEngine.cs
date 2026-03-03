// StingBIM.AI.Tagging - SuperIntelligent Tagging System
// TagPlacementEngine.cs - Central orchestrator for AI-powered optimal tag placement
// Surpasses BIMLOGIQ Smart Annotation, Naviate Tag Settings, and Ideate Batch Tagging
// Copyright (c) 2026 StingBIM. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using StingBIM.AI.Tagging.Data;
using StingBIM.AI.Tagging.Intelligence;
using StingBIM.AI.Tagging.Models;
using StingBIM.AI.Tagging.Quality;
using StingBIM.AI.Tagging.Rules;

namespace StingBIM.AI.Tagging.Engine
{
    /// <summary>
    /// Central orchestrator of the SuperIntelligent Tagging System. Integrates rule evaluation,
    /// template-driven placement, collision detection and resolution, element clustering,
    /// machine-learned user preferences, and global alignment optimization into a unified
    /// AI-powered tag placement algorithm.
    ///
    /// <para>
    /// <strong>Placement Pipeline:</strong>
    /// <list type="number">
    ///   <item>Accept <see cref="TagPlacementRequest"/> with elements, views, strategy, and options.</item>
    ///   <item>For each view, collect taggable elements filtered by rule engine.</item>
    ///   <item>Detect element clusters for grouped tagging (typical, count, range).</item>
    ///   <item>For each element: evaluate rules, generate up to 24 candidate positions, score with
    ///         7-component weighted objective, select best, resolve collisions via fallback chain.</item>
    ///   <item>Run global alignment optimization across all placed tags.</item>
    ///   <item>Optionally run quality analysis for placement assurance.</item>
    ///   <item>Return <see cref="BatchPlacementResult"/> with full diagnostics.</item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// <strong>Surpasses:</strong>
    /// <list type="bullet">
    ///   <item>BIMLOGIQ Smart Annotation: cloud AI replaced with offline intelligence engine,
    ///         7-component scoring vs. single heuristic, 24 candidates vs. 4 positions.</item>
    ///   <item>Naviate Tag Settings: 9+1 positions expanded to 24 candidates with near/far/leader
    ///         variants, priority-only ranking replaced with weighted multi-objective optimization.</item>
    ///   <item>Ideate BIMLink: batch-only tagging extended with real-time single-tag placement,
    ///         no learning replaced with adaptive user preference learning.</item>
    /// </list>
    /// </para>
    /// </summary>
    public class TagPlacementEngine
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        #region Fields

        private readonly TagRuleEngine _ruleEngine;
        private readonly CollisionResolver _collisionResolver;
        private readonly ClusterDetector _clusterDetector;
        private readonly TagContentGenerator _contentGenerator;
        private readonly TagIntelligenceEngine _intelligenceEngine;
        private readonly TagLifecycleManager _lifecycleManager;
        private readonly TagRepository _repository;
        private readonly TagConfiguration _configuration;
        private readonly object _lockObject = new object();

        /// <summary>Cached view contexts for the current placement session.</summary>
        private readonly Dictionary<int, ViewTagContext> _viewContextCache;

        /// <summary>Tags placed during the current batch session, used for inter-tag collision tracking.</summary>
        private readonly List<TagInstance> _sessionPlacedTags;

        /// <summary>Alignment rails discovered per view during the current session.</summary>
        private readonly Dictionary<int, List<AlignmentRail>> _viewAlignmentRails;

        // Candidate generation multipliers controlling near/far/leader offset distances
        private const double NearOffsetMultiplier = 1.0;
        private const double FarOffsetMultiplier = 2.0;
        private const double LeaderOffsetMultiplier = 3.5;

        // Alignment optimization thresholds
        private const double AlignmentRailSnapDistance = 0.005;
        private const double AlignmentRailMergeDistance = 0.003;
        private const int MaxAlignmentIterations = 5;

        // Scoring thresholds
        private const double MinimumAcceptableScore = 0.05;
        private const double CollisionPenaltyExponent = 2.0;
        private const double CropRegionEdgeMargin = 0.01;
        private const double ReadabilityHorizontalBonus = 0.15;
        private const double ReadabilityClearBackgroundBonus = 0.10;
        private const double ReadabilityVerticalPenalty = 0.30;
        private const double ReadabilityEdgeProximityPenalty = 0.25;

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes a new instance of the <see cref="TagPlacementEngine"/> with all required subsystems.
        /// </summary>
        /// <param name="ruleEngine">Evaluates tag rules against elements to determine taggability and template selection.</param>
        /// <param name="collisionResolver">Detects and resolves spatial collisions between tags and annotations.</param>
        /// <param name="clusterDetector">Identifies clusters of similar elements for grouped tagging strategies.</param>
        /// <param name="contentGenerator">Generates tag display text from content expressions and element parameters.</param>
        /// <param name="intelligenceEngine">Provides machine-learned placement predictions from user correction history.</param>
        /// <param name="lifecycleManager">Creates, moves, and deletes Revit tag elements via the API.</param>
        /// <param name="repository">Persistent storage for tags, rules, templates, and learning data.</param>
        /// <param name="configuration">Tagging system configuration with score weights and thresholds.</param>
        public TagPlacementEngine(
            TagRuleEngine ruleEngine,
            CollisionResolver collisionResolver,
            ClusterDetector clusterDetector,
            TagContentGenerator contentGenerator,
            TagIntelligenceEngine intelligenceEngine,
            TagLifecycleManager lifecycleManager,
            TagRepository repository,
            TagConfiguration configuration)
        {
            _ruleEngine = ruleEngine ?? throw new ArgumentNullException(nameof(ruleEngine));
            _collisionResolver = collisionResolver ?? throw new ArgumentNullException(nameof(collisionResolver));
            _clusterDetector = clusterDetector ?? throw new ArgumentNullException(nameof(clusterDetector));
            _contentGenerator = contentGenerator ?? throw new ArgumentNullException(nameof(contentGenerator));
            _intelligenceEngine = intelligenceEngine ?? throw new ArgumentNullException(nameof(intelligenceEngine));
            _lifecycleManager = lifecycleManager ?? throw new ArgumentNullException(nameof(lifecycleManager));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            _viewContextCache = new Dictionary<int, ViewTagContext>();
            _sessionPlacedTags = new List<TagInstance>();
            _viewAlignmentRails = new Dictionary<int, List<AlignmentRail>>();

            Logger.Info("TagPlacementEngine initialized with all subsystems");
        }

        #endregion

        #region Placement Pipeline

        /// <summary>
        /// Overload that accepts an explicit view context for single-view placement coordination.
        /// The view context is used for cross-view consistency but the request drives the actual placement.
        /// </summary>
        public async Task<BatchPlacementResult> PlaceTagsAsync(
            TagPlacementRequest request,
            ViewTagContext viewContext,
            IProgress<PlacementProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            // Ensure the request targets the provided view context's view
            if (viewContext != null && request.ViewIds != null && !request.ViewIds.Contains(viewContext.ViewId))
            {
                request.ViewIds.Add(viewContext.ViewId);
            }
            return await PlaceTagsAsync(request, progress, cancellationToken);
        }

        /// <summary>
        /// Executes the full AI-powered tag placement pipeline for a batch of elements across one
        /// or more views. This is the primary entry point for automated tagging operations.
        /// </summary>
        /// <param name="request">Specifies elements, views, strategy, and options for the placement batch.</param>
        /// <param name="progress">Optional progress reporter for UI feedback.</param>
        /// <param name="cancellationToken">Token to cancel long-running placement operations.</param>
        /// <returns>A <see cref="BatchPlacementResult"/> containing per-tag results and aggregate statistics.</returns>
        public async Task<BatchPlacementResult> PlaceTagsAsync(
            TagPlacementRequest request,
            IProgress<PlacementProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.ViewIds == null || request.ViewIds.Count == 0)
                throw new ArgumentException("At least one view ID must be specified.", nameof(request));

            var stopwatch = Stopwatch.StartNew();
            var batchResult = new BatchPlacementResult();
            var session = _repository.StartSession("Batch placement: " + request.Strategy);

            Logger.Info("Starting batch placement: {0} views, strategy={1}, elements={2}",
                request.ViewIds.Count, request.Strategy,
                request.ElementIds.Count > 0 ? request.ElementIds.Count.ToString() : "all");

            try
            {
                lock (_lockObject)
                {
                    _viewContextCache.Clear();
                    _sessionPlacedTags.Clear();
                    _viewAlignmentRails.Clear();
                }

                int totalElementsProcessed = 0;
                int totalElementsEstimate = 0;

                // First pass: estimate total elements for progress reporting
                foreach (int viewId in request.ViewIds)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var context = GetOrCreateViewContext(viewId);
                    var elementsInView = GetElementsForView(request, context);
                    totalElementsEstimate += elementsInView.Count;
                }

                batchResult.TotalElements = totalElementsEstimate;

                // Second pass: process each view
                for (int viewIndex = 0; viewIndex < request.ViewIds.Count; viewIndex++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int viewId = request.ViewIds[viewIndex];
                    var viewContext = GetOrCreateViewContext(viewId);

                    Logger.Debug("Processing view {0} ({1}/{2}): {3}",
                        viewId, viewIndex + 1, request.ViewIds.Count, viewContext.ViewName);

                    var viewResults = await ProcessViewAsync(
                        request, viewContext, totalElementsProcessed, totalElementsEstimate,
                        progress, cancellationToken);

                    foreach (var result in viewResults)
                    {
                        batchResult.Results.Add(result);
                        if (result.Success)
                            batchResult.SuccessCount++;
                        else if (result.FailureReason == "skipped")
                            batchResult.SkippedCount++;
                        else
                            batchResult.FailureCount++;

                        batchResult.CollisionsResolved += result.CollisionActionsApplied.Count;

                        if (result.CollisionActionsApplied.Contains(CollisionAction.FlagManual))
                            batchResult.ManualReviewCount++;
                    }

                    totalElementsProcessed += viewResults.Count;
                    batchResult.ViewsProcessed++;

                    // Run global alignment optimization for this view
                    List<TagInstance> viewTags;
                    lock (_lockObject)
                    {
                        viewTags = _sessionPlacedTags
                            .Where(t => t.ViewId == viewId)
                            .ToList();
                    }

                    if (viewTags.Count > 1 && request.Alignment != AlignmentMode.None)
                    {
                        var optimizedTags = OptimizeGlobalAlignment(viewTags, viewContext);
                        lock (_lockObject)
                        {
                            foreach (var optimized in optimizedTags)
                            {
                                int idx = _sessionPlacedTags.FindIndex(t => t.TagId == optimized.TagId);
                                if (idx >= 0)
                                    _sessionPlacedTags[idx] = optimized;
                            }
                        }
                    }
                }

                // Compute aggregate quality score
                if (request.RunQualityCheck)
                {
                    List<TagInstance> allPlaced;
                    lock (_lockObject)
                    {
                        allPlaced = _sessionPlacedTags.ToList();
                    }
                    batchResult.QualityReport = ComputeInlineQualityReport(allPlaced, request.ViewIds);
                    batchResult.QualityScore = batchResult.QualityReport.QualityScore;
                }
                else
                {
                    batchResult.QualityScore = batchResult.TotalElements > 0
                        ? (double)batchResult.SuccessCount / batchResult.TotalElements * 100.0
                        : 0.0;
                }

                stopwatch.Stop();
                batchResult.Duration = stopwatch.Elapsed;

                _repository.EndSession(session.SessionId, batchResult);

                Logger.Info("Batch placement complete: {0} placed, {1} failed, {2} skipped, " +
                            "{3} collisions resolved, quality={4:F1}%, duration={5}ms",
                    batchResult.SuccessCount, batchResult.FailureCount, batchResult.SkippedCount,
                    batchResult.CollisionsResolved, batchResult.QualityScore,
                    stopwatch.ElapsedMilliseconds);
            }
            catch (OperationCanceledException)
            {
                Logger.Warn("Batch placement cancelled by user");
                batchResult.Duration = stopwatch.Elapsed;
                _repository.EndSession(session.SessionId, batchResult);
                throw;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Batch placement failed with unhandled exception");
                batchResult.Duration = stopwatch.Elapsed;
                _repository.EndSession(session.SessionId, batchResult);
                throw;
            }

            return batchResult;
        }

        /// <summary>
        /// Processes all taggable elements within a single view, including cluster detection.
        /// </summary>
        private async Task<List<TagPlacementResult>> ProcessViewAsync(
            TagPlacementRequest request,
            ViewTagContext viewContext,
            int processedSoFar,
            int totalElements,
            IProgress<PlacementProgress> progress,
            CancellationToken cancellationToken)
        {
            var results = new List<TagPlacementResult>();
            var elementsToTag = GetElementsForView(request, viewContext);
            var existingTagsInView = _repository.GetTagsByView(viewContext.ViewId);

            // Remove existing tags if replacement is requested
            if (request.ReplaceExisting)
            {
                foreach (var existing in existingTagsInView)
                {
                    _lifecycleManager.DeleteTag(existing.TagId);
                    _repository.RemoveTag(existing.TagId);
                }
                existingTagsInView.Clear();
            }

            // Filter out already-tagged elements unless replacing
            if (!request.ReplaceExisting)
            {
                var alreadyTaggedIds = new HashSet<int>(
                    existingTagsInView.Select(t => t.HostElementId));
                elementsToTag = elementsToTag
                    .Where(id => !alreadyTaggedIds.Contains(id))
                    .ToList();
            }

            // Detect clusters if enabled
            var clusters = new List<ElementCluster>();
            var clusteredElementIds = new HashSet<int>();

            if (request.EnableClusterDetection && elementsToTag.Count >= _configuration.Settings.ClusterMinPoints)
            {
                clusters = _clusterDetector.DetectClusters(
                    elementsToTag, viewContext,
                    _configuration.Settings.ClusterEpsilon,
                    _configuration.Settings.ClusterMinPoints);

                foreach (var cluster in clusters)
                {
                    foreach (int eid in cluster.ElementIds)
                        clusteredElementIds.Add(eid);
                }

                Logger.Debug("View {0}: detected {1} clusters covering {2} elements",
                    viewContext.ViewId, clusters.Count, clusteredElementIds.Count);
            }

            // Process clusters first
            foreach (var cluster in clusters)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var clusterResults = await ProcessClusterAsync(
                    cluster, request, viewContext, existingTagsInView, cancellationToken);
                results.AddRange(clusterResults);

                ReportProgress(progress, processedSoFar + results.Count, totalElements,
                    viewContext.ViewId, request.ViewIds.Count, "Processing cluster: " + cluster.ClusterId);
            }

            // Process non-clustered elements individually
            var nonClusteredElements = elementsToTag
                .Where(id => !clusteredElementIds.Contains(id))
                .ToList();

            for (int i = 0; i < nonClusteredElements.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int elementId = nonClusteredElements[i];
                var elementResult = await ProcessElementAsync(
                    elementId, request, viewContext, existingTagsInView, cancellationToken);
                results.Add(elementResult);

                if (elementResult.Success && elementResult.Tag != null)
                {
                    existingTagsInView.Add(elementResult.Tag);
                    lock (_lockObject)
                    {
                        _sessionPlacedTags.Add(elementResult.Tag);
                    }
                    _repository.AddTag(elementResult.Tag);
                }

                ReportProgress(progress, processedSoFar + results.Count, totalElements,
                    viewContext.ViewId, request.ViewIds.Count,
                    "Tagging element " + elementId);
            }

            return results;
        }

        /// <summary>
        /// Processes a single element through the full placement pipeline: rule evaluation,
        /// candidate generation, scoring, selection, and collision resolution.
        /// </summary>
        private async Task<TagPlacementResult> ProcessElementAsync(
            int elementId,
            TagPlacementRequest request,
            ViewTagContext viewContext,
            List<TagInstance> existingTagsInView,
            CancellationToken cancellationToken)
        {
            var result = new TagPlacementResult();

            try
            {
                // Step 1: Build element context with rule and template
                var elementContext = BuildElementContext(elementId, request, viewContext);
                if (elementContext == null)
                {
                    result.Success = false;
                    result.FailureReason = "skipped";
                    return result;
                }

                // Step 2: Generate candidate positions
                var candidates = GenerateCandidatePositions(
                    elementContext.HostBounds,
                    elementContext.EstimatedTagBounds,
                    elementContext.Template,
                    viewContext);

                if (candidates.Count == 0)
                {
                    result.Success = false;
                    result.FailureReason = "No valid candidate positions generated";
                    Logger.Warn("Element {0}: no candidate positions available in view {1}",
                        elementId, viewContext.ViewId);
                    return result;
                }

                // Step 3: Build a preliminary tag instance for scoring
                var tagInstance = BuildTagInstance(elementContext, viewContext, candidates[0].Position);

                // Step 4: Score all candidates
                foreach (var candidate in candidates)
                {
                    candidate.ScoreBreakdown = ScoreCandidate(
                        candidate, tagInstance, elementContext.HostBounds,
                        existingTagsInView, viewContext);
                    candidate.Score = candidate.ScoreBreakdown.TotalScore;
                }

                // Step 5: Rank candidates by composite score (descending)
                candidates.Sort((a, b) => b.Score.CompareTo(a.Score));
                result.EvaluatedCandidates = candidates;

                // Step 6: Try the best candidate, with fallback chain for collisions
                PlacementCandidate selectedCandidate = null;
                int fallbackLevel = 0;

                foreach (var candidate in candidates)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (candidate.Score < MinimumAcceptableScore)
                        break;

                    var tagBoundsAtPosition = ComputeTagBoundsAtPosition(
                        candidate.Position, elementContext.EstimatedTagBounds);

                    var collisions = _collisionResolver.DetectCollisions(
                        tagBoundsAtPosition, viewContext, existingTagsInView,
                        request.CollisionClearance);

                    candidate.HasCollision = collisions.Count > 0;
                    candidate.CollisionCount = collisions.Count;

                    if (!candidate.HasCollision)
                    {
                        selectedCandidate = candidate;
                        break;
                    }

                    // Attempt collision resolution via the template fallback chain
                    var resolvedCandidate = ResolveCollisionsWithFallback(
                        candidate, tagInstance, elementContext, viewContext,
                        existingTagsInView, collisions, request.CollisionClearance,
                        ref fallbackLevel);

                    if (resolvedCandidate != null)
                    {
                        selectedCandidate = resolvedCandidate;
                        result.CollisionActionsApplied.AddRange(
                            elementContext.Template.FallbackChain.Take(fallbackLevel + 1));
                        break;
                    }
                }

                // Step 7: If no candidate survived, flag for manual review
                if (selectedCandidate == null)
                {
                    selectedCandidate = candidates[0]; // Use the highest-scored even with collision
                    result.CollisionActionsApplied.Add(CollisionAction.FlagManual);
                    fallbackLevel = _configuration.Settings.MaxFallbackLevels;
                    tagInstance.State = TagState.PendingReview;
                    Logger.Warn("Element {0}: all candidates have collisions, flagged for manual review",
                        elementId);
                }

                result.SelectedCandidate = selectedCandidate;
                result.FallbackLevel = fallbackLevel;

                // Step 8: Finalize tag instance with selected position
                tagInstance.Placement.Position = selectedCandidate.Position;
                tagInstance.Placement.ResolvedPosition = selectedCandidate.RelativePosition;
                tagInstance.Placement.LeaderType = selectedCandidate.LeaderType;
                tagInstance.Placement.LeaderLength = selectedCandidate.LeaderLength;
                tagInstance.PlacementScore = selectedCandidate.Score;
                tagInstance.Bounds = ComputeTagBoundsAtPosition(
                    selectedCandidate.Position, elementContext.EstimatedTagBounds);

                if (selectedCandidate.LeaderType != LeaderType.None)
                {
                    tagInstance.Placement.LeaderEndPoint = elementContext.HostBounds.Center;
                }

                // Step 9: Create the Revit tag via lifecycle manager
                bool created = _lifecycleManager.CreateTagInRevit(tagInstance, viewContext);
                if (created)
                {
                    tagInstance.State = tagInstance.State == TagState.PendingReview
                        ? TagState.PendingReview
                        : TagState.Active;
                    tagInstance.LastModified = DateTime.UtcNow;
                    result.Tag = tagInstance;
                    result.Success = true;

                    // Record with intelligence engine for future learning
                    _intelligenceEngine.RecordPlacement(tagInstance);
                }
                else
                {
                    result.Success = false;
                    result.FailureReason = "Failed to create Revit tag element";
                    Logger.Error("Element {0}: Revit tag creation failed in view {1}",
                        elementId, viewContext.ViewId);
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.FailureReason = ex.Message;
                Logger.Error(ex, "Element {0}: unhandled error during placement", elementId);
            }

            return result;
        }

        /// <summary>
        /// Processes a cluster of elements according to the cluster's recommended tagging strategy.
        /// </summary>
        private async Task<List<TagPlacementResult>> ProcessClusterAsync(
            ElementCluster cluster,
            TagPlacementRequest request,
            ViewTagContext viewContext,
            List<TagInstance> existingTagsInView,
            CancellationToken cancellationToken)
        {
            var results = new List<TagPlacementResult>();
            var strategy = cluster.RecommendedStrategy;

            Logger.Debug("Processing cluster {0}: type={1}, strategy={2}, elements={3}",
                cluster.ClusterId, cluster.Type, strategy, cluster.Count);

            switch (strategy)
            {
                case ClusterTagStrategy.TagAll:
                    foreach (int elementId in cluster.ElementIds)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var result = await ProcessElementAsync(
                            elementId, request, viewContext, existingTagsInView, cancellationToken);
                        results.Add(result);

                        if (result.Success && result.Tag != null)
                        {
                            existingTagsInView.Add(result.Tag);
                            lock (_lockObject)
                            {
                                _sessionPlacedTags.Add(result.Tag);
                            }
                            _repository.AddTag(result.Tag);
                        }
                    }
                    break;

                case ClusterTagStrategy.TagTypical:
                    var typicalResult = await ProcessElementAsync(
                        cluster.RepresentativeElementId, request, viewContext,
                        existingTagsInView, cancellationToken);

                    if (typicalResult.Success && typicalResult.Tag != null)
                    {
                        typicalResult.Tag.DisplayText += " (TYP.)";
                        typicalResult.Tag.Metadata["ClusterId"] = cluster.ClusterId;
                        typicalResult.Tag.Metadata["ClusterCount"] = cluster.Count;
                        existingTagsInView.Add(typicalResult.Tag);
                        lock (_lockObject)
                        {
                            _sessionPlacedTags.Add(typicalResult.Tag);
                        }
                        _repository.AddTag(typicalResult.Tag);
                    }
                    results.Add(typicalResult);

                    // Add skipped results for the non-representative elements
                    foreach (int elementId in cluster.ElementIds)
                    {
                        if (elementId == cluster.RepresentativeElementId) continue;
                        results.Add(new TagPlacementResult
                        {
                            Success = false,
                            FailureReason = "skipped"
                        });
                    }
                    break;

                case ClusterTagStrategy.TagWithCount:
                    var countResult = await ProcessElementAsync(
                        cluster.RepresentativeElementId, request, viewContext,
                        existingTagsInView, cancellationToken);

                    if (countResult.Success && countResult.Tag != null)
                    {
                        countResult.Tag.Metadata["ClusterId"] = cluster.ClusterId;
                        countResult.Tag.Metadata["ClusterCount"] = cluster.Count;
                        countResult.Tag.Metadata["CountParameter"] =
                            _configuration.Settings.ClusterCountParameterName;
                        existingTagsInView.Add(countResult.Tag);
                        lock (_lockObject)
                        {
                            _sessionPlacedTags.Add(countResult.Tag);
                        }
                        _repository.AddTag(countResult.Tag);
                    }
                    results.Add(countResult);

                    foreach (int elementId in cluster.ElementIds)
                    {
                        if (elementId == cluster.RepresentativeElementId) continue;
                        results.Add(new TagPlacementResult
                        {
                            Success = false,
                            FailureReason = "skipped"
                        });
                    }
                    break;

                case ClusterTagStrategy.TagRange:
                    // Tag first and last elements in the cluster
                    int firstId = cluster.ElementIds.First();
                    int lastId = cluster.ElementIds.Last();

                    var firstResult = await ProcessElementAsync(
                        firstId, request, viewContext, existingTagsInView, cancellationToken);
                    if (firstResult.Success && firstResult.Tag != null)
                    {
                        firstResult.Tag.Metadata["ClusterId"] = cluster.ClusterId;
                        firstResult.Tag.Metadata["RangePosition"] = "first";
                        existingTagsInView.Add(firstResult.Tag);
                        lock (_lockObject)
                        {
                            _sessionPlacedTags.Add(firstResult.Tag);
                        }
                        _repository.AddTag(firstResult.Tag);
                    }
                    results.Add(firstResult);

                    if (firstId != lastId)
                    {
                        var lastResult = await ProcessElementAsync(
                            lastId, request, viewContext, existingTagsInView, cancellationToken);
                        if (lastResult.Success && lastResult.Tag != null)
                        {
                            lastResult.Tag.Metadata["ClusterId"] = cluster.ClusterId;
                            lastResult.Tag.Metadata["RangePosition"] = "last";
                            existingTagsInView.Add(lastResult.Tag);
                            lock (_lockObject)
                            {
                                _sessionPlacedTags.Add(lastResult.Tag);
                            }
                            _repository.AddTag(lastResult.Tag);
                        }
                        results.Add(lastResult);
                    }

                    // Skip remaining elements
                    foreach (int elementId in cluster.ElementIds)
                    {
                        if (elementId == firstId || elementId == lastId) continue;
                        results.Add(new TagPlacementResult
                        {
                            Success = false,
                            FailureReason = "skipped"
                        });
                    }
                    break;

                case ClusterTagStrategy.TagGrouped:
                    var groupedResult = await ProcessElementAsync(
                        cluster.RepresentativeElementId, request, viewContext,
                        existingTagsInView, cancellationToken);

                    if (groupedResult.Success && groupedResult.Tag != null)
                    {
                        groupedResult.Tag.Metadata["ClusterId"] = cluster.ClusterId;
                        groupedResult.Tag.Metadata["ClusterCount"] = cluster.Count;
                        groupedResult.Tag.Metadata["GroupedDisplay"] = true;
                        existingTagsInView.Add(groupedResult.Tag);
                        lock (_lockObject)
                        {
                            _sessionPlacedTags.Add(groupedResult.Tag);
                        }
                        _repository.AddTag(groupedResult.Tag);
                    }
                    results.Add(groupedResult);

                    foreach (int elementId in cluster.ElementIds)
                    {
                        if (elementId == cluster.RepresentativeElementId) continue;
                        results.Add(new TagPlacementResult
                        {
                            Success = false,
                            FailureReason = "skipped"
                        });
                    }
                    break;
            }

            return results;
        }

        /// <summary>
        /// Places a single tag on a specific element in a specific view. Suitable for real-time,
        /// interactive tag placement triggered by user click.
        /// </summary>
        /// <param name="elementId">Revit ElementId of the element to tag.</param>
        /// <param name="viewId">Revit ElementId of the view to place the tag in.</param>
        /// <param name="strategy">Placement strategy to use.</param>
        /// <param name="templateName">Specific template name, or null for auto-selection.</param>
        /// <param name="hintPosition">Optional user-provided hint position for ManualHint strategy.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Placement result with the created tag and diagnostics.</returns>
        public async Task<TagPlacementResult> PlaceSingleTagAsync(
            int elementId,
            int viewId,
            PlacementStrategy strategy = PlacementStrategy.Automatic,
            string templateName = null,
            Point2D? hintPosition = null,
            CancellationToken cancellationToken = default)
        {
            var viewContext = GetOrCreateViewContext(viewId);
            var existingTags = _repository.GetTagsByView(viewId);

            var request = new TagPlacementRequest
            {
                ElementIds = new List<int> { elementId },
                ViewIds = new List<int> { viewId },
                Strategy = strategy,
                TemplateNames = templateName != null ? new List<string> { templateName } : null,
                EnableClusterDetection = false,
                RunQualityCheck = false
            };

            // For ManualHint strategy, score the hint position as an additional candidate
            if (strategy == PlacementStrategy.ManualHint && hintPosition.HasValue)
            {
                request.Options["HintPosition"] = hintPosition.Value;
            }

            var result = await ProcessElementAsync(
                elementId, request, viewContext, existingTags, cancellationToken);

            if (result.Success && result.Tag != null)
            {
                lock (_lockObject)
                {
                    _sessionPlacedTags.Add(result.Tag);
                }
                _repository.AddTag(result.Tag);
                _intelligenceEngine.RecordPlacement(result.Tag);
            }

            return result;
        }

        /// <summary>
        /// Re-optimizes the positions of all existing tags in a view without creating new tags.
        /// Equivalent to BIMLOGIQ's "Arrange Tags" feature but with full AI-powered optimization.
        /// </summary>
        /// <param name="viewId">Revit ElementId of the view to arrange tags in.</param>
        /// <param name="alignment">Alignment mode to apply during arrangement.</param>
        /// <param name="preserveUserAdjusted">Whether to preserve positions of user-adjusted tags.</param>
        /// <param name="progress">Optional progress reporter.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Batch result describing tag movements performed.</returns>
        public async Task<BatchPlacementResult> ArrangeExistingTagsAsync(
            int viewId,
            AlignmentMode alignment = AlignmentMode.Relaxed,
            bool preserveUserAdjusted = true,
            IProgress<PlacementProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            var batchResult = new BatchPlacementResult();
            var viewContext = GetOrCreateViewContext(viewId);
            var existingTags = _repository.GetTagsByView(viewId);

            Logger.Info("Arranging {0} existing tags in view {1}, alignment={2}",
                existingTags.Count, viewId, alignment);

            batchResult.TotalElements = existingTags.Count;
            batchResult.ViewsProcessed = 1;

            if (existingTags.Count == 0)
            {
                batchResult.Duration = stopwatch.Elapsed;
                return batchResult;
            }

            // Separate user-adjusted tags from auto-placed tags
            var tagsToArrange = preserveUserAdjusted
                ? existingTags.Where(t => !t.UserAdjusted).ToList()
                : existingTags.ToList();

            var fixedTags = preserveUserAdjusted
                ? existingTags.Where(t => t.UserAdjusted).ToList()
                : new List<TagInstance>();

            // Re-score each tag's current position and find better alternatives
            for (int i = 0; i < tagsToArrange.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var tag = tagsToArrange[i];

                ReportProgress(progress, i, tagsToArrange.Count,
                    viewId, 1, "Re-evaluating tag: " + tag.TagId);

                var hostBounds = _lifecycleManager.GetElementBounds(tag.HostElementId, viewContext);
                if (hostBounds == null)
                {
                    batchResult.SkippedCount++;
                    batchResult.Results.Add(new TagPlacementResult
                    {
                        Tag = tag,
                        Success = false,
                        FailureReason = "Host element not found in view"
                    });
                    continue;
                }

                var template = _repository.GetBestTemplate(tag.CategoryName, viewContext.ViewType)
                    ?? CreateDefaultTemplate(tag.CategoryName);

                var estimatedBounds = new TagBounds2D(0, 0, tag.Bounds.Width, tag.Bounds.Height);

                var candidates = GenerateCandidatePositions(
                    hostBounds, estimatedBounds, template, viewContext);

                // Include the current position as a candidate with a stability bonus
                var currentCandidate = new PlacementCandidate
                {
                    Position = tag.Placement.Position,
                    RelativePosition = tag.Placement.ResolvedPosition,
                    LeaderType = tag.Placement.LeaderType,
                    LeaderLength = tag.Placement.LeaderLength,
                    TemplatePriority = 0
                };
                candidates.Insert(0, currentCandidate);

                // Build a reference set of other tags (exclude the tag being re-evaluated)
                var otherTags = existingTags.Where(t => t.TagId != tag.TagId).ToList();
                otherTags.AddRange(fixedTags.Where(t => t.TagId != tag.TagId));

                foreach (var candidate in candidates)
                {
                    candidate.ScoreBreakdown = ScoreCandidate(
                        candidate, tag, hostBounds, otherTags, viewContext);
                    candidate.Score = candidate.ScoreBreakdown.TotalScore;
                }

                // Add stability bonus to the current position to avoid unnecessary moves
                double stabilityBonus = 0.05;
                candidates[0].Score += stabilityBonus;

                candidates.Sort((a, b) => b.Score.CompareTo(a.Score));

                var bestCandidate = candidates[0];
                double moveDistance = tag.Placement.Position.DistanceTo(bestCandidate.Position);

                var placementResult = new TagPlacementResult
                {
                    Tag = tag,
                    EvaluatedCandidates = candidates,
                    SelectedCandidate = bestCandidate,
                    FallbackLevel = 0
                };

                // Only move if the improvement is significant (more than 2mm displacement)
                if (moveDistance > 0.002 && bestCandidate.Position.DistanceTo(tag.Placement.Position) > 0.001)
                {
                    tag.Placement.Position = bestCandidate.Position;
                    tag.Placement.ResolvedPosition = bestCandidate.RelativePosition;
                    tag.Placement.LeaderType = bestCandidate.LeaderType;
                    tag.Placement.LeaderLength = bestCandidate.LeaderLength;
                    tag.Bounds = ComputeTagBoundsAtPosition(bestCandidate.Position, estimatedBounds);
                    tag.PlacementScore = bestCandidate.Score;
                    tag.LastModified = DateTime.UtcNow;

                    bool moved = _lifecycleManager.MoveTagInRevit(tag.TagId, bestCandidate.Position, viewContext);
                    placementResult.Success = moved;
                    if (moved)
                    {
                        _repository.UpdateTag(tag);
                        batchResult.SuccessCount++;
                    }
                    else
                    {
                        placementResult.FailureReason = "Failed to move Revit tag";
                        batchResult.FailureCount++;
                    }
                }
                else
                {
                    placementResult.Success = true;
                    batchResult.SkippedCount++;
                }

                batchResult.Results.Add(placementResult);
            }

            // Run global alignment optimization on all tags
            if (alignment != AlignmentMode.None && tagsToArrange.Count > 1)
            {
                var allViewTags = existingTags.ToList();
                var optimizedTags = OptimizeGlobalAlignment(allViewTags, viewContext);

                foreach (var optimized in optimizedTags)
                {
                    var original = existingTags.FirstOrDefault(t => t.TagId == optimized.TagId);
                    if (original != null && original.Placement.Position.DistanceTo(optimized.Placement.Position) > 0.001)
                    {
                        _lifecycleManager.MoveTagInRevit(optimized.TagId, optimized.Placement.Position, viewContext);
                        _repository.UpdateTag(optimized);
                    }
                }
            }

            stopwatch.Stop();
            batchResult.Duration = stopwatch.Elapsed;

            Logger.Info("Arrange complete: {0} moved, {1} kept, {2} failed, duration={3}ms",
                batchResult.SuccessCount, batchResult.SkippedCount,
                batchResult.FailureCount, stopwatch.ElapsedMilliseconds);

            return batchResult;
        }

        /// <summary>
        /// Attempts to resolve collisions for a candidate position by walking the template's fallback chain.
        /// Returns a resolved candidate or null if all fallback actions fail.
        /// </summary>
        private PlacementCandidate ResolveCollisionsWithFallback(
            PlacementCandidate originalCandidate,
            TagInstance tag,
            ElementTaggingContext elementContext,
            ViewTagContext viewContext,
            List<TagInstance> existingTags,
            List<TagCollision> collisions,
            double clearance,
            ref int fallbackLevel)
        {
            var fallbackChain = elementContext.Template.FallbackChain;
            if (fallbackChain == null || fallbackChain.Count == 0)
                return null;

            for (int i = 0; i < fallbackChain.Count && i < _configuration.Settings.MaxFallbackLevels; i++)
            {
                fallbackLevel = i;
                var action = fallbackChain[i];

                switch (action)
                {
                    case CollisionAction.Reposition:
                        // Already handled by trying the next candidate in the sorted list
                        return null;

                    case CollisionAction.Nudge:
                        var nudged = ApplyNudge(originalCandidate, elementContext, viewContext,
                            existingTags, collisions, clearance);
                        if (nudged != null) return nudged;
                        break;

                    case CollisionAction.LeaderReroute:
                        var rerouted = ApplyLeaderReroute(originalCandidate, elementContext,
                            viewContext, existingTags, clearance);
                        if (rerouted != null) return rerouted;
                        break;

                    case CollisionAction.Stack:
                        var stacked = ApplyStack(originalCandidate, elementContext,
                            viewContext, existingTags, collisions, clearance);
                        if (stacked != null) return stacked;
                        break;

                    case CollisionAction.Abbreviate:
                        // Abbreviation reduces tag bounds, which may resolve the collision
                        var abbreviated = ApplyAbbreviation(originalCandidate, elementContext,
                            viewContext, existingTags, clearance);
                        if (abbreviated != null) return abbreviated;
                        break;

                    case CollisionAction.FlagManual:
                        // Terminal action: return the original candidate flagged for manual review
                        return originalCandidate;
                }
            }

            return null;
        }

        /// <summary>
        /// Applies minimum displacement nudge to resolve a collision.
        /// </summary>
        private PlacementCandidate ApplyNudge(
            PlacementCandidate candidate,
            ElementTaggingContext elementContext,
            ViewTagContext viewContext,
            List<TagInstance> existingTags,
            List<TagCollision> collisions,
            double clearance)
        {
            var nudgeVector = _collisionResolver.ComputeNudgeVector(
                ComputeTagBoundsAtPosition(candidate.Position, elementContext.EstimatedTagBounds),
                collisions);

            var nudgedPosition = candidate.Position.Offset(nudgeVector.X, nudgeVector.Y);

            // Verify the nudged position is within the view crop region
            if (!IsWithinViewCropRegion(nudgedPosition, elementContext.EstimatedTagBounds, viewContext))
                return null;

            // Verify the nudged position does not create new collisions
            var nudgedBounds = ComputeTagBoundsAtPosition(nudgedPosition, elementContext.EstimatedTagBounds);
            var newCollisions = _collisionResolver.DetectCollisions(
                nudgedBounds, viewContext, existingTags, clearance);

            if (newCollisions.Count > 0)
                return null;

            // Determine if nudge requires a leader
            double distanceToHost = nudgedPosition.DistanceTo(elementContext.HostBounds.Center);
            double leaderThreshold = elementContext.Template.LeaderDistanceThreshold;
            var leaderType = distanceToHost > leaderThreshold ? LeaderType.Straight : LeaderType.None;
            double leaderLength = leaderType != LeaderType.None ? distanceToHost : 0.0;

            return new PlacementCandidate
            {
                Position = nudgedPosition,
                RelativePosition = candidate.RelativePosition,
                LeaderType = leaderType,
                LeaderLength = leaderLength,
                TemplatePriority = candidate.TemplatePriority,
                Score = candidate.Score * 0.90, // Slight penalty for nudging
                HasCollision = false,
                CollisionCount = 0
            };
        }

        /// <summary>
        /// Extends the tag further from the host element with a leader to reach a clear zone.
        /// </summary>
        private PlacementCandidate ApplyLeaderReroute(
            PlacementCandidate candidate,
            ElementTaggingContext elementContext,
            ViewTagContext viewContext,
            List<TagInstance> existingTags,
            double clearance)
        {
            var clearZone = _collisionResolver.FindClearZone(
                elementContext.HostBounds,
                elementContext.EstimatedTagBounds,
                viewContext,
                existingTags,
                elementContext.Template.MaxLeaderLength);

            if (clearZone == null)
                return null;

            Point2D clearPosition = clearZone.Value;

            if (!IsWithinViewCropRegion(clearPosition, elementContext.EstimatedTagBounds, viewContext))
                return null;

            double leaderLength = clearPosition.DistanceTo(elementContext.HostBounds.Center);
            if (leaderLength > elementContext.Template.MaxLeaderLength)
                return null;

            // Determine leader type based on geometry
            var leaderType = DetermineLeaderType(clearPosition, elementContext.HostBounds.Center,
                elementContext.Template);

            return new PlacementCandidate
            {
                Position = clearPosition,
                RelativePosition = candidate.RelativePosition,
                LeaderType = leaderType,
                LeaderLength = leaderLength,
                TemplatePriority = candidate.TemplatePriority + 1,
                Score = candidate.Score * 0.80, // Penalty for leader reroute
                HasCollision = false,
                CollisionCount = 0
            };
        }

        /// <summary>
        /// Stacks the tag vertically with the conflicting tag under a shared leader.
        /// </summary>
        private PlacementCandidate ApplyStack(
            PlacementCandidate candidate,
            ElementTaggingContext elementContext,
            ViewTagContext viewContext,
            List<TagInstance> existingTags,
            List<TagCollision> collisions,
            double clearance)
        {
            if (!elementContext.Template.AllowStacking)
                return null;

            // Find the primary conflicting tag to stack with
            var primaryConflict = collisions
                .OrderByDescending(c => c.OverlapArea)
                .FirstOrDefault();

            if (primaryConflict == null)
                return null;

            var conflictTag = existingTags.FirstOrDefault(t => t.TagId == primaryConflict.ConflictId);
            if (conflictTag == null || conflictTag.Bounds == null)
                return null;

            // Position this tag below the conflicting tag
            double stackOffsetY = conflictTag.Bounds.Height + clearance;
            var stackedPosition = new Point2D(
                conflictTag.Placement.Position.X,
                conflictTag.Placement.Position.Y - stackOffsetY);

            // Verify stacked position is clear
            var stackedBounds = ComputeTagBoundsAtPosition(stackedPosition, elementContext.EstimatedTagBounds);
            var newCollisions = _collisionResolver.DetectCollisions(
                stackedBounds, viewContext, existingTags, clearance);

            if (newCollisions.Count > 0)
                return null;

            if (!IsWithinViewCropRegion(stackedPosition, elementContext.EstimatedTagBounds, viewContext))
                return null;

            double leaderLength = stackedPosition.DistanceTo(elementContext.HostBounds.Center);

            return new PlacementCandidate
            {
                Position = stackedPosition,
                RelativePosition = candidate.RelativePosition,
                LeaderType = LeaderType.Straight,
                LeaderLength = leaderLength,
                TemplatePriority = candidate.TemplatePriority + 2,
                Score = candidate.Score * 0.70, // Larger penalty for stacking
                HasCollision = false,
                CollisionCount = 0
            };
        }

        /// <summary>
        /// Attempts to abbreviate tag content to reduce its footprint and resolve the collision.
        /// </summary>
        private PlacementCandidate ApplyAbbreviation(
            PlacementCandidate candidate,
            ElementTaggingContext elementContext,
            ViewTagContext viewContext,
            List<TagInstance> existingTags,
            double clearance)
        {
            // Estimate abbreviated tag bounds at 70% of original width
            double abbreviatedWidth = elementContext.EstimatedTagBounds.Width * 0.70;
            var abbreviatedBounds = new TagBounds2D(
                0, 0, abbreviatedWidth, elementContext.EstimatedTagBounds.Height);

            var boundsAtPosition = ComputeTagBoundsAtPosition(candidate.Position, abbreviatedBounds);
            var collisions = _collisionResolver.DetectCollisions(
                boundsAtPosition, viewContext, existingTags, clearance);

            if (collisions.Count > 0)
                return null;

            return new PlacementCandidate
            {
                Position = candidate.Position,
                RelativePosition = candidate.RelativePosition,
                LeaderType = candidate.LeaderType,
                LeaderLength = candidate.LeaderLength,
                TemplatePriority = candidate.TemplatePriority,
                Score = candidate.Score * 0.85, // Penalty for abbreviation
                HasCollision = false,
                CollisionCount = 0
            };
        }

        #endregion

        #region Candidate Generation

        /// <summary>
        /// Generates up to 24 candidate positions for placing a tag around its host element.
        /// Candidates include 8 cardinal/diagonal positions (each with near and far offset variants),
        /// center, insertion point, and leader-based positions for a total of up to 24 candidates.
        /// Positions outside the view crop region are excluded.
        /// </summary>
        /// <param name="hostBounds">Bounding box of the host element in view coordinates.</param>
        /// <param name="tagEstimatedBounds">Estimated bounding box of the tag (width/height only, origin at 0,0).</param>
        /// <param name="template">Tag template defining preferred positions, offsets, and leader settings.</param>
        /// <param name="context">View context with crop region and existing annotation bounds.</param>
        /// <returns>Ordered list of candidate positions with template priority assigned.</returns>
        public List<PlacementCandidate> GenerateCandidatePositions(
            TagBounds2D hostBounds,
            TagBounds2D tagEstimatedBounds,
            TagTemplateDefinition template,
            ViewTagContext context)
        {
            var candidates = new List<PlacementCandidate>();
            double tagW = tagEstimatedBounds.Width;
            double tagH = tagEstimatedBounds.Height;
            double baseOffset = GetEffectiveOffset(template, context);
            var hostCenter = hostBounds.Center;

            // Build a priority lookup from the template's preferred positions list
            var priorityMap = new Dictionary<TagPosition, int>();
            if (template.PreferredPositions != null)
            {
                for (int i = 0; i < template.PreferredPositions.Count; i++)
                    priorityMap[template.PreferredPositions[i]] = i;
            }

            int defaultPriority = priorityMap.Count > 0 ? priorityMap.Count : 10;

            // 8 cardinal + diagonal positions, each with near and far variants = 16 candidates
            TagPosition[] cardinalPositions = new[]
            {
                TagPosition.Top, TagPosition.Bottom, TagPosition.Left, TagPosition.Right,
                TagPosition.TopLeft, TagPosition.TopRight, TagPosition.BottomLeft, TagPosition.BottomRight
            };

            foreach (var relPos in cardinalPositions)
            {
                // Near variant
                Point2D nearPos = ComputeCardinalPosition(relPos, hostBounds, tagW, tagH,
                    baseOffset * NearOffsetMultiplier);
                int priority = priorityMap.ContainsKey(relPos) ? priorityMap[relPos] : defaultPriority;

                if (IsWithinViewCropRegion(nearPos, tagEstimatedBounds, context))
                {
                    candidates.Add(new PlacementCandidate
                    {
                        Position = nearPos,
                        RelativePosition = relPos,
                        LeaderType = LeaderType.None,
                        LeaderLength = 0.0,
                        TemplatePriority = priority
                    });
                }

                // Far variant
                Point2D farPos = ComputeCardinalPosition(relPos, hostBounds, tagW, tagH,
                    baseOffset * FarOffsetMultiplier);

                if (IsWithinViewCropRegion(farPos, tagEstimatedBounds, context))
                {
                    double farDistance = farPos.DistanceTo(hostCenter);
                    bool needsLeader = farDistance > template.LeaderDistanceThreshold;

                    candidates.Add(new PlacementCandidate
                    {
                        Position = farPos,
                        RelativePosition = relPos,
                        LeaderType = needsLeader ? LeaderType.Straight : LeaderType.None,
                        LeaderLength = needsLeader ? farDistance : 0.0,
                        TemplatePriority = priority + 1
                    });
                }
            }

            // Center position (inside the host element)
            if (template.AllowHostOverlap)
            {
                int centerPriority = priorityMap.ContainsKey(TagPosition.Center)
                    ? priorityMap[TagPosition.Center] : defaultPriority;

                if (IsWithinViewCropRegion(hostCenter, tagEstimatedBounds, context))
                {
                    candidates.Add(new PlacementCandidate
                    {
                        Position = hostCenter,
                        RelativePosition = TagPosition.Center,
                        LeaderType = LeaderType.None,
                        LeaderLength = 0.0,
                        TemplatePriority = centerPriority
                    });
                }
            }

            // Insertion point candidate
            var insertionPoint = GetElementInsertionPointFromContext(context, hostBounds);
            int insertionPriority = priorityMap.ContainsKey(TagPosition.InsertionPoint)
                ? priorityMap[TagPosition.InsertionPoint] : defaultPriority;

            if (IsWithinViewCropRegion(insertionPoint, tagEstimatedBounds, context))
            {
                double ipDistance = insertionPoint.DistanceTo(hostCenter);
                bool ipNeedsLeader = ipDistance > template.LeaderDistanceThreshold;

                candidates.Add(new PlacementCandidate
                {
                    Position = insertionPoint,
                    RelativePosition = TagPosition.InsertionPoint,
                    LeaderType = ipNeedsLeader ? LeaderType.Straight : LeaderType.None,
                    LeaderLength = ipNeedsLeader ? ipDistance : 0.0,
                    TemplatePriority = insertionPriority
                });
            }

            // Leader-based positions at larger offsets for the four cardinal directions
            TagPosition[] leaderDirections = new[]
            {
                TagPosition.Top, TagPosition.Bottom, TagPosition.Left, TagPosition.Right
            };

            foreach (var relPos in leaderDirections)
            {
                Point2D leaderPos = ComputeCardinalPosition(relPos, hostBounds, tagW, tagH,
                    baseOffset * LeaderOffsetMultiplier);

                if (IsWithinViewCropRegion(leaderPos, tagEstimatedBounds, context))
                {
                    double leaderLength = leaderPos.DistanceTo(hostCenter);
                    if (leaderLength <= template.MaxLeaderLength)
                    {
                        var leaderType = DetermineLeaderType(leaderPos, hostCenter, template);
                        int lPriority = priorityMap.ContainsKey(relPos)
                            ? priorityMap[relPos] + 2 : defaultPriority + 2;

                        candidates.Add(new PlacementCandidate
                        {
                            Position = leaderPos,
                            RelativePosition = relPos,
                            LeaderType = leaderType,
                            LeaderLength = leaderLength,
                            TemplatePriority = lPriority
                        });
                    }
                }
            }

            // Enforce maximum candidate count from configuration
            int maxCandidates = _configuration.Settings.MaxCandidatePositions;
            if (candidates.Count > maxCandidates)
            {
                candidates.Sort((a, b) => a.TemplatePriority.CompareTo(b.TemplatePriority));
                candidates = candidates.Take(maxCandidates).ToList();
            }

            Logger.Trace("Generated {0} candidate positions for host bounds {1}",
                candidates.Count, hostBounds.Center);

            return candidates;
        }

        /// <summary>
        /// Computes the tag anchor position for a cardinal or diagonal position relative to the host element.
        /// The anchor is the center point of the tag at the computed position.
        /// </summary>
        private Point2D ComputeCardinalPosition(
            TagPosition position,
            TagBounds2D hostBounds,
            double tagWidth,
            double tagHeight,
            double offset)
        {
            double hostCenterX = (hostBounds.MinX + hostBounds.MaxX) / 2.0;
            double hostCenterY = (hostBounds.MinY + hostBounds.MaxY) / 2.0;
            double halfTagW = tagWidth / 2.0;
            double halfTagH = tagHeight / 2.0;

            switch (position)
            {
                case TagPosition.Top:
                    return new Point2D(hostCenterX, hostBounds.MaxY + halfTagH + offset);

                case TagPosition.Bottom:
                    return new Point2D(hostCenterX, hostBounds.MinY - halfTagH - offset);

                case TagPosition.Left:
                    return new Point2D(hostBounds.MinX - halfTagW - offset, hostCenterY);

                case TagPosition.Right:
                    return new Point2D(hostBounds.MaxX + halfTagW + offset, hostCenterY);

                case TagPosition.TopLeft:
                    return new Point2D(
                        hostBounds.MinX - halfTagW - offset,
                        hostBounds.MaxY + halfTagH + offset);

                case TagPosition.TopRight:
                    return new Point2D(
                        hostBounds.MaxX + halfTagW + offset,
                        hostBounds.MaxY + halfTagH + offset);

                case TagPosition.BottomLeft:
                    return new Point2D(
                        hostBounds.MinX - halfTagW - offset,
                        hostBounds.MinY - halfTagH - offset);

                case TagPosition.BottomRight:
                    return new Point2D(
                        hostBounds.MaxX + halfTagW + offset,
                        hostBounds.MinY - halfTagH - offset);

                case TagPosition.Center:
                    return new Point2D(hostCenterX, hostCenterY);

                default:
                    return new Point2D(hostCenterX, hostBounds.MaxY + halfTagH + offset);
            }
        }

        /// <summary>
        /// Checks whether a tag placed at the given anchor position would be fully contained
        /// within the view's crop region.
        /// </summary>
        private bool IsWithinViewCropRegion(Point2D tagAnchor, TagBounds2D tagSize, ViewTagContext context)
        {
            if (context.CropRegion == null)
                return true;

            double halfW = tagSize.Width / 2.0;
            double halfH = tagSize.Height / 2.0;

            return tagAnchor.X - halfW >= context.CropRegion.MinX &&
                   tagAnchor.X + halfW <= context.CropRegion.MaxX &&
                   tagAnchor.Y - halfH >= context.CropRegion.MinY &&
                   tagAnchor.Y + halfH <= context.CropRegion.MaxY;
        }

        /// <summary>
        /// Derives the element insertion point from the view context or falls back to the host bounds center.
        /// </summary>
        private Point2D GetElementInsertionPointFromContext(ViewTagContext context, TagBounds2D hostBounds)
        {
            // Default to center offset downward slightly, simulating a typical Revit insertion point
            return new Point2D(
                hostBounds.Center.X,
                hostBounds.Center.Y - hostBounds.Height * 0.1);
        }

        #endregion

        #region Scoring

        /// <summary>
        /// Computes a 7-component placement score for a candidate position. Each component is
        /// individually calculated and then combined using configurable weights from
        /// <see cref="TagSettings"/>. The composite score drives candidate ranking in the
        /// placement pipeline.
        /// </summary>
        /// <param name="candidate">Candidate position to score.</param>
        /// <param name="tag">Tag instance being placed (for category, family context).</param>
        /// <param name="hostBounds">Host element bounding box.</param>
        /// <param name="existingTags">Already-placed tags in the view for collision and alignment checks.</param>
        /// <param name="context">View context with crop region and existing annotations.</param>
        /// <returns>Score breakdown with all 7 components and the composite total.</returns>
        public PlacementScoreBreakdown ScoreCandidate(
            PlacementCandidate candidate,
            TagInstance tag,
            TagBounds2D hostBounds,
            List<TagInstance> existingTags,
            ViewTagContext context)
        {
            var settings = _configuration.Settings;

            double proximityScore = ComputeProximityScore(candidate.Position, hostBounds);
            double collisionScore = ComputeCollisionScore(candidate, tag, existingTags, context);
            double alignmentScore = ComputeAlignmentScore(candidate, existingTags, tag.CategoryName, context);
            double leaderScore = ComputeLeaderScore(candidate, hostBounds);
            double readabilityScore = ComputeReadabilityScore(candidate, tag, context);
            double preferenceScore = ComputePreferenceScore(candidate, tag, context);
            double templatePriorityScore = ComputeTemplatePriorityScore(candidate);

            double compositeScore = ComputeCompositeScore(
                proximityScore, collisionScore, alignmentScore, leaderScore,
                readabilityScore, preferenceScore, templatePriorityScore, settings);

            return new PlacementScoreBreakdown
            {
                ProximityScore = proximityScore,
                CollisionScore = collisionScore,
                AlignmentScore = alignmentScore,
                LeaderScore = leaderScore,
                ReadabilityScore = readabilityScore,
                PreferenceScore = preferenceScore,
                TemplatePriorityScore = templatePriorityScore,
                TotalScore = compositeScore
            };
        }

        /// <summary>
        /// Proximity score: inversely proportional to the distance from the candidate position
        /// to the host element centroid. The reference distance is the diagonal of the host
        /// element bounding box, so a tag at one diagonal-length away scores 0.0 and a tag
        /// at the centroid scores 1.0.
        /// </summary>
        private double ComputeProximityScore(Point2D candidatePosition, TagBounds2D hostBounds)
        {
            double distance = candidatePosition.DistanceTo(hostBounds.Center);
            double referenceDiagonal = Math.Sqrt(
                hostBounds.Width * hostBounds.Width + hostBounds.Height * hostBounds.Height);

            // Prevent division by zero for zero-area elements
            if (referenceDiagonal < 1e-10)
                referenceDiagonal = 0.01;

            // Exponential decay gives a smoother gradient than linear
            double normalizedDistance = distance / referenceDiagonal;
            double score = Math.Exp(-1.5 * normalizedDistance);

            return Math.Max(0.0, Math.Min(1.0, score));
        }

        /// <summary>
        /// Collision score: 1.0 when the candidate has zero overlap with existing annotations.
        /// Decreases exponentially with the total overlap area relative to the tag's own area.
        /// Uses an exponent to penalize even small overlaps significantly.
        /// </summary>
        private double ComputeCollisionScore(
            PlacementCandidate candidate,
            TagInstance tag,
            List<TagInstance> existingTags,
            ViewTagContext context)
        {
            if (tag.Bounds == null && candidate.Position.X == 0.0 && candidate.Position.Y == 0.0)
                return 1.0;

            double tagWidth = tag.Bounds != null ? tag.Bounds.Width : 0.02;
            double tagHeight = tag.Bounds != null ? tag.Bounds.Height : 0.01;
            double tagArea = tagWidth * tagHeight;

            if (tagArea < 1e-10) return 1.0;

            var candidateBounds = new TagBounds2D(
                candidate.Position.X - tagWidth / 2.0,
                candidate.Position.Y - tagHeight / 2.0,
                candidate.Position.X + tagWidth / 2.0,
                candidate.Position.Y + tagHeight / 2.0);

            double totalOverlapArea = 0.0;

            // Check against already-placed tags
            foreach (var existing in existingTags)
            {
                if (existing.Bounds == null) continue;
                double overlap = candidateBounds.OverlapArea(existing.Bounds);
                totalOverlapArea += overlap;
            }

            // Check against existing view annotations (dimensions, text notes, etc.)
            if (context.ExistingAnnotationBounds != null)
            {
                foreach (var annotBounds in context.ExistingAnnotationBounds)
                {
                    double overlap = candidateBounds.OverlapArea(annotBounds);
                    totalOverlapArea += overlap;
                }
            }

            if (totalOverlapArea <= 0.0) return 1.0;

            double overlapRatio = totalOverlapArea / tagArea;
            double score = Math.Pow(Math.Max(0.0, 1.0 - overlapRatio), CollisionPenaltyExponent);

            return Math.Max(0.0, Math.Min(1.0, score));
        }

        /// <summary>
        /// Alignment score: measures how well the candidate position aligns with already-placed
        /// tags of the same category. Perfect horizontal or vertical alignment within the snap
        /// tolerance yields 1.0. Close alignment yields partial credit. No alignment yields a
        /// baseline of 0.3 to avoid overly penalizing the first tag placed.
        /// </summary>
        private double ComputeAlignmentScore(
            PlacementCandidate candidate,
            List<TagInstance> existingTags,
            string categoryName,
            ViewTagContext context)
        {
            // If no existing tags, return neutral score (first tag has no alignment peers)
            if (existingTags == null || existingTags.Count == 0)
                return 0.5;

            // Filter to tags of the same category for alignment evaluation
            var sameCategoryTags = existingTags
                .Where(t => string.Equals(t.CategoryName, categoryName, StringComparison.OrdinalIgnoreCase))
                .Where(t => t.Placement != null)
                .ToList();

            if (sameCategoryTags.Count == 0)
                return 0.5;

            double bestHorizontalDelta = double.MaxValue;
            double bestVerticalDelta = double.MaxValue;

            foreach (var existing in sameCategoryTags)
            {
                double deltaY = Math.Abs(candidate.Position.Y - existing.Placement.Position.Y);
                double deltaX = Math.Abs(candidate.Position.X - existing.Placement.Position.X);

                if (deltaY < bestHorizontalDelta) bestHorizontalDelta = deltaY;
                if (deltaX < bestVerticalDelta) bestVerticalDelta = deltaX;
            }

            double horizontalAlignScore = 0.0;
            if (bestHorizontalDelta < AlignmentRailSnapDistance)
                horizontalAlignScore = 1.0;
            else if (bestHorizontalDelta < AlignmentRailSnapDistance * 3.0)
                horizontalAlignScore = 1.0 - (bestHorizontalDelta - AlignmentRailSnapDistance)
                    / (AlignmentRailSnapDistance * 2.0);

            double verticalAlignScore = 0.0;
            if (bestVerticalDelta < AlignmentRailSnapDistance)
                verticalAlignScore = 1.0;
            else if (bestVerticalDelta < AlignmentRailSnapDistance * 3.0)
                verticalAlignScore = 1.0 - (bestVerticalDelta - AlignmentRailSnapDistance)
                    / (AlignmentRailSnapDistance * 2.0);

            // Take the better alignment (horizontal row or vertical column)
            double score = Math.Max(horizontalAlignScore, verticalAlignScore);

            // Blend with a baseline to not overly punish non-aligned positions
            return 0.3 + 0.7 * score;
        }

        /// <summary>
        /// Leader score: 1.0 for no leader (best readability). Decreases linearly with leader
        /// length, reaching 0.0 at the template's maximum leader length. Elbow leaders receive
        /// a small additional penalty for visual complexity.
        /// </summary>
        private double ComputeLeaderScore(PlacementCandidate candidate, TagBounds2D hostBounds)
        {
            if (candidate.LeaderType == LeaderType.None)
                return 1.0;

            double maxLeader = _configuration.Settings.DefaultLeaderDistanceThreshold * LeaderOffsetMultiplier;
            if (maxLeader < 1e-10) maxLeader = 0.05;

            double normalizedLength = Math.Min(1.0, candidate.LeaderLength / maxLeader);
            double score = 1.0 - normalizedLength;

            // Additional penalty for elbow leaders due to visual complexity
            if (candidate.LeaderType == LeaderType.Elbow)
                score *= 0.90;

            // Additional penalty for arc leaders
            if (candidate.LeaderType == LeaderType.Arc)
                score *= 0.85;

            return Math.Max(0.0, Math.Min(1.0, score));
        }

        /// <summary>
        /// Readability score: evaluates orientation, background clarity, and edge proximity.
        /// Horizontal tags with clear backgrounds away from crop region edges score highest.
        /// </summary>
        private double ComputeReadabilityScore(
            PlacementCandidate candidate,
            TagInstance tag,
            ViewTagContext context)
        {
            double score = 0.5; // Neutral baseline

            // Orientation bonus: horizontal tags are most readable
            var orientation = tag.Placement?.Orientation ?? TagOrientation.Horizontal;
            if (orientation == TagOrientation.Horizontal)
                score += ReadabilityHorizontalBonus;
            else if (orientation == TagOrientation.Vertical)
                score -= ReadabilityVerticalPenalty;

            // Clear background bonus: check if the candidate position overlaps with the host element
            if (tag.Bounds != null)
            {
                var candidateBounds = new TagBounds2D(
                    candidate.Position.X - tag.Bounds.Width / 2.0,
                    candidate.Position.Y - tag.Bounds.Height / 2.0,
                    candidate.Position.X + tag.Bounds.Width / 2.0,
                    candidate.Position.Y + tag.Bounds.Height / 2.0);

                // Check overlap with host element (tags on clear background are better)
                // We use the existing annotation bounds as a proxy for background clutter
                bool hasBackgroundClutter = false;
                if (context.ExistingAnnotationBounds != null)
                {
                    foreach (var annot in context.ExistingAnnotationBounds)
                    {
                        if (candidateBounds.Intersects(annot, 0.001))
                        {
                            hasBackgroundClutter = true;
                            break;
                        }
                    }
                }

                if (!hasBackgroundClutter)
                    score += ReadabilityClearBackgroundBonus;
            }

            // Edge proximity penalty: tags near the crop region edges are harder to read
            if (context.CropRegion != null)
            {
                double distToLeft = candidate.Position.X - context.CropRegion.MinX;
                double distToRight = context.CropRegion.MaxX - candidate.Position.X;
                double distToBottom = candidate.Position.Y - context.CropRegion.MinY;
                double distToTop = context.CropRegion.MaxY - candidate.Position.Y;

                double minEdgeDist = Math.Min(
                    Math.Min(distToLeft, distToRight),
                    Math.Min(distToBottom, distToTop));

                if (minEdgeDist < CropRegionEdgeMargin)
                {
                    double edgePenalty = ReadabilityEdgeProximityPenalty *
                        (1.0 - minEdgeDist / CropRegionEdgeMargin);
                    score -= edgePenalty;
                }
            }

            return Math.Max(0.0, Math.Min(1.0, score));
        }

        /// <summary>
        /// Preference score: queries the intelligence engine for learned user placement preferences.
        /// Returns the confidence-weighted similarity between the candidate and the learned pattern.
        /// Falls back to 0.5 (neutral) when learning is disabled or insufficient observations exist.
        /// </summary>
        private double ComputePreferenceScore(
            PlacementCandidate candidate,
            TagInstance tag,
            ViewTagContext context)
        {
            var settings = _configuration.Settings;

            if (!settings.LearningEnabled)
                return 0.5;

            double preferenceScore = _intelligenceEngine.GetPreferenceScore(
                candidate, tag.CategoryName, context.ViewType);

            // Clamp to valid range
            return Math.Max(0.0, Math.Min(1.0, preferenceScore));
        }

        /// <summary>
        /// Template priority score: higher scores for positions that match earlier entries in the
        /// template's preferred positions list. The first preferred position scores 1.0, the last
        /// scores near 0.0. Non-preferred positions receive a baseline score of 0.2.
        /// </summary>
        private double ComputeTemplatePriorityScore(PlacementCandidate candidate)
        {
            int maxPriority = _configuration.Settings.MaxCandidatePositions;
            if (maxPriority <= 0) maxPriority = 24;

            // TemplatePriority 0 = highest priority, maxPriority = lowest
            double normalizedPriority = (double)candidate.TemplatePriority / maxPriority;
            double score = 1.0 - Math.Min(1.0, normalizedPriority);

            // Ensure non-preferred positions still get a baseline score
            return Math.Max(0.2, score);
        }

        /// <summary>
        /// Computes the final composite score by weighting all 7 components. The 6 base components
        /// (proximity, collision, alignment, leader, readability, template priority) are combined
        /// using their configured weights. The preference score from the intelligence engine is
        /// then blended in using the intelligence score weight to produce the final ranking score.
        /// </summary>
        private double ComputeCompositeScore(
            double proximityScore,
            double collisionScore,
            double alignmentScore,
            double leaderScore,
            double readabilityScore,
            double preferenceScore,
            double templatePriorityScore,
            TagSettings settings)
        {
            // Compute the sum of base weights for normalization
            double weightSum = settings.ProximityWeight
                + settings.CollisionWeight
                + settings.AlignmentWeight
                + settings.LeaderWeight
                + settings.ReadabilityWeight
                + settings.TemplatePriorityWeight;

            // Guard against zero weight sum
            if (weightSum < 1e-10) weightSum = 1.0;

            // Weighted sum of the 6 base components
            double baseScore = (
                proximityScore * settings.ProximityWeight +
                collisionScore * settings.CollisionWeight +
                alignmentScore * settings.AlignmentWeight +
                leaderScore * settings.LeaderWeight +
                readabilityScore * settings.ReadabilityWeight +
                templatePriorityScore * settings.TemplatePriorityWeight
            ) / weightSum;

            // Blend the intelligence-learned preference score
            double intelligenceWeight = settings.LearningEnabled
                ? settings.IntelligenceScoreWeight
                : 0.0;

            double finalScore = baseScore * (1.0 - intelligenceWeight)
                + preferenceScore * intelligenceWeight;

            return Math.Max(0.0, finalScore);
        }

        #endregion

        #region Alignment Optimization

        /// <summary>
        /// Performs global alignment optimization on a set of placed tags within a view.
        /// Groups tags by category and proximity to shared horizontal/vertical alignment lines,
        /// then nudges tags onto the nearest alignment rail to produce clean, organized layouts.
        /// Iterates up to <see cref="MaxAlignmentIterations"/> times to resolve cascading adjustments.
        /// </summary>
        /// <param name="placedTags">Tags placed in the current batch for this view.</param>
        /// <param name="context">View context with crop region and constraints.</param>
        /// <returns>Updated tag instances with optimized positions.</returns>
        public List<TagInstance> OptimizeGlobalAlignment(
            List<TagInstance> placedTags,
            ViewTagContext context)
        {
            if (placedTags == null || placedTags.Count < 2)
                return placedTags ?? new List<TagInstance>();

            Logger.Debug("Starting global alignment optimization for {0} tags in view {1}",
                placedTags.Count, context.ViewId);

            var workingTags = placedTags.Select(t => CloneTagInstance(t)).ToList();

            for (int iteration = 0; iteration < MaxAlignmentIterations; iteration++)
            {
                // Discover alignment rails from the current tag positions
                var horizontalRails = DiscoverAlignmentRails(workingTags, isHorizontal: true);
                var verticalRails = DiscoverAlignmentRails(workingTags, isHorizontal: false);

                // Merge rails that are very close together
                horizontalRails = MergeCloseRails(horizontalRails);
                verticalRails = MergeCloseRails(verticalRails);

                int nudgeCount = 0;

                // Assign tags to their nearest rail and nudge
                foreach (var tag in workingTags)
                {
                    if (tag.UserAdjusted) continue;
                    if (tag.Placement == null) continue;

                    // Try horizontal alignment (shared Y position)
                    var bestHRail = FindBestRail(tag, horizontalRails, isHorizontal: true);
                    if (bestHRail != null)
                    {
                        double deltaY = bestHRail.Position - tag.Placement.Position.Y;
                        if (Math.Abs(deltaY) > 1e-6 && Math.Abs(deltaY) < AlignmentRailSnapDistance * 4.0)
                        {
                            var nudgedPos = new Point2D(tag.Placement.Position.X,
                                bestHRail.Position);

                            if (ValidateAlignmentNudge(tag, nudgedPos, workingTags, context))
                            {
                                tag.Placement.Position = nudgedPos;
                                tag.Bounds = ComputeTagBoundsAtPosition(nudgedPos,
                                    new TagBounds2D(0, 0, tag.Bounds.Width, tag.Bounds.Height));
                                nudgeCount++;
                            }
                        }
                    }

                    // Try vertical alignment (shared X position)
                    var bestVRail = FindBestRail(tag, verticalRails, isHorizontal: false);
                    if (bestVRail != null)
                    {
                        double deltaX = bestVRail.Position - tag.Placement.Position.X;
                        if (Math.Abs(deltaX) > 1e-6 && Math.Abs(deltaX) < AlignmentRailSnapDistance * 4.0)
                        {
                            var nudgedPos = new Point2D(bestVRail.Position,
                                tag.Placement.Position.Y);

                            if (ValidateAlignmentNudge(tag, nudgedPos, workingTags, context))
                            {
                                tag.Placement.Position = nudgedPos;
                                tag.Bounds = ComputeTagBoundsAtPosition(nudgedPos,
                                    new TagBounds2D(0, 0, tag.Bounds.Width, tag.Bounds.Height));
                                nudgeCount++;
                            }
                        }
                    }
                }

                // If no tags were nudged, the alignment has converged
                if (nudgeCount == 0)
                {
                    Logger.Debug("Alignment optimization converged after {0} iterations", iteration + 1);
                    break;
                }
            }

            return workingTags;
        }

        /// <summary>
        /// Discovers alignment rails from current tag positions by clustering tags with similar
        /// horizontal (Y) or vertical (X) coordinates.
        /// </summary>
        private List<AlignmentRail> DiscoverAlignmentRails(List<TagInstance> tags, bool isHorizontal)
        {
            var rails = new List<AlignmentRail>();
            var usedTags = new HashSet<string>();

            // Sort tags by the relevant coordinate
            var sortedTags = isHorizontal
                ? tags.Where(t => t.Placement != null).OrderBy(t => t.Placement.Position.Y).ToList()
                : tags.Where(t => t.Placement != null).OrderBy(t => t.Placement.Position.X).ToList();

            foreach (var tag in sortedTags)
            {
                if (usedTags.Contains(tag.TagId)) continue;

                double coord = isHorizontal ? tag.Placement.Position.Y : tag.Placement.Position.X;

                // Find all tags within snap distance of this coordinate
                var alignedTags = sortedTags
                    .Where(t => !usedTags.Contains(t.TagId))
                    .Where(t =>
                    {
                        double otherCoord = isHorizontal
                            ? t.Placement.Position.Y : t.Placement.Position.X;
                        return Math.Abs(otherCoord - coord) < AlignmentRailSnapDistance * 2.0;
                    })
                    .ToList();

                if (alignedTags.Count >= 2)
                {
                    // Rail position is the average coordinate of all aligned tags
                    double avgCoord = isHorizontal
                        ? alignedTags.Average(t => t.Placement.Position.Y)
                        : alignedTags.Average(t => t.Placement.Position.X);

                    var rail = new AlignmentRail
                    {
                        Position = avgCoord,
                        IsHorizontal = isHorizontal,
                        ViewId = tags.FirstOrDefault()?.ViewId ?? 0,
                        TagIds = alignedTags.Select(t => t.TagId).ToList()
                    };

                    rails.Add(rail);

                    foreach (var t in alignedTags)
                        usedTags.Add(t.TagId);
                }
            }

            return rails;
        }

        /// <summary>
        /// Merges alignment rails that are within the merge distance of each other,
        /// combining their tag assignments and averaging their positions.
        /// </summary>
        private List<AlignmentRail> MergeCloseRails(List<AlignmentRail> rails)
        {
            if (rails.Count < 2) return rails;

            var merged = new List<AlignmentRail>();
            var used = new HashSet<int>();

            rails.Sort((a, b) => a.Position.CompareTo(b.Position));

            for (int i = 0; i < rails.Count; i++)
            {
                if (used.Contains(i)) continue;

                var current = new AlignmentRail
                {
                    Position = rails[i].Position,
                    IsHorizontal = rails[i].IsHorizontal,
                    ViewId = rails[i].ViewId,
                    TagIds = new List<string>(rails[i].TagIds)
                };

                for (int j = i + 1; j < rails.Count; j++)
                {
                    if (used.Contains(j)) continue;

                    if (Math.Abs(rails[j].Position - current.Position) < AlignmentRailMergeDistance)
                    {
                        // Merge: weighted average position by tag count
                        int totalCount = current.TagIds.Count + rails[j].TagIds.Count;
                        current.Position = (current.Position * current.TagIds.Count +
                            rails[j].Position * rails[j].TagIds.Count) / totalCount;
                        current.TagIds.AddRange(rails[j].TagIds);
                        used.Add(j);
                    }
                }

                merged.Add(current);
                used.Add(i);
            }

            return merged;
        }

        /// <summary>
        /// Finds the best alignment rail for a tag based on proximity and category matching.
        /// </summary>
        private AlignmentRail FindBestRail(TagInstance tag, List<AlignmentRail> rails, bool isHorizontal)
        {
            if (rails.Count == 0 || tag.Placement == null) return null;

            double coord = isHorizontal ? tag.Placement.Position.Y : tag.Placement.Position.X;
            AlignmentRail best = null;
            double bestDistance = double.MaxValue;

            foreach (var rail in rails)
            {
                // Skip rails that already contain this tag
                if (rail.TagIds.Contains(tag.TagId)) continue;

                double distance = Math.Abs(rail.Position - coord);
                if (distance < bestDistance && distance < AlignmentRailSnapDistance * 4.0)
                {
                    bestDistance = distance;
                    best = rail;
                }
            }

            return best;
        }

        /// <summary>
        /// Validates that nudging a tag to a new position does not create collisions
        /// with other tags and remains within the view crop region.
        /// </summary>
        private bool ValidateAlignmentNudge(
            TagInstance tag,
            Point2D nudgedPosition,
            List<TagInstance> allTags,
            ViewTagContext context)
        {
            if (tag.Bounds == null) return false;

            var tagSize = new TagBounds2D(0, 0, tag.Bounds.Width, tag.Bounds.Height);

            // Check crop region containment
            if (!IsWithinViewCropRegion(nudgedPosition, tagSize, context))
                return false;

            // Check collisions with other tags
            var nudgedBounds = ComputeTagBoundsAtPosition(nudgedPosition, tagSize);
            double clearance = _configuration.Settings.CollisionClearance;

            foreach (var other in allTags)
            {
                if (other.TagId == tag.TagId) continue;
                if (other.Bounds == null) continue;

                if (nudgedBounds.Intersects(other.Bounds, clearance))
                    return false;
            }

            return true;
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Gets or creates a cached view context for the given view ID.
        /// </summary>
        private ViewTagContext GetOrCreateViewContext(int viewId)
        {
            lock (_lockObject)
            {
                if (_viewContextCache.TryGetValue(viewId, out var cached))
                    return cached;

                var context = _lifecycleManager.BuildViewContext(viewId);
                _viewContextCache[viewId] = context;
                return context;
            }
        }

        /// <summary>
        /// Retrieves the list of element IDs to process for a given view, applying the request's
        /// element ID filter or collecting all taggable elements if no filter is specified.
        /// </summary>
        private List<int> GetElementsForView(TagPlacementRequest request, ViewTagContext viewContext)
        {
            List<int> elements;

            if (request.ElementIds != null && request.ElementIds.Count > 0)
            {
                // Filter the request's element IDs to those visible in this view
                elements = request.ElementIds
                    .Where(id => _lifecycleManager.IsElementInView(id, viewContext.ViewId))
                    .ToList();
            }
            else
            {
                // Collect all taggable elements in the view
                elements = _lifecycleManager.GetTaggableElementsInView(viewContext.ViewId);
            }

            // Apply rule-based filtering
            elements = _ruleEngine.GetFilteredElementIds(elements, viewContext, request.RuleGroupName);

            return elements;
        }

        /// <summary>
        /// Builds the complete element tagging context including rule match, template selection,
        /// content generation, and bounds estimation.
        /// </summary>
        private ElementTaggingContext BuildElementContext(
            int elementId,
            TagPlacementRequest request,
            ViewTagContext viewContext)
        {
            // Get element metadata from the lifecycle manager
            var hostBounds = _lifecycleManager.GetElementBounds(elementId, viewContext);
            if (hostBounds == null)
            {
                Logger.Warn("Element {0}: no bounds available in view {1}", elementId, viewContext.ViewId);
                return null;
            }

            string categoryName = _lifecycleManager.GetElementCategory(elementId);
            string familyName = _lifecycleManager.GetElementFamilyName(elementId);
            string typeName = _lifecycleManager.GetElementTypeName(elementId);

            // Evaluate rules to find matching rule and template
            var matchingRule = _ruleEngine.GetMatchingRule(elementId, viewContext, request.RuleGroupName);
            if (matchingRule == null && !_ruleEngine.IsElementTaggable(elementId, viewContext))
            {
                Logger.Trace("Element {0}: not taggable by current rules", elementId);
                return null;
            }

            // Select template: request override > rule template > category default > system default
            TagTemplateDefinition template = null;

            if (request.TemplateNames != null && request.TemplateNames.Count > 0)
            {
                foreach (string templateName in request.TemplateNames)
                {
                    template = _repository.GetTemplate(templateName);
                    if (template != null) break;
                }
            }

            if (template == null && matchingRule != null && !string.IsNullOrEmpty(matchingRule.TemplateName))
            {
                template = _repository.GetTemplate(matchingRule.TemplateName);
            }

            if (template == null)
            {
                template = _repository.GetBestTemplate(categoryName, viewContext.ViewType);
            }

            if (template == null)
            {
                template = CreateDefaultTemplate(categoryName);
            }

            // Generate tag content text
            string content = _contentGenerator.GenerateContent(elementId, template);

            // Estimate tag bounds based on content and tag family
            var estimatedTagBounds = _contentGenerator.EstimateTagBounds(
                content, template, viewContext.Scale);

            // Get insertion point and rotation
            var insertionPoint = _lifecycleManager.GetElementInsertionPoint(elementId, viewContext);
            double rotation = _lifecycleManager.GetElementRotation(elementId, viewContext);

            return new ElementTaggingContext
            {
                ElementId = elementId,
                CategoryName = categoryName,
                FamilyName = familyName,
                TypeName = typeName,
                HostBounds = hostBounds,
                InsertionPoint = insertionPoint,
                MatchingRule = matchingRule,
                Template = template,
                GeneratedContent = content,
                EstimatedTagBounds = estimatedTagBounds,
                ElementRotation = rotation
            };
        }

        /// <summary>
        /// Builds a <see cref="TagInstance"/> from element context and an initial position.
        /// </summary>
        private TagInstance BuildTagInstance(
            ElementTaggingContext elementContext,
            ViewTagContext viewContext,
            Point2D initialPosition)
        {
            var orientation = DetermineOrientation(elementContext.Template,
                elementContext.ElementRotation, viewContext);

            return new TagInstance
            {
                TagId = Guid.NewGuid().ToString("N"),
                RevitElementId = 0,
                HostElementId = elementContext.ElementId,
                ViewId = viewContext.ViewId,
                CategoryName = elementContext.CategoryName,
                FamilyName = elementContext.FamilyName,
                TypeName = elementContext.TypeName,
                TagFamilyName = elementContext.Template.TagFamilyName,
                TagTypeName = elementContext.Template.TagTypeName,
                Placement = new TagPlacement
                {
                    Position = initialPosition,
                    PreferredPosition = elementContext.Template.PreferredPositions.Count > 0
                        ? elementContext.Template.PreferredPositions[0]
                        : TagPosition.Top,
                    Orientation = orientation,
                    OffsetX = elementContext.Template.OffsetX,
                    OffsetY = elementContext.Template.OffsetY
                },
                DisplayText = elementContext.GeneratedContent,
                ContentExpression = elementContext.Template.ContentExpression,
                State = TagState.Processing,
                Bounds = ComputeTagBoundsAtPosition(initialPosition, elementContext.EstimatedTagBounds),
                CreatedByRule = elementContext.MatchingRule?.RuleId,
                CreatedByTemplate = elementContext.Template.Name,
                CreationSource = TagCreationSource.AutomatedPlacement,
                LastModified = DateTime.UtcNow,
                UserAdjusted = false
            };
        }

        /// <summary>
        /// Computes an axis-aligned bounding box for a tag centered at the given position.
        /// </summary>
        private TagBounds2D ComputeTagBoundsAtPosition(Point2D center, TagBounds2D tagSize)
        {
            double halfW = tagSize.Width / 2.0;
            double halfH = tagSize.Height / 2.0;

            return new TagBounds2D(
                center.X - halfW,
                center.Y - halfH,
                center.X + halfW,
                center.Y + halfH);
        }

        /// <summary>
        /// Calculates the effective placement offset based on the template settings and view scale.
        /// Larger view scales (zoomed out) need proportionally larger offsets for readability.
        /// </summary>
        private double GetEffectiveOffset(TagTemplateDefinition template, ViewTagContext context)
        {
            double baseOffset = _configuration.Settings.CollisionClearance;

            if (template.OffsetX > 0 || template.OffsetY > 0)
                baseOffset = Math.Max(template.OffsetX, template.OffsetY);

            // Scale-aware offset: at 1:100, use base offset; at 1:200, double it
            double scaleFactor = context.Scale > 0 ? context.Scale / 100.0 : 1.0;
            return baseOffset * Math.Max(1.0, scaleFactor);
        }

        /// <summary>
        /// Determines the leader type based on geometry and template preferences.
        /// Uses elbow leaders when the horizontal distance is significantly different from
        /// the vertical distance; straight leaders otherwise.
        /// </summary>
        private LeaderType DetermineLeaderType(
            Point2D tagPosition,
            Point2D hostCenter,
            TagTemplateDefinition template)
        {
            if (template.LeaderType != LeaderType.Auto)
                return template.LeaderType;

            double dx = Math.Abs(tagPosition.X - hostCenter.X);
            double dy = Math.Abs(tagPosition.Y - hostCenter.Y);

            // If the tag is primarily offset in one axis, use an elbow for cleaner routing
            double ratio = dx > 1e-10 ? dy / dx : (dy > 1e-10 ? double.MaxValue : 1.0);

            if (ratio > 2.0 || ratio < 0.5)
                return LeaderType.Elbow;

            return LeaderType.Straight;
        }

        /// <summary>
        /// Determines tag orientation based on template settings, element rotation, and view type.
        /// </summary>
        private TagOrientation DetermineOrientation(
            TagTemplateDefinition template,
            double elementRotation,
            ViewTagContext context)
        {
            if (template.Orientation != TagOrientation.Auto)
                return template.Orientation;

            // In section/elevation views, prefer horizontal orientation
            if (context.ViewType == TagViewType.Section ||
                context.ViewType == TagViewType.Elevation)
                return TagOrientation.Horizontal;

            // If the template says follow element rotation and the element is significantly rotated
            if (template.FollowElementRotation && Math.Abs(elementRotation) > 0.1)
                return TagOrientation.FollowElement;

            // Check view type defaults from configuration
            if (_configuration.Settings.ViewTypeSettings.TryGetValue(context.ViewType, out var defaults))
                return defaults.Orientation;

            return TagOrientation.Horizontal;
        }

        /// <summary>
        /// Creates a default template when no matching template is found in the repository.
        /// Provides reasonable defaults for any element category.
        /// </summary>
        private TagTemplateDefinition CreateDefaultTemplate(string categoryName)
        {
            return new TagTemplateDefinition
            {
                Name = "Default_" + (categoryName ?? "Generic"),
                Description = "Auto-generated default template",
                CategoryName = categoryName,
                TagFamilyName = categoryName + " Tag",
                TagTypeName = "Standard",
                PreferredPositions = new List<TagPosition>
                {
                    TagPosition.Top,
                    TagPosition.Right,
                    TagPosition.Bottom,
                    TagPosition.Left,
                    TagPosition.TopRight,
                    TagPosition.TopLeft,
                    TagPosition.BottomRight,
                    TagPosition.BottomLeft,
                    TagPosition.Center
                },
                LeaderType = LeaderType.Auto,
                MinLeaderLength = 0.005,
                MaxLeaderLength = 0.05,
                LeaderDistanceThreshold = _configuration.Settings.DefaultLeaderDistanceThreshold,
                Orientation = TagOrientation.Auto,
                FollowElementRotation = false,
                AllowStacking = true,
                AllowHostOverlap = false,
                Alignment = _configuration.Settings.DefaultAlignmentMode,
                FallbackChain = new List<CollisionAction>
                {
                    CollisionAction.Reposition,
                    CollisionAction.Nudge,
                    CollisionAction.LeaderReroute,
                    CollisionAction.Stack,
                    CollisionAction.FlagManual
                }
            };
        }

        /// <summary>
        /// Reports placement progress to the optional progress handler.
        /// </summary>
        private void ReportProgress(
            IProgress<PlacementProgress> progress,
            int currentElement,
            int totalElements,
            int currentViewId,
            int totalViews,
            string operation)
        {
            progress?.Report(new PlacementProgress
            {
                CurrentElement = currentElement,
                TotalElements = totalElements,
                CurrentView = currentViewId,
                TotalViews = totalViews,
                CurrentOperation = operation
            });
        }

        /// <summary>
        /// Creates a deep clone of a TagInstance for use in alignment optimization
        /// without modifying the original reference.
        /// </summary>
        private TagInstance CloneTagInstance(TagInstance source)
        {
            return new TagInstance
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
                Placement = source.Placement != null ? new TagPlacement
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
                } : null,
                DisplayText = source.DisplayText,
                ContentExpression = source.ContentExpression,
                State = source.State,
                Bounds = source.Bounds != null ? new TagBounds2D(
                    source.Bounds.MinX, source.Bounds.MinY,
                    source.Bounds.MaxX, source.Bounds.MaxY) : null,
                CreatedByRule = source.CreatedByRule,
                CreatedByTemplate = source.CreatedByTemplate,
                CreationSource = source.CreationSource,
                PlacementScore = source.PlacementScore,
                LastModified = source.LastModified,
                UserAdjusted = source.UserAdjusted,
                Metadata = source.Metadata != null
                    ? new Dictionary<string, object>(source.Metadata)
                    : new Dictionary<string, object>()
            };
        }

        /// <summary>
        /// Computes an inline quality report for placed tags without requiring an external
        /// TagQualityAnalyzer dependency. Evaluates clash, alignment, and coverage metrics.
        /// </summary>
        private TagQualityReport ComputeInlineQualityReport(
            List<TagInstance> placedTags,
            List<int> viewIds)
        {
            var report = new TagQualityReport
            {
                GeneratedAt = DateTime.UtcNow,
                TotalTagsAnalyzed = placedTags.Count,
                TotalViewsAnalyzed = viewIds.Count,
                Issues = new List<TagQualityIssue>(),
                IssueCountsByType = new Dictionary<QualityIssueType, int>(),
                IssueCountsBySeverity = new Dictionary<IssueSeverity, int>()
            };

            var analysisStart = DateTime.UtcNow;
            double clearance = _configuration.Settings.CollisionClearance;
            int clashCount = 0;
            int misalignedCount = 0;
            int staleCount = 0;

            // Check for inter-tag clashes
            for (int i = 0; i < placedTags.Count; i++)
            {
                var tagA = placedTags[i];
                if (tagA.Bounds == null) continue;

                for (int j = i + 1; j < placedTags.Count; j++)
                {
                    var tagB = placedTags[j];
                    if (tagB.Bounds == null) continue;
                    if (tagA.ViewId != tagB.ViewId) continue;

                    if (tagA.Bounds.Intersects(tagB.Bounds, clearance))
                    {
                        double overlapArea = tagA.Bounds.OverlapArea(tagB.Bounds);
                        var severity = overlapArea > tagA.Bounds.Area * 0.5
                            ? IssueSeverity.Critical
                            : IssueSeverity.Warning;

                        report.Issues.Add(new TagQualityIssue
                        {
                            IssueId = Guid.NewGuid().ToString("N"),
                            IssueType = QualityIssueType.Clash,
                            Severity = severity,
                            AffectedTagId = tagA.TagId,
                            AffectedElementId = tagA.RevitElementId,
                            ViewId = tagA.ViewId,
                            Description = string.Format("Tag '{0}' clashes with tag '{1}', overlap area: {2:F6}",
                                tagA.TagId, tagB.TagId, overlapArea),
                            IsAutoFixable = true,
                            SuggestedFix = "Nudge or reposition one of the conflicting tags",
                            Location = tagA.Placement?.Position ?? new Point2D(0, 0),
                            DetectedAt = DateTime.UtcNow
                        });
                        clashCount++;
                    }
                }

                // Check for pending review state (manual flags)
                if (tagA.State == TagState.PendingReview)
                {
                    report.Issues.Add(new TagQualityIssue
                    {
                        IssueId = Guid.NewGuid().ToString("N"),
                        IssueType = QualityIssueType.Clash,
                        Severity = IssueSeverity.Warning,
                        AffectedTagId = tagA.TagId,
                        AffectedElementId = tagA.RevitElementId,
                        ViewId = tagA.ViewId,
                        Description = "Tag was flagged for manual review during placement",
                        IsAutoFixable = false,
                        SuggestedFix = "Manually reposition the tag to resolve collisions",
                        Location = tagA.Placement?.Position ?? new Point2D(0, 0),
                        DetectedAt = DateTime.UtcNow
                    });
                }

                // Check for blank/empty display text
                if (string.IsNullOrWhiteSpace(tagA.DisplayText) || tagA.DisplayText == "?")
                {
                    report.Issues.Add(new TagQualityIssue
                    {
                        IssueId = Guid.NewGuid().ToString("N"),
                        IssueType = QualityIssueType.Blank,
                        Severity = IssueSeverity.Critical,
                        AffectedTagId = tagA.TagId,
                        AffectedElementId = tagA.RevitElementId,
                        ViewId = tagA.ViewId,
                        Description = "Tag displays blank or placeholder text",
                        IsAutoFixable = false,
                        SuggestedFix = "Verify element parameter values are populated",
                        Location = tagA.Placement?.Position ?? new Point2D(0, 0),
                        DetectedAt = DateTime.UtcNow
                    });
                    staleCount++;
                }
            }

            // Check per-view alignment quality
            foreach (int viewId in viewIds)
            {
                var viewTags = placedTags.Where(t => t.ViewId == viewId).ToList();
                if (viewTags.Count < 2) continue;

                var categoryGroups = viewTags.GroupBy(t => t.CategoryName);
                foreach (var group in categoryGroups)
                {
                    var tags = group.ToList();
                    if (tags.Count < 2) continue;

                    // Check if tags within the same category are reasonably aligned
                    var yPositions = tags.Where(t => t.Placement != null)
                        .Select(t => t.Placement.Position.Y).ToList();
                    var xPositions = tags.Where(t => t.Placement != null)
                        .Select(t => t.Placement.Position.X).ToList();

                    if (yPositions.Count < 2) continue;

                    double yRange = yPositions.Max() - yPositions.Min();
                    double xRange = xPositions.Max() - xPositions.Min();

                    // If all tags are spread without clear alignment, flag misalignment
                    double avgSpread = (yRange + xRange) / 2.0;
                    if (avgSpread > AlignmentRailSnapDistance * 10.0)
                    {
                        bool hasHorizontalAlignment = HasGroupAlignment(yPositions, AlignmentRailSnapDistance * 2.0);
                        bool hasVerticalAlignment = HasGroupAlignment(xPositions, AlignmentRailSnapDistance * 2.0);

                        if (!hasHorizontalAlignment && !hasVerticalAlignment)
                        {
                            foreach (var tag in tags)
                            {
                                report.Issues.Add(new TagQualityIssue
                                {
                                    IssueId = Guid.NewGuid().ToString("N"),
                                    IssueType = QualityIssueType.Misaligned,
                                    Severity = IssueSeverity.Info,
                                    AffectedTagId = tag.TagId,
                                    AffectedElementId = tag.RevitElementId,
                                    ViewId = viewId,
                                    Description = string.Format("Tag in category '{0}' is not aligned with peers",
                                        group.Key),
                                    IsAutoFixable = true,
                                    SuggestedFix = "Run Arrange Tags to align with category peers",
                                    Location = tag.Placement?.Position ?? new Point2D(0, 0),
                                    DetectedAt = DateTime.UtcNow
                                });
                                misalignedCount++;
                            }
                        }
                    }
                }

                // Compute per-view score
                int viewIssues = report.Issues.Count(i => i.ViewId == viewId);
                double viewScore = viewTags.Count > 0
                    ? Math.Max(0.0, 100.0 - (double)viewIssues / viewTags.Count * 50.0)
                    : 100.0;
                report.ViewScores[viewId] = viewScore;
            }

            // Aggregate issue counts
            foreach (var issue in report.Issues)
            {
                if (!report.IssueCountsByType.ContainsKey(issue.IssueType))
                    report.IssueCountsByType[issue.IssueType] = 0;
                report.IssueCountsByType[issue.IssueType]++;

                if (!report.IssueCountsBySeverity.ContainsKey(issue.Severity))
                    report.IssueCountsBySeverity[issue.Severity] = 0;
                report.IssueCountsBySeverity[issue.Severity]++;
            }

            report.AutoFixableCount = report.Issues.Count(i => i.IsAutoFixable);

            // Overall quality score: 100 minus weighted deductions
            int criticalCount = report.IssueCountsBySeverity.ContainsKey(IssueSeverity.Critical)
                ? report.IssueCountsBySeverity[IssueSeverity.Critical] : 0;
            int warningCount = report.IssueCountsBySeverity.ContainsKey(IssueSeverity.Warning)
                ? report.IssueCountsBySeverity[IssueSeverity.Warning] : 0;
            int infoCount = report.IssueCountsBySeverity.ContainsKey(IssueSeverity.Info)
                ? report.IssueCountsBySeverity[IssueSeverity.Info] : 0;

            double deduction = criticalCount * 10.0 + warningCount * 3.0 + infoCount * 0.5;
            double maxDeduction = placedTags.Count > 0 ? placedTags.Count * 10.0 : 100.0;
            report.QualityScore = Math.Max(0.0, 100.0 - deduction / maxDeduction * 100.0);

            report.AnalysisDuration = DateTime.UtcNow - analysisStart;

            Logger.Info("Quality report: score={0:F1}%, issues={1} (critical={2}, warning={3}, info={4})",
                report.QualityScore, report.Issues.Count, criticalCount, warningCount, infoCount);

            return report;
        }

        /// <summary>
        /// Checks whether a group of coordinate values has at least a subgroup that shares
        /// alignment within the given tolerance.
        /// </summary>
        private bool HasGroupAlignment(List<double> values, double tolerance)
        {
            if (values.Count < 2) return true;

            values.Sort();

            // Find the largest subgroup of values within tolerance of each other
            int maxGroupSize = 1;
            int currentGroupSize = 1;

            for (int i = 1; i < values.Count; i++)
            {
                if (Math.Abs(values[i] - values[i - 1]) < tolerance)
                {
                    currentGroupSize++;
                    if (currentGroupSize > maxGroupSize)
                        maxGroupSize = currentGroupSize;
                }
                else
                {
                    currentGroupSize = 1;
                }
            }

            // At least half the tags should be aligned
            return maxGroupSize >= Math.Max(2, values.Count / 2);
        }

        #endregion
    }

    #region Internal Supporting Types

    /// <summary>
    /// Represents a horizontal or vertical alignment line that tags can snap to
    /// for consistent, clean documentation layouts. Discovered during global
    /// alignment optimization by clustering tags with similar coordinates.
    /// </summary>
    internal class AlignmentRail
    {
        /// <summary>Position of the rail: Y coordinate for horizontal rails, X coordinate for vertical rails.</summary>
        public double Position { get; set; }

        /// <summary>Whether this is a horizontal rail (tags share Y) or vertical rail (tags share X).</summary>
        public bool IsHorizontal { get; set; }

        /// <summary>IDs of tags currently assigned to this rail.</summary>
        public List<string> TagIds { get; set; } = new List<string>();

        /// <summary>View ID this rail belongs to.</summary>
        public int ViewId { get; set; }

        /// <summary>Strength of this rail: the number of tags that naturally clustered onto it.</summary>
        public int Strength => TagIds.Count;
    }

    /// <summary>
    /// Bundles all context information needed for placing a tag on a single element
    /// within a specific view. Built once per element and reused across candidate
    /// generation, scoring, and tag instance creation.
    /// </summary>
    internal class ElementTaggingContext
    {
        /// <summary>Revit ElementId of the host element.</summary>
        public int ElementId { get; set; }

        /// <summary>Revit category name (e.g., "Doors", "Walls").</summary>
        public string CategoryName { get; set; }

        /// <summary>Family name of the host element.</summary>
        public string FamilyName { get; set; }

        /// <summary>Type name of the host element.</summary>
        public string TypeName { get; set; }

        /// <summary>Bounding box of the host element in view coordinates.</summary>
        public TagBounds2D HostBounds { get; set; }

        /// <summary>Insertion point of the host element in view coordinates.</summary>
        public Point2D InsertionPoint { get; set; }

        /// <summary>Matching tag rule from the rule engine (may be null).</summary>
        public TagRule MatchingRule { get; set; }

        /// <summary>Selected tag template for this element.</summary>
        public TagTemplateDefinition Template { get; set; }

        /// <summary>Detected cluster this element belongs to (null if unclustered).</summary>
        public ElementCluster Cluster { get; set; }

        /// <summary>Generated display text for the tag.</summary>
        public string GeneratedContent { get; set; }

        /// <summary>Estimated bounding box of the tag (width/height, origin at 0,0).</summary>
        public TagBounds2D EstimatedTagBounds { get; set; }

        /// <summary>Rotation angle of the host element in radians.</summary>
        public double ElementRotation { get; set; }
    }

    #endregion
}
