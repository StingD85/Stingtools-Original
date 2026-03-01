// StingBIM.AI.Tagging - SuperIntelligent Tagging System
// TagAnnotationOptimizer.cs - Global annotation layout optimization engine
// Optimizes the ENTIRE VIEW as a unified combinatorial optimization problem
// While TagPlacementEngine optimizes one tag at a time, this engine treats
// all tags in a view as a single layout system and finds the global optimum.
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

namespace StingBIM.AI.Tagging.Engine
{
    /// <summary>
    /// Global annotation layout optimization engine. While <see cref="TagPlacementEngine"/>
    /// optimizes one tag at a time, this engine treats the ENTIRE VIEW as a unified
    /// combinatorial optimization problem, jointly optimizing positions, alignment, spacing,
    /// leader routes, and grouping for all tags via simulated annealing.
    ///
    /// <para><strong>Energy Function:</strong> Weighted sum of collision area, total leader
    /// length, alignment deviation, distance from preferred positions, and readability
    /// penalties. Five move operators (shift, swap, rotate, toggle-leader, adjust-leader-
    /// endpoint) explore the solution space with Boltzmann acceptance.</para>
    /// </summary>
    public class TagAnnotationOptimizer
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        #region Inner Types

        /// <summary>Options controlling optimization subsystems and tuning parameters.</summary>
        public class OptimizationOptions
        {
            public int MaxIterations { get; set; } = 5000;
            public double CoolingRate { get; set; } = 0.995;
            public double MinTemperature { get; set; } = 0.001;
            public double InitialTemperature { get; set; } = 100.0;
            public bool EnableAlignmentRails { get; set; } = true;
            public bool EnableSpacingNormalization { get; set; } = true;
            public bool EnableLeaderOptimization { get; set; } = true;
            public bool EnableDensityAdaptation { get; set; } = true;
            public bool EnableTagGrouping { get; set; } = true;
            /// <summary>If true, tags with UserAdjusted=true are not moved.</summary>
            public bool LockManuallyPlacedTags { get; set; } = true;
            public double CollisionWeight { get; set; } = 10.0;
            public double LeaderLengthWeight { get; set; } = 2.0;
            public double AlignmentWeight { get; set; } = 3.0;
            public double PreferredPositionWeight { get; set; } = 1.5;
            public double ReadabilityWeight { get; set; } = 2.5;
            public int MinRailTagCount { get; set; } = 3;
            public double RailToleranceBand { get; set; } = 0.004;
            public double RailSnapTolerance { get; set; } = 0.006;
            public double MinLeaderAngleDegrees { get; set; } = 15.0;
            public double DensityKernelBandwidth { get; set; } = 0.05;
            public double MinimumClearance { get; set; } = 0.002;
        }

        /// <summary>Result of global layout optimization with before/after metrics.</summary>
        public class OptimizationResult
        {
            public double EnergyBefore { get; set; }
            public double EnergyAfter { get; set; }
            public double ImprovementPercentage { get; set; }
            public int IterationsExecuted { get; set; }
            public int MovesAccepted { get; set; }
            public int UphillMovesAccepted { get; set; }
            public double FinalTemperature { get; set; }
            public List<AlignmentRailInfo> AlignmentRails { get; set; } = new List<AlignmentRailInfo>();
            public List<SpacingProfile> SpacingProfiles { get; set; } = new List<SpacingProfile>();
            public LeaderOptimizationResult LeaderResult { get; set; }
            public List<DensityRegion> DensityRegions { get; set; } = new List<DensityRegion>();
            public List<TagGroup> TagGroups { get; set; } = new List<TagGroup>();
            public List<TagMoveRecord> AppliedMoves { get; set; } = new List<TagMoveRecord>();
            public int TotalTags { get; set; }
            public int LockedTags { get; set; }
            public TimeSpan Duration { get; set; }
            public int ViewId { get; set; }
        }

        /// <summary>Progress information emitted during optimization for UI feedback.</summary>
        public class OptimizationProgress
        {
            public string Phase { get; set; }
            public int CurrentIteration { get; set; }
            public int TotalIterations { get; set; }
            public double CurrentEnergy { get; set; }
            public double BestEnergy { get; set; }
            public double CurrentTemperature { get; set; }
            public double PercentComplete { get; set; }
        }

        /// <summary>Alignment rail info: position, orientation, snapped tag count, confidence.</summary>
        public class AlignmentRailInfo
        {
            public double Position { get; set; }
            public RailOrientation Orientation { get; set; }
            public int SnappedTagCount { get; set; }
            public double Confidence { get; set; }
            public List<string> TagIds { get; set; } = new List<string>();
        }

        /// <summary>Orientation of an alignment rail.</summary>
        public enum RailOrientation { Horizontal, Vertical }

        /// <summary>Statistical spacing profile between adjacent tags on a rail or in a group.</summary>
        public class SpacingProfile
        {
            public string GroupId { get; set; }
            public double MinSpacing { get; set; }
            public double MaxSpacing { get; set; }
            public double MeanSpacing { get; set; }
            public double StdDevSpacing { get; set; }
            public double IdealSpacing { get; set; }
            public int PairCount { get; set; }
        }

        /// <summary>Result of leader route optimization.</summary>
        public class LeaderOptimizationResult
        {
            public int CrossingsEliminated { get; set; }
            public double TotalLengthBefore { get; set; }
            public double TotalLengthAfter { get; set; }
            public double LengthReduction => TotalLengthBefore - TotalLengthAfter;
            public int AngleViolationsFixed { get; set; }
            public int LeadersRerouted { get; set; }
        }

        /// <summary>Density classification levels for view regions.</summary>
        public enum DensityLevel { Low, Medium, High, VeryHigh }

        /// <summary>A region in the view classified by element density for adaptive placement.</summary>
        public class DensityRegion
        {
            public Point2D Center { get; set; }
            public double Radius { get; set; }
            public DensityLevel Level { get; set; }
            public int ElementCount { get; set; }
            public double RawDensity { get; set; }
        }

        /// <summary>A group of related tags optimized as a unit for visual coherence.</summary>
        public class TagGroup
        {
            public string GroupId { get; set; }
            public TagGroupType GroupType { get; set; }
            public List<string> MemberTagIds { get; set; } = new List<string>();
            public TagBounds2D GroupBounds { get; set; }
            public double AlignmentAxisPosition { get; set; }
            public bool IsHorizontalAlignment { get; set; }
            public string SharedProperty { get; set; }
        }

        /// <summary>Tag group relationship type.</summary>
        public enum TagGroupType { SameSystem, SameRoom, SameHost, SameCategory, UserDefined }

        /// <summary>Record of a single tag move applied during optimization.</summary>
        public class TagMoveRecord
        {
            public string TagId { get; set; }
            public Point2D OriginalPosition { get; set; }
            public Point2D NewPosition { get; set; }
            public MoveType MoveType { get; set; }
            public double EnergyDelta { get; set; }
        }

        /// <summary>Types of moves used during simulated annealing.</summary>
        public enum MoveType
        {
            ShiftSingle, SwapTwo, RotateTag, ToggleLeader,
            AdjustLeaderEndpoint, SnapToRail, NormalizeSpacing
        }

        #endregion

        #region Fields

        private readonly TagRepository _repository;
        private readonly TagConfiguration _configuration;
        private readonly CollisionResolver _collisionResolver;
        private readonly object _lockObject = new object();
        private readonly Random _random;

        private Dictionary<string, TagWorkingState> _workingStates;
        private List<InternalAlignmentRail> _alignmentRails;
        private List<DensityRegion> _densityRegions;
        private List<TagGroup> _tagGroups;
        private ViewTagContext _currentViewContext;
        private OptimizationOptions _currentOptions;
        private Dictionary<string, TagWorkingState> _bestStates;
        private double _bestEnergy;

        #endregion

        #region Working State Types

        /// <summary>Mutable working copy of a tag's position used during annealing.</summary>
        private class TagWorkingState
        {
            public string TagId { get; set; }
            public Point2D Position { get; set; }
            public TagBounds2D Bounds { get; set; }
            public Point2D LeaderEndPoint { get; set; }
            public Point2D? LeaderElbowPoint { get; set; }
            public LeaderType LeaderType { get; set; }
            public double Rotation { get; set; }
            public double Width { get; set; }
            public double Height { get; set; }
            public bool IsLocked { get; set; }
            public string CategoryName { get; set; }
            public int HostElementId { get; set; }
            public Point2D PreferredPosition { get; set; }
            public Point2D HostCenter { get; set; }
            public string RoomName { get; set; }
            public string SystemName { get; set; }

            public TagWorkingState Clone() => new TagWorkingState
            {
                TagId = TagId,
                Position = new Point2D(Position.X, Position.Y),
                Bounds = new TagBounds2D(Bounds.MinX, Bounds.MinY, Bounds.MaxX, Bounds.MaxY),
                LeaderEndPoint = new Point2D(LeaderEndPoint.X, LeaderEndPoint.Y),
                LeaderElbowPoint = LeaderElbowPoint.HasValue
                    ? new Point2D(LeaderElbowPoint.Value.X, LeaderElbowPoint.Value.Y)
                    : (Point2D?)null,
                LeaderType = LeaderType, Rotation = Rotation,
                Width = Width, Height = Height, IsLocked = IsLocked,
                CategoryName = CategoryName, HostElementId = HostElementId,
                PreferredPosition = new Point2D(PreferredPosition.X, PreferredPosition.Y),
                HostCenter = new Point2D(HostCenter.X, HostCenter.Y),
                RoomName = RoomName, SystemName = SystemName
            };
        }

        private class InternalAlignmentRail
        {
            public double Position { get; set; }
            public bool IsHorizontal { get; set; }
            public List<string> TagIds { get; set; } = new List<string>();
            public double Confidence { get; set; }
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes a new instance of the <see cref="TagAnnotationOptimizer"/> class.
        /// </summary>
        public TagAnnotationOptimizer(
            TagRepository repository, TagConfiguration configuration,
            CollisionResolver collisionResolver)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _collisionResolver = collisionResolver ?? throw new ArgumentNullException(nameof(collisionResolver));
            _random = new Random(42); // Deterministic seed for reproducibility
            _workingStates = new Dictionary<string, TagWorkingState>(StringComparer.OrdinalIgnoreCase);
            _alignmentRails = new List<InternalAlignmentRail>();
            _densityRegions = new List<DensityRegion>();
            _tagGroups = new List<TagGroup>();
            Logger.Info("TagAnnotationOptimizer initialized");
        }

        #endregion

        #region Core Public API

        /// <summary>
        /// Optimizes the entire annotation layout for a view. Orchestrates density analysis,
        /// grouping, alignment rail discovery, simulated annealing, spacing normalization,
        /// and leader optimization into a single optimization pass.
        /// </summary>
        public async Task<OptimizationResult> OptimizeViewLayoutAsync(
            int viewId, OptimizationOptions options,
            IProgress<OptimizationProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            var stopwatch = Stopwatch.StartNew();
            Logger.Info("Starting global layout optimization for view {ViewId}", viewId);
            var result = new OptimizationResult { ViewId = viewId };

            try
            {
                // Phase 1: Initialize working state
                ReportProgress(progress, "Initializing", 0, 1, 0, 0, 0, 2);
                await Task.Run(() => InitializeWorkingState(viewId, options), cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                result.TotalTags = _workingStates.Count;
                result.LockedTags = _workingStates.Values.Count(s => s.IsLocked);

                if (result.TotalTags - result.LockedTags < 2)
                {
                    Logger.Info("View {ViewId}: fewer than 2 movable tags, skipping", viewId);
                    result.EnergyBefore = result.EnergyAfter = ComputeTotalEnergy(options);
                    result.Duration = stopwatch.Elapsed;
                    return result;
                }

                // Phase 2: Density analysis
                if (options.EnableDensityAdaptation)
                {
                    ReportProgress(progress, "Analyzing density", 0, 1, 0, 0, 0, 8);
                    await Task.Run(() => AnalyzeDensity(options), cancellationToken);
                    result.DensityRegions = new List<DensityRegion>(_densityRegions);
                }

                // Phase 3: Tag grouping
                if (options.EnableTagGrouping)
                {
                    ReportProgress(progress, "Detecting tag groups", 0, 1, 0, 0, 0, 14);
                    await Task.Run(() => DetectTagGroups(), cancellationToken);
                    result.TagGroups = new List<TagGroup>(_tagGroups);
                }

                // Phase 4: Alignment rail discovery
                if (options.EnableAlignmentRails)
                {
                    ReportProgress(progress, "Discovering alignment rails", 0, 1, 0, 0, 0, 18);
                    await Task.Run(() => DiscoverAlignmentRails(options), cancellationToken);
                    result.AlignmentRails = _alignmentRails.Select(r => new AlignmentRailInfo
                    {
                        Position = r.Position,
                        Orientation = r.IsHorizontal ? RailOrientation.Horizontal : RailOrientation.Vertical,
                        SnappedTagCount = r.TagIds.Count, Confidence = r.Confidence,
                        TagIds = new List<string>(r.TagIds)
                    }).ToList();
                }

                // Phase 5: Compute initial energy and run simulated annealing
                double initialEnergy = ComputeTotalEnergy(options);
                result.EnergyBefore = initialEnergy;
                _bestEnergy = initialEnergy;
                _bestStates = CloneWorkingStates();

                var sa = await Task.Run(
                    () => RunSimulatedAnnealing(options, progress, cancellationToken),
                    cancellationToken);
                result.IterationsExecuted = sa.iterations;
                result.MovesAccepted = sa.accepted;
                result.UphillMovesAccepted = sa.uphillAccepted;
                result.FinalTemperature = sa.finalTemp;
                _workingStates = _bestStates;

                // Phase 6: Spacing normalization
                if (options.EnableSpacingNormalization)
                {
                    ReportProgress(progress, "Normalizing spacing", 0, 1, 0, _bestEnergy, 0, 85);
                    result.SpacingProfiles = await Task.Run(
                        () => NormalizeSpacing(options), cancellationToken);
                }

                // Phase 7: Leader optimization
                if (options.EnableLeaderOptimization)
                {
                    ReportProgress(progress, "Optimizing leaders", 0, 1, 0, _bestEnergy, 0, 92);
                    result.LeaderResult = await Task.Run(
                        () => OptimizeLeaders(options), cancellationToken);
                }

                double finalEnergy = ComputeTotalEnergy(options);
                result.EnergyAfter = finalEnergy;
                result.ImprovementPercentage = initialEnergy > 1e-10
                    ? (initialEnergy - finalEnergy) / initialEnergy * 100.0 : 0.0;
                result.AppliedMoves = BuildMoveRecords(viewId);
                ReportProgress(progress, "Complete", 0, 1, finalEnergy, finalEnergy, 0, 100);
            }
            catch (OperationCanceledException)
            {
                Logger.Warn("Layout optimization cancelled for view {ViewId}", viewId);
                throw;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Layout optimization failed for view {ViewId}", viewId);
                throw;
            }
            finally
            {
                stopwatch.Stop();
                result.Duration = stopwatch.Elapsed;
                Logger.Info("View {ViewId} optimized in {Duration}ms ({Pct:F1}% improvement)",
                    viewId, stopwatch.ElapsedMilliseconds, result.ImprovementPercentage);
            }

            return result;
        }

        /// <summary>
        /// Applies optimized positions back to tag instances in the repository.
        /// Call after reviewing the <see cref="OptimizationResult"/> to commit changes.
        /// </summary>
        public int ApplyOptimizedPositions(int viewId)
        {
            lock (_lockObject)
            {
                int updated = 0;
                var tags = _repository.GetTagsByView(viewId);
                if (tags == null) return 0;

                foreach (var tag in tags)
                {
                    if (tag?.TagId == null || !_workingStates.ContainsKey(tag.TagId))
                        continue;
                    if (tag.Placement == null) continue;

                    var state = _workingStates[tag.TagId];
                    if (state.Position.DistanceTo(tag.Placement.Position) < 0.0001) continue;

                    tag.Placement.Position = state.Position;
                    tag.Placement.LeaderType = state.LeaderType;
                    tag.Placement.LeaderElbowPoint = state.LeaderElbowPoint;
                    tag.Placement.Rotation = state.Rotation;
                    tag.Bounds = new TagBounds2D(state.Bounds.MinX, state.Bounds.MinY,
                        state.Bounds.MaxX, state.Bounds.MaxY);
                    tag.LastModified = DateTime.UtcNow;
                    updated++;
                }

                Logger.Info("Applied {Count} optimized positions to view {ViewId}", updated, viewId);
                return updated;
            }
        }

        /// <summary>Returns the density level at a specific point in the view.</summary>
        public DensityLevel GetDensityAtPoint(Point2D point)
        {
            if (_densityRegions == null || _densityRegions.Count == 0) return DensityLevel.Medium;
            var nearest = _densityRegions.OrderBy(r => r.Center.DistanceTo(point)).First();
            return nearest.Center.DistanceTo(point) <= nearest.Radius * 1.5
                ? nearest.Level : DensityLevel.Low;
        }

        #endregion

        #region Initialization and State Setup

        /// <summary>
        /// Loads all tags for the view from the repository and builds mutable working states.
        /// Locked tags participate in energy computation but are never moved.
        /// </summary>
        private void InitializeWorkingState(int viewId, OptimizationOptions options)
        {
            lock (_lockObject)
            {
                _workingStates.Clear();
                _alignmentRails.Clear();
                _densityRegions.Clear();
                _tagGroups.Clear();

                var tags = _repository.GetTagsByView(viewId);
                if (tags == null || !tags.Any()) return;

                _currentViewContext = _repository.GetViewContext(viewId);
                _currentOptions = options;

                foreach (var tag in tags)
                {
                    if (tag.State == TagState.MarkedForDeletion || tag.State == TagState.Orphaned) continue;
                    if (tag.Placement == null || tag.Bounds == null) continue;

                    _workingStates[tag.TagId] = new TagWorkingState
                    {
                        TagId = tag.TagId,
                        Position = new Point2D(tag.Placement.Position.X, tag.Placement.Position.Y),
                        Bounds = new TagBounds2D(tag.Bounds.MinX, tag.Bounds.MinY, tag.Bounds.MaxX, tag.Bounds.MaxY),
                        LeaderEndPoint = tag.Placement.LeaderEndPoint,
                        LeaderElbowPoint = tag.Placement.LeaderElbowPoint,
                        LeaderType = tag.Placement.LeaderType,
                        Rotation = tag.Placement.Rotation,
                        Width = tag.Bounds.Width, Height = tag.Bounds.Height,
                        IsLocked = options.LockManuallyPlacedTags && tag.UserAdjusted,
                        CategoryName = tag.CategoryName ?? string.Empty,
                        HostElementId = tag.HostElementId,
                        PreferredPosition = new Point2D(tag.Placement.Position.X, tag.Placement.Position.Y),
                        HostCenter = tag.Placement.LeaderEndPoint,
                        RoomName = tag.Metadata.TryGetValue("RoomName", out var rn) ? rn?.ToString() ?? "" : "",
                        SystemName = tag.Metadata.TryGetValue("SystemName", out var sn) ? sn?.ToString() ?? "" : ""
                    };
                }

                Logger.Debug("Initialized {Count} working states for view {ViewId} ({Locked} locked)",
                    _workingStates.Count, viewId, _workingStates.Values.Count(s => s.IsLocked));
            }
        }

        private Dictionary<string, TagWorkingState> CloneWorkingStates()
        {
            var clone = new Dictionary<string, TagWorkingState>(_workingStates.Count, StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in _workingStates) clone[kvp.Key] = kvp.Value.Clone();
            return clone;
        }

        #endregion

        #region Simulated Annealing Optimizer

        /// <summary>
        /// Executes simulated annealing. Five move operators explore the solution space:
        /// shift-single (40%), swap-two (20%), rotate (10%), toggle-leader (15%),
        /// adjust-leader-endpoint (15%). Uphill moves accepted with Boltzmann probability.
        /// </summary>
        private (int iterations, int accepted, int uphillAccepted, double finalTemp)
            RunSimulatedAnnealing(OptimizationOptions options,
                IProgress<OptimizationProgress> progress, CancellationToken ct)
        {
            double temperature = options.InitialTemperature;
            double currentEnergy = ComputeTotalEnergy(options);
            int accepted = 0, uphillAccepted = 0, iteration = 0;

            var movableIds = _workingStates.Where(kvp => !kvp.Value.IsLocked)
                .Select(kvp => kvp.Key).ToList();
            if (movableIds.Count == 0) return (0, 0, 0, temperature);

            // Cumulative probabilities: shift 40%, swap 20%, rotate 10%, toggleLeader 15%, adjustLeader 15%
            double[] cp = { 0.40, 0.60, 0.70, 0.85, 1.00 };
            int progressInterval = Math.Max(1, options.MaxIterations / 100);

            for (iteration = 0; iteration < options.MaxIterations; iteration++)
            {
                ct.ThrowIfCancellationRequested();
                if (temperature < options.MinTemperature) break;

                double roll = _random.NextDouble();
                TagWorkingState[] saved;
                if (roll < cp[0])      saved = ApplyShiftMove(movableIds, temperature, options);
                else if (roll < cp[1]) saved = ApplySwapMove(movableIds);
                else if (roll < cp[2]) saved = ApplyRotateMove(movableIds);
                else if (roll < cp[3]) saved = ApplyToggleLeaderMove(movableIds);
                else                   saved = ApplyAdjustLeaderMove(movableIds, temperature);

                double newEnergy = ComputeTotalEnergy(options);
                double delta = newEnergy - currentEnergy;

                // Metropolis acceptance criterion
                bool accept = delta <= 0 || _random.NextDouble() < Math.Exp(-delta / temperature);

                if (accept)
                {
                    currentEnergy = newEnergy;
                    accepted++;
                    if (delta > 0) uphillAccepted++;
                    if (currentEnergy < _bestEnergy)
                    {
                        _bestEnergy = currentEnergy;
                        _bestStates = CloneWorkingStates();
                    }
                }
                else
                {
                    foreach (var s in saved)
                        if (s != null && _workingStates.ContainsKey(s.TagId))
                            _workingStates[s.TagId] = s;
                }

                // Logarithmically-modulated geometric cooling
                temperature *= options.CoolingRate;
                double logFactor = 1.0 / (1.0 + 0.01 * Math.Log(1.0 + iteration));
                temperature = Math.Max(temperature, options.MinTemperature) * logFactor
                              + options.MinTemperature * (1.0 - logFactor);

                if (iteration % progressInterval == 0)
                    ReportProgress(progress, "Simulated annealing", iteration, options.MaxIterations,
                        currentEnergy, _bestEnergy, temperature,
                        20.0 + iteration / (double)options.MaxIterations * 65.0);
            }

            Logger.Debug("Annealing: {Iter} iterations, {Acc} accepted ({Up} uphill), " +
                        "temp={T:F6}, best={E:F4}", iteration, accepted, uphillAccepted, temperature, _bestEnergy);
            return (iteration, accepted, uphillAccepted, temperature);
        }

        /// <summary>Shift a random tag by temperature-scaled displacement.</summary>
        private TagWorkingState[] ApplyShiftMove(List<string> ids, double temp, OptimizationOptions opt)
        {
            var state = _workingStates[ids[_random.Next(ids.Count)]];
            var saved = state.Clone();

            double viewExtent = _currentViewContext?.CropRegion != null
                ? Math.Max(_currentViewContext.CropRegion.Width, _currentViewContext.CropRegion.Height) : 1.0;
            double maxDisp = Math.Max(0.001, viewExtent * 0.05 * (temp / opt.InitialTemperature));
            double angle = _random.NextDouble() * 2.0 * Math.PI;
            double mag = _random.NextDouble() * maxDisp;

            state.Position = new Point2D(state.Position.X + mag * Math.Cos(angle),
                                         state.Position.Y + mag * Math.Sin(angle));
            UpdateBounds(state);
            ConstrainToCrop(state);
            return new[] { saved };
        }

        /// <summary>Swap positions of two random tags.</summary>
        private TagWorkingState[] ApplySwapMove(List<string> ids)
        {
            if (ids.Count < 2) return Array.Empty<TagWorkingState>();
            int i1 = _random.Next(ids.Count), i2;
            do { i2 = _random.Next(ids.Count); } while (i2 == i1);

            var s1 = _workingStates[ids[i1]]; var s2 = _workingStates[ids[i2]];
            var sv1 = s1.Clone(); var sv2 = s2.Clone();
            var tmp = s1.Position; s1.Position = s2.Position; s2.Position = tmp;
            UpdateBounds(s1); UpdateBounds(s2);
            return new[] { sv1, sv2 };
        }

        /// <summary>Toggle a random tag between horizontal and vertical orientation.</summary>
        private TagWorkingState[] ApplyRotateMove(List<string> ids)
        {
            var state = _workingStates[ids[_random.Next(ids.Count)]];
            var saved = state.Clone();
            state.Rotation = Math.Abs(state.Rotation) < 0.01 ? Math.PI / 2.0 : 0.0;
            double tw = state.Width; state.Width = state.Height; state.Height = tw;
            UpdateBounds(state);
            return new[] { saved };
        }

        /// <summary>Cycle a random tag's leader: None -> Straight -> Elbow -> None.</summary>
        private TagWorkingState[] ApplyToggleLeaderMove(List<string> ids)
        {
            var state = _workingStates[ids[_random.Next(ids.Count)]];
            var saved = state.Clone();

            switch (state.LeaderType)
            {
                case LeaderType.None:
                    state.LeaderType = LeaderType.Straight;
                    var away = Vector2D.FromPoints(state.HostCenter, state.Position).Normalize();
                    if (away.Length < 1e-10) away = new Vector2D(1, 0);
                    var off = away.Scale(0.015);
                    state.Position = state.Position.Offset(off.X, off.Y);
                    UpdateBounds(state);
                    break;
                case LeaderType.Straight:
                    state.LeaderType = LeaderType.Elbow;
                    var mid = Point2D.Midpoint(state.Position, state.LeaderEndPoint);
                    state.LeaderElbowPoint = new Point2D(state.Position.X, mid.Y);
                    break;
                case LeaderType.Elbow:
                    state.LeaderType = LeaderType.None;
                    state.LeaderElbowPoint = null;
                    var tow = Vector2D.FromPoints(state.Position, state.HostCenter).Normalize();
                    var cl = tow.Scale(0.01);
                    state.Position = state.Position.Offset(cl.X, cl.Y);
                    UpdateBounds(state);
                    break;
                default:
                    state.LeaderType = LeaderType.Straight;
                    break;
            }
            return new[] { saved };
        }

        /// <summary>Perturb a random leader endpoint by a temperature-scaled offset.</summary>
        private TagWorkingState[] ApplyAdjustLeaderMove(List<string> ids, double temp)
        {
            var leaderIds = ids.Where(id => _workingStates[id].LeaderType != LeaderType.None).ToList();
            if (leaderIds.Count == 0) return Array.Empty<TagWorkingState>();

            var state = _workingStates[leaderIds[_random.Next(leaderIds.Count)]];
            var saved = state.Clone();
            double mag = Math.Max(0.001, 0.01 * (temp / (_currentOptions?.InitialTemperature ?? 100.0)));
            double angle = _random.NextDouble() * 2.0 * Math.PI;

            state.LeaderEndPoint = new Point2D(
                state.LeaderEndPoint.X + mag * Math.Cos(angle),
                state.LeaderEndPoint.Y + mag * Math.Sin(angle));
            if (state.LeaderElbowPoint.HasValue)
                state.LeaderElbowPoint = new Point2D(
                    state.Position.X, (state.Position.Y + state.LeaderEndPoint.Y) / 2.0);
            return new[] { saved };
        }

        private void UpdateBounds(TagWorkingState s)
        {
            double hw = s.Width / 2.0, hh = s.Height / 2.0;
            s.Bounds = new TagBounds2D(s.Position.X - hw, s.Position.Y - hh,
                                       s.Position.X + hw, s.Position.Y + hh);
        }

        private void ConstrainToCrop(TagWorkingState s)
        {
            if (_currentViewContext?.CropRegion == null) return;
            var c = _currentViewContext.CropRegion;
            double m = 0.005, hw = s.Width / 2.0, hh = s.Height / 2.0;
            double lx = c.MinX + m + hw, rx = c.MaxX - m - hw;
            double by = c.MinY + m + hh, ty = c.MaxY - m - hh;
            if (lx > rx) lx = rx = (c.MinX + c.MaxX) / 2.0;
            if (by > ty) by = ty = (c.MinY + c.MaxY) / 2.0;
            s.Position = new Point2D(Math.Max(lx, Math.Min(rx, s.Position.X)),
                                     Math.Max(by, Math.Min(ty, s.Position.Y)));
            UpdateBounds(s);
        }

        #endregion

        #region Energy Function

        /// <summary>
        /// Total energy = weighted sum of 5 components: collision, leader length,
        /// alignment deviation, preferred position distance, readability.
        /// </summary>
        private double ComputeTotalEnergy(OptimizationOptions o)
        {
            return o.CollisionWeight * CollisionEnergy()
                 + o.LeaderLengthWeight * LeaderEnergy()
                 + o.AlignmentWeight * AlignmentEnergy()
                 + o.PreferredPositionWeight * PreferredEnergy()
                 + o.ReadabilityWeight * ReadabilityEnergy();
        }

        private double CollisionEnergy()
        {
            double total = 0;
            var states = _workingStates.Values.ToList();
            double cl = _currentOptions?.MinimumClearance ?? 0.002;

            for (int i = 0; i < states.Count; i++)
            {
                var bi = states[i].Bounds.Expand(cl);
                for (int j = i + 1; j < states.Count; j++)
                {
                    var bj = states[j].Bounds.Expand(cl);
                    if (bi.Intersects(bj)) total += bi.OverlapArea(bj);
                }
                if (_currentViewContext?.ExistingAnnotationBounds != null)
                    foreach (var ab in _currentViewContext.ExistingAnnotationBounds)
                        if (bi.Intersects(ab)) total += bi.OverlapArea(ab) * 0.5;
            }
            return total;
        }

        private double LeaderEnergy()
        {
            double total = 0;
            foreach (var s in _workingStates.Values)
            {
                if (s.LeaderType == LeaderType.None) continue;
                total += (s.LeaderType == LeaderType.Elbow && s.LeaderElbowPoint.HasValue)
                    ? s.Position.DistanceTo(s.LeaderElbowPoint.Value) + s.LeaderElbowPoint.Value.DistanceTo(s.LeaderEndPoint)
                    : s.Position.DistanceTo(s.LeaderEndPoint);
            }
            return total;
        }

        private double AlignmentEnergy()
        {
            if (_alignmentRails == null || _alignmentRails.Count == 0) return 0;
            double total = 0;
            double snapRange = (_currentOptions?.RailSnapTolerance ?? 0.006) * 3.0;

            foreach (var s in _workingStates.Values)
            {
                double minDev = double.MaxValue;
                bool near = false;
                foreach (var r in _alignmentRails)
                {
                    double dev = Math.Abs((r.IsHorizontal ? s.Position.Y : s.Position.X) - r.Position);
                    if (dev < snapRange) { near = true; if (dev < minDev) minDev = dev; }
                }
                if (near) total += minDev * minDev; // Quadratic penalty
            }
            return total;
        }

        private double PreferredEnergy()
        {
            double total = 0;
            foreach (var s in _workingStates.Values)
                total += s.Position.DistanceTo(s.PreferredPosition);
            return total;
        }

        private double ReadabilityEnergy()
        {
            double total = 0;
            double minAngle = (_currentOptions?.MinLeaderAngleDegrees ?? 15.0) * Math.PI / 180.0;

            foreach (var s in _workingStates.Values)
            {
                // Vertical rotation penalty
                if (Math.Abs(s.Rotation - Math.PI / 2.0) < 0.1) total += 0.3;

                // Leader angle penalty
                if (s.LeaderType != LeaderType.None)
                {
                    var v = Vector2D.FromPoints(s.LeaderEndPoint, s.Position);
                    if (v.Length > 1e-10)
                    {
                        double a = Math.Abs(Math.Atan2(v.Y, v.X));
                        double fromH = Math.Min(a, Math.PI - a);
                        double fromV = Math.Abs(a - Math.PI / 2.0);
                        double minFromAxis = Math.Min(fromH, fromV);
                        if (minFromAxis < minAngle)
                            total += (minAngle - minFromAxis) / minAngle * 0.5;
                    }
                }

                // Edge proximity penalty
                if (_currentViewContext?.CropRegion != null)
                {
                    var cr = _currentViewContext.CropRegion;
                    double minE = Math.Min(
                        Math.Min(s.Bounds.MinX - cr.MinX, cr.MaxX - s.Bounds.MaxX),
                        Math.Min(s.Bounds.MinY - cr.MinY, cr.MaxY - s.Bounds.MaxY));
                    if (minE < 0) total += 1.0;
                    else if (minE < 0.01) total += (1.0 - minE / 0.01) * 0.25;
                }
            }
            return total;
        }

        #endregion

        #region Alignment Rail System

        /// <summary>
        /// Detects implicit alignment rails via histogram binning of tag coordinates.
        /// Horizontal rails from Y-coordinate peaks, vertical rails from X-coordinate peaks.
        /// </summary>
        private void DiscoverAlignmentRails(OptimizationOptions options)
        {
            lock (_lockObject)
            {
                _alignmentRails.Clear();
                var states = _workingStates.Values.ToList();
                if (states.Count < options.MinRailTagCount) return;

                _alignmentRails.AddRange(DetectRails(states, true, options));
                _alignmentRails.AddRange(DetectRails(states, false, options));
                SnapTagsToRails(options);
                DetectNearAlignmentOpportunities(options);

                Logger.Debug("Discovered {Count} rails ({H}H, {V}V)", _alignmentRails.Count,
                    _alignmentRails.Count(r => r.IsHorizontal), _alignmentRails.Count(r => !r.IsHorizontal));
            }
        }

        /// <summary>Histogram binning + peak detection for one axis.</summary>
        private List<InternalAlignmentRail> DetectRails(
            List<TagWorkingState> states, bool isH, OptimizationOptions opt)
        {
            var rails = new List<InternalAlignmentRail>();
            var coords = states.Select(s => (Val: isH ? s.Position.Y : s.Position.X, s.TagId))
                               .OrderBy(c => c.Val).ToList();

            if (coords.Count < opt.MinRailTagCount) return rails;
            double binW = opt.RailToleranceBand;
            double minV = coords.First().Val, maxV = coords.Last().Val;

            if (maxV - minV < binW)
            {
                if (coords.Count >= opt.MinRailTagCount)
                    rails.Add(new InternalAlignmentRail
                    {
                        Position = coords.Average(c => c.Val), IsHorizontal = isH,
                        TagIds = coords.Select(c => c.TagId).ToList(),
                        Confidence = Math.Min(1.0, coords.Count / 10.0)
                    });
                return rails;
            }

            int binCount = Math.Max(1, (int)Math.Ceiling((maxV - minV) / binW));
            var bins = Enumerable.Range(0, binCount + 1)
                .Select(_ => new List<(double Val, string TagId)>()).ToList();

            foreach (var c in coords)
                bins[Math.Min(binCount, (int)((c.Val - minV) / binW))].Add(c);

            var visited = new bool[bins.Count];
            for (int i = 0; i < bins.Count; i++)
            {
                if (visited[i]) continue;
                var peak = new List<(double Val, string TagId)>();
                int j = i;
                while (j < bins.Count && (bins[j].Count > 0 ||
                    (j > i && j + 1 < bins.Count && bins[j + 1].Count > 0)))
                {
                    peak.AddRange(bins[j]); visited[j] = true; j++;
                    if (j < bins.Count && bins[j].Count == 0 &&
                        !(j + 1 < bins.Count && bins[j + 1].Count > 0)) break;
                }
                if (peak.Count >= opt.MinRailTagCount)
                    rails.Add(new InternalAlignmentRail
                    {
                        Position = peak.Average(t => t.Val), IsHorizontal = isH,
                        TagIds = peak.Select(t => t.TagId).ToList(),
                        Confidence = Math.Min(1.0, peak.Count / 10.0)
                    });
            }
            return rails;
        }

        /// <summary>Snap tags within tolerance to their nearest rail.</summary>
        private void SnapTagsToRails(OptimizationOptions opt)
        {
            foreach (var rail in _alignmentRails)
                foreach (var s in _workingStates.Values)
                {
                    if (s.IsLocked) continue;
                    double coord = rail.IsHorizontal ? s.Position.Y : s.Position.X;
                    double dev = Math.Abs(coord - rail.Position);
                    if (dev <= opt.RailSnapTolerance && dev > 1e-10)
                    {
                        s.Position = rail.IsHorizontal
                            ? new Point2D(s.Position.X, rail.Position)
                            : new Point2D(rail.Position, s.Position.Y);
                        UpdateBounds(s);
                        if (!rail.TagIds.Contains(s.TagId)) rail.TagIds.Add(s.TagId);
                    }
                }
        }

        /// <summary>Create rails for tags that are almost aligned but missed histogram peaks.</summary>
        private void DetectNearAlignmentOpportunities(OptimizationOptions opt)
        {
            var unrailed = _workingStates.Values
                .Where(s => !s.IsLocked && !_alignmentRails.Any(r => r.TagIds.Contains(s.TagId)))
                .ToList();

            // Try horizontal and vertical near-alignments
            foreach (bool isH in new[] { true, false })
            {
                for (int i = 0; i < unrailed.Count; i++)
                {
                    var cands = new List<TagWorkingState> { unrailed[i] };
                    double refCoord = isH ? unrailed[i].Position.Y : unrailed[i].Position.X;

                    for (int j = i + 1; j < unrailed.Count; j++)
                    {
                        double otherCoord = isH ? unrailed[j].Position.Y : unrailed[j].Position.X;
                        if (Math.Abs(refCoord - otherCoord) < opt.RailToleranceBand * 2.0)
                            cands.Add(unrailed[j]);
                    }

                    if (cands.Count < 2) continue;
                    double avg = isH ? cands.Average(c => c.Position.Y) : cands.Average(c => c.Position.X);

                    bool dup = _alignmentRails.Any(r =>
                        r.IsHorizontal == isH && Math.Abs(r.Position - avg) < opt.RailToleranceBand);
                    if (dup) continue;

                    _alignmentRails.Add(new InternalAlignmentRail
                    {
                        Position = avg, IsHorizontal = isH,
                        TagIds = cands.Select(c => c.TagId).ToList(),
                        Confidence = Math.Min(1.0, cands.Count / 8.0)
                    });

                    foreach (var c in cands.Where(c => !c.IsLocked))
                    {
                        c.Position = isH ? new Point2D(c.Position.X, avg) : new Point2D(avg, c.Position.Y);
                        UpdateBounds(c);
                    }
                }
            }
        }

        #endregion

        #region Spacing Normalizer

        /// <summary>
        /// Normalizes spacing between adjacent tags on each alignment rail and within
        /// tag groups. Computes ideal spacing from tag size, view scale, and local density.
        /// </summary>
        private List<SpacingProfile> NormalizeSpacing(OptimizationOptions opt)
        {
            var profiles = new List<SpacingProfile>();
            foreach (var rail in _alignmentRails.Where(r => r.TagIds.Count >= 2))
            {
                var p = NormalizeRailSpacing(rail, opt);
                if (p != null) profiles.Add(p);
            }
            foreach (var group in _tagGroups)
            {
                int onRail = group.MemberTagIds.Count(id =>
                    _alignmentRails.Any(r => r.TagIds.Contains(id)));
                if (onRail > group.MemberTagIds.Count / 2) continue;
                var p = NormalizeGroupSpacing(group, opt);
                if (p != null) profiles.Add(p);
            }
            return profiles;
        }

        private SpacingProfile NormalizeRailSpacing(InternalAlignmentRail rail, OptimizationOptions opt)
        {
            var tags = rail.TagIds.Where(id => _workingStates.ContainsKey(id) && !_workingStates[id].IsLocked)
                .Select(id => _workingStates[id]).ToList();
            if (tags.Count < 2) return null;

            tags = rail.IsHorizontal ? tags.OrderBy(s => s.Position.X).ToList()
                                     : tags.OrderBy(s => s.Position.Y).ToList();

            var spacings = new List<double>();
            for (int i = 0; i < tags.Count - 1; i++)
                spacings.Add(Math.Abs(rail.IsHorizontal
                    ? tags[i + 1].Position.X - tags[i].Position.X
                    : tags[i + 1].Position.Y - tags[i].Position.Y));
            if (spacings.Count == 0) return null;

            double mean = spacings.Average(), min = spacings.Min(), max = spacings.Max();
            double var_ = spacings.Count > 1 ? spacings.Sum(s => (s - mean) * (s - mean)) / (spacings.Count - 1) : 0;
            double stdDev = Math.Sqrt(var_);

            double avgExt = rail.IsHorizontal ? tags.Average(s => s.Width) : tags.Average(s => s.Height);
            double scale = Math.Max(0.5, Math.Min(2.0, (_currentViewContext?.Scale ?? 100) / 100.0));
            double ideal = (avgExt + opt.MinimumClearance * 4.0) * scale;

            // Density adjustment
            var center = new Point2D(tags.Average(s => s.Position.X), tags.Average(s => s.Position.Y));
            var nearest = _densityRegions.OrderBy(d => d.Center.DistanceTo(center)).FirstOrDefault();
            if (nearest != null)
                ideal *= nearest.Level == DensityLevel.VeryHigh ? 0.7
                       : nearest.Level == DensityLevel.High ? 0.85
                       : nearest.Level == DensityLevel.Low ? 1.2 : 1.0;

            // Only normalize if spacing is significantly non-uniform
            if (stdDev > ideal * 0.15 || Math.Abs(mean - ideal) > ideal * 0.3)
            {
                double totalSpan = ideal * (tags.Count - 1);
                double start = rail.IsHorizontal ? tags.First().Position.X : tags.First().Position.Y;
                double end = rail.IsHorizontal ? tags.Last().Position.X : tags.Last().Position.Y;
                double mid = (start + end) / 2.0;
                double newStart = mid - totalSpan / 2.0;

                for (int i = 0; i < tags.Count; i++)
                {
                    double nc = newStart + i * ideal;
                    tags[i].Position = rail.IsHorizontal
                        ? new Point2D(nc, tags[i].Position.Y)
                        : new Point2D(tags[i].Position.X, nc);
                    UpdateBounds(tags[i]);
                    ConstrainToCrop(tags[i]);
                }
            }

            return new SpacingProfile
            {
                GroupId = $"Rail_{(rail.IsHorizontal ? "H" : "V")}_{rail.Position:F4}",
                MinSpacing = min, MaxSpacing = max, MeanSpacing = mean,
                StdDevSpacing = stdDev, IdealSpacing = ideal, PairCount = spacings.Count
            };
        }

        private SpacingProfile NormalizeGroupSpacing(TagGroup group, OptimizationOptions opt)
        {
            var tags = group.MemberTagIds.Where(id => _workingStates.ContainsKey(id) && !_workingStates[id].IsLocked)
                .Select(id => _workingStates[id]).ToList();
            if (tags.Count < 2) return null;

            double xExt = tags.Max(s => s.Position.X) - tags.Min(s => s.Position.X);
            double yExt = tags.Max(s => s.Position.Y) - tags.Min(s => s.Position.Y);
            bool isH = xExt >= yExt;

            tags = isH ? tags.OrderBy(s => s.Position.X).ToList() : tags.OrderBy(s => s.Position.Y).ToList();

            var spacings = new List<double>();
            for (int i = 0; i < tags.Count - 1; i++)
                spacings.Add(Math.Abs(isH ? tags[i + 1].Position.X - tags[i].Position.X
                                          : tags[i + 1].Position.Y - tags[i].Position.Y));
            if (spacings.Count == 0) return null;

            double mean = spacings.Average();
            double var_ = spacings.Count > 1 ? spacings.Sum(s => (s - mean) * (s - mean)) / (spacings.Count - 1) : 0;
            double avgExt = isH ? tags.Average(s => s.Width) : tags.Average(s => s.Height);

            return new SpacingProfile
            {
                GroupId = group.GroupId, MinSpacing = spacings.Min(), MaxSpacing = spacings.Max(),
                MeanSpacing = mean, StdDevSpacing = Math.Sqrt(var_),
                IdealSpacing = avgExt + opt.MinimumClearance * 4.0, PairCount = spacings.Count
            };
        }

        #endregion

        #region Leader Route Optimizer

        /// <summary>
        /// Optimizes leader lines: eliminate crossings, enforce minimum angles,
        /// reroute leaders that pass through other tags.
        /// </summary>
        private LeaderOptimizationResult OptimizeLeaders(OptimizationOptions opt)
        {
            var result = new LeaderOptimizationResult();
            var leaders = _workingStates.Values.Where(s => s.LeaderType != LeaderType.None).ToList();
            if (leaders.Count < 2)
            {
                result.TotalLengthBefore = result.TotalLengthAfter = leaders.Sum(LeaderLen);
                return result;
            }

            result.TotalLengthBefore = leaders.Sum(LeaderLen);
            result.CrossingsEliminated = EliminateCrossings(leaders);
            result.AngleViolationsFixed = FixAngleViolations(leaders, opt);
            result.LeadersRerouted = RerouteOccludedLeaders(leaders);
            result.TotalLengthAfter = leaders.Sum(LeaderLen);

            Logger.Debug("Leaders: {X} crossings, {A} angles, {R} rerouted, length delta={D:F4}",
                result.CrossingsEliminated, result.AngleViolationsFixed,
                result.LeadersRerouted, result.LengthReduction);
            return result;
        }

        private double LeaderLen(TagWorkingState s)
        {
            if (s.LeaderType == LeaderType.None) return 0;
            return (s.LeaderType == LeaderType.Elbow && s.LeaderElbowPoint.HasValue)
                ? s.Position.DistanceTo(s.LeaderElbowPoint.Value) + s.LeaderElbowPoint.Value.DistanceTo(s.LeaderEndPoint)
                : s.Position.DistanceTo(s.LeaderEndPoint);
        }

        /// <summary>Swap tags to uncross leaders, up to 10 passes.</summary>
        private int EliminateCrossings(List<TagWorkingState> leaders)
        {
            int fixed_ = 0;
            bool changed = true;

            for (int pass = 0; pass < 10 && changed; pass++)
            {
                changed = false;
                for (int i = 0; i < leaders.Count; i++)
                    for (int j = i + 1; j < leaders.Count; j++)
                    {
                        var a = leaders[i]; var b = leaders[j];
                        if ((a.IsLocked && b.IsLocked) || !SegmentsIntersect(a.Position, a.LeaderEndPoint, b.Position, b.LeaderEndPoint))
                            continue;

                        double cur = LeaderLen(a) + LeaderLen(b);
                        var savA = a.Clone(); var savB = b.Clone();

                        if (!a.IsLocked && !b.IsLocked)
                        { var t = a.Position; a.Position = b.Position; b.Position = t; UpdateBounds(a); UpdateBounds(b); }
                        else if (!a.IsLocked)
                        { a.Position = ReflectAway(a, b); UpdateBounds(a); }
                        else
                        { b.Position = ReflectAway(b, a); UpdateBounds(b); }

                        if (!SegmentsIntersect(a.Position, a.LeaderEndPoint, b.Position, b.LeaderEndPoint)
                            && LeaderLen(a) + LeaderLen(b) < cur * 1.5)
                        { fixed_++; changed = true; }
                        else
                        { _workingStates[a.TagId] = savA; _workingStates[b.TagId] = savB;
                          leaders[i] = savA; leaders[j] = savB; }
                    }
            }
            return fixed_;
        }

        private Point2D ReflectAway(TagWorkingState movable, TagWorkingState anchor)
        {
            var dir = Vector2D.FromPoints(anchor.LeaderEndPoint, anchor.Position).Normalize();
            var perp = new Vector2D(-dir.Y, dir.X);
            var toEnd = Vector2D.FromPoints(anchor.Position, movable.LeaderEndPoint);
            double endSide = perp.X * toEnd.X + perp.Y * toEnd.Y;
            var toMov = Vector2D.FromPoints(anchor.Position, movable.Position);
            double movSide = perp.X * toMov.X + perp.Y * toMov.Y;

            if (endSide * movSide < 0)
            {
                double dist = 2.0 * Math.Abs(movSide);
                var shift = perp.Scale(endSide > 0 ? dist : -dist);
                return movable.Position.Offset(shift.X, shift.Y);
            }
            return movable.Position;
        }

        private int FixAngleViolations(List<TagWorkingState> leaders, OptimizationOptions opt)
        {
            int fixed_ = 0;
            double minA = opt.MinLeaderAngleDegrees * Math.PI / 180.0;

            foreach (var s in leaders.Where(s => !s.IsLocked))
            {
                var v = Vector2D.FromPoints(s.LeaderEndPoint, s.Position);
                if (v.Length < 1e-10) continue;
                double angle = Math.Atan2(Math.Abs(v.Y), Math.Abs(v.X));

                if (angle < minA)
                {
                    double reqY = Math.Abs(v.X) * Math.Tan(minA);
                    s.Position = new Point2D(s.Position.X, s.LeaderEndPoint.Y + (v.Y >= 0 ? reqY : -reqY));
                    UpdateBounds(s); ConstrainToCrop(s); fixed_++;
                }
                else if (angle > Math.PI / 2.0 - minA)
                {
                    double reqX = Math.Abs(v.Y) * Math.Tan(minA);
                    s.Position = new Point2D(s.LeaderEndPoint.X + (v.X >= 0 ? reqX : -reqX), s.Position.Y);
                    UpdateBounds(s); ConstrainToCrop(s); fixed_++;
                }
            }
            return fixed_;
        }

        private int RerouteOccludedLeaders(List<TagWorkingState> leaders)
        {
            int rerouted = 0;
            foreach (var s in leaders.Where(s => !s.IsLocked))
            {
                bool occluded = _workingStates.Values.Any(o =>
                    o.TagId != s.TagId && LeaderHitsBounds(s.Position, s.LeaderEndPoint, o.Bounds));
                if (!occluded) continue;

                var best = FindClearPosition(s);
                if (best.DistanceTo(s.Position) > 1e-10)
                { s.Position = best; UpdateBounds(s); ConstrainToCrop(s); rerouted++; }
            }
            return rerouted;
        }

        /// <summary>Liang-Barsky line-rect intersection test.</summary>
        private bool LeaderHitsBounds(Point2D p1, Point2D p2, TagBounds2D b)
        {
            double dx = p2.X - p1.X, dy = p2.Y - p1.Y;
            double[] p = { -dx, dx, -dy, dy };
            double[] q = { p1.X - b.MinX, b.MaxX - p1.X, p1.Y - b.MinY, b.MaxY - p1.Y };

            double tMin = 0, tMax = 1;
            for (int i = 0; i < 4; i++)
            {
                if (Math.Abs(p[i]) < 1e-10) { if (q[i] < 0) return false; }
                else { double t = q[i] / p[i]; if (p[i] < 0) { if (t > tMin) tMin = t; } else { if (t < tMax) tMax = t; } }
            }
            return tMin < tMax - 0.01 && tMax > 0.01 && tMin < 0.99;
        }

        private Point2D FindClearPosition(TagWorkingState s)
        {
            double step = Math.Max(s.Width, s.Height) * 0.5;
            double bestScore = double.MaxValue;
            var bestPos = s.Position;

            for (int d = 0; d < 8; d++)
            {
                double a = d * Math.PI / 4.0;
                for (int dist = 1; dist <= 4; dist++)
                {
                    var cp = s.Position.Offset(Math.Cos(a) * step * dist, Math.Sin(a) * step * dist);
                    int occ = _workingStates.Values.Count(o =>
                        o.TagId != s.TagId && LeaderHitsBounds(cp, s.LeaderEndPoint, o.Bounds));
                    double score = occ * 10.0 + cp.DistanceTo(s.LeaderEndPoint) + cp.DistanceTo(s.PreferredPosition) * 0.5;
                    if (score < bestScore) { bestScore = score; bestPos = cp; }
                }
            }
            return bestPos;
        }

        private bool SegmentsIntersect(Point2D p1, Point2D p2, Point2D p3, Point2D p4)
        {
            double d1 = Cross(p3, p4, p1), d2 = Cross(p3, p4, p2);
            double d3 = Cross(p1, p2, p3), d4 = Cross(p1, p2, p4);
            if (((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) &&
                ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0))) return true;
            if (Math.Abs(d1) < 1e-10 && OnSeg(p3, p4, p1)) return true;
            if (Math.Abs(d2) < 1e-10 && OnSeg(p3, p4, p2)) return true;
            if (Math.Abs(d3) < 1e-10 && OnSeg(p1, p2, p3)) return true;
            if (Math.Abs(d4) < 1e-10 && OnSeg(p1, p2, p4)) return true;
            return false;
        }

        private double Cross(Point2D a, Point2D b, Point2D c) =>
            (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);

        private bool OnSeg(Point2D a, Point2D b, Point2D p) =>
            Math.Min(a.X, b.X) <= p.X + 1e-10 && p.X <= Math.Max(a.X, b.X) + 1e-10 &&
            Math.Min(a.Y, b.Y) <= p.Y + 1e-10 && p.Y <= Math.Max(a.Y, b.Y) + 1e-10;

        #endregion

        #region Density-Based Layout Adaptation

        /// <summary>
        /// Kernel Density Estimation on host element positions. Classifies view
        /// regions as Low/Medium/High/VeryHigh for adaptive placement tightness.
        /// </summary>
        private void AnalyzeDensity(OptimizationOptions opt)
        {
            lock (_lockObject)
            {
                _densityRegions.Clear();
                var positions = _workingStates.Values.Select(s => s.HostCenter).ToList();
                if (positions.Count < 3) return;

                double bw = Math.Max(opt.DensityKernelBandwidth, 0.01);
                double minX = positions.Min(p => p.X), maxX = positions.Max(p => p.X);
                double minY = positions.Min(p => p.Y), maxY = positions.Max(p => p.Y);
                if (maxX - minX < 1e-10 && maxY - minY < 1e-10) return;

                int res = 8;
                double cw = Math.Max((maxX - minX) / res, bw);
                double ch = Math.Max((maxY - minY) / res, bw);
                int cols = Math.Max(1, (int)Math.Ceiling((maxX - minX) / cw));
                int rows = Math.Max(1, (int)Math.Ceiling((maxY - minY) / ch));

                var densities = new double[rows, cols];
                double maxD = 0;

                for (int r = 0; r < rows; r++)
                    for (int c = 0; c < cols; c++)
                    {
                        var ep = new Point2D(minX + (c + 0.5) * cw, minY + (r + 0.5) * ch);
                        double d = 0;
                        foreach (var p in positions)
                        { double u = ep.DistanceTo(p) / bw; d += Math.Exp(-0.5 * u * u); }
                        d /= positions.Count * bw * bw * 2.0 * Math.PI;
                        densities[r, c] = d;
                        if (d > maxD) maxD = d;
                    }

                if (maxD < 1e-10) return;
                double tLow = maxD * 0.2, tMed = maxD * 0.45, tHigh = maxD * 0.7;

                for (int r = 0; r < rows; r++)
                    for (int c = 0; c < cols; c++)
                    {
                        double d = densities[r, c];
                        if (d < tLow * 0.5) continue;
                        var cc = new Point2D(minX + (c + 0.5) * cw, minY + (r + 0.5) * ch);
                        double rad = Math.Max(cw, ch) / 2.0;
                        int cnt = positions.Count(p => p.DistanceTo(cc) < rad * 1.5);
                        DensityLevel lev = d >= tHigh ? DensityLevel.VeryHigh
                            : d >= tMed ? DensityLevel.High : d >= tLow ? DensityLevel.Medium : DensityLevel.Low;
                        _densityRegions.Add(new DensityRegion
                            { Center = cc, Radius = rad, Level = lev, ElementCount = cnt, RawDensity = d });
                    }

                MergeDensityRegions(cw, ch);
                Logger.Debug("Density: {N} regions ({VH}VH {H}H {M}M {L}L)", _densityRegions.Count,
                    _densityRegions.Count(r => r.Level == DensityLevel.VeryHigh),
                    _densityRegions.Count(r => r.Level == DensityLevel.High),
                    _densityRegions.Count(r => r.Level == DensityLevel.Medium),
                    _densityRegions.Count(r => r.Level == DensityLevel.Low));
            }
        }

        private void MergeDensityRegions(double cw, double ch)
        {
            double mergeDist = Math.Max(cw, ch) * 1.5;
            var merged = new List<DensityRegion>();
            var used = new bool[_densityRegions.Count];

            for (int i = 0; i < _densityRegions.Count; i++)
            {
                if (used[i]) continue;
                var group = new List<DensityRegion> { _densityRegions[i] };
                used[i] = true;

                for (int j = i + 1; j < _densityRegions.Count; j++)
                {
                    if (used[j]) continue;
                    if (_densityRegions[j].Level == _densityRegions[i].Level &&
                        _densityRegions[j].Center.DistanceTo(_densityRegions[i].Center) < mergeDist)
                    { group.Add(_densityRegions[j]); used[j] = true; }
                }

                if (group.Count == 1) { merged.Add(group[0]); continue; }
                double ax = group.Average(r => r.Center.X), ay = group.Average(r => r.Center.Y);
                var ct = new Point2D(ax, ay);
                merged.Add(new DensityRegion
                {
                    Center = ct, Level = group[0].Level,
                    Radius = group.Max(r => r.Center.DistanceTo(ct) + r.Radius),
                    ElementCount = group.Sum(r => r.ElementCount),
                    RawDensity = group.Average(r => r.RawDensity)
                });
            }
            _densityRegions = merged;
        }

        #endregion

        #region Tag Grouping Optimizer

        /// <summary>
        /// Groups related tags by shared system, room, host element, or category proximity.
        /// Groups are used for visual coherence: consistent alignment and spacing.
        /// </summary>
        private void DetectTagGroups()
        {
            lock (_lockObject)
            {
                _tagGroups.Clear();
                var all = _workingStates.Values.ToList();

                // Group by system name
                foreach (var g in all.Where(s => !string.IsNullOrEmpty(s.SystemName))
                    .GroupBy(s => s.SystemName).Where(g => g.Count() >= 2))
                    _tagGroups.Add(MakeGroup($"Sys_{g.Key}", TagGroupType.SameSystem,
                        g.Select(s => s.TagId).ToList(), g.Key));

                // Group by room
                foreach (var g in all.Where(s => !string.IsNullOrEmpty(s.RoomName))
                    .GroupBy(s => s.RoomName).Where(g => g.Count() >= 2))
                {
                    var ids = g.Select(s => s.TagId).ToList();
                    if (ids.All(id => _tagGroups.Any(tg => tg.GroupType == TagGroupType.SameSystem
                        && tg.MemberTagIds.Contains(id)))) continue;
                    _tagGroups.Add(MakeGroup($"Room_{g.Key}", TagGroupType.SameRoom, ids, g.Key));
                }

                // Group by host element
                foreach (var g in all.Where(s => s.HostElementId > 0)
                    .GroupBy(s => s.HostElementId).Where(g => g.Count() >= 2))
                    _tagGroups.Add(MakeGroup($"Host_{g.Key}", TagGroupType.SameHost,
                        g.Select(s => s.TagId).ToList(), $"Element {g.Key}"));

                // Category + proximity for remaining ungrouped tags
                var ungrouped = all.Where(s => !_tagGroups.Any(tg => tg.MemberTagIds.Contains(s.TagId)));
                foreach (var catGroup in ungrouped.GroupBy(s => s.CategoryName).Where(g => g.Count() >= 2))
                    foreach (var proxGroup in ClusterByProximity(catGroup.ToList()).Where(c => c.Count >= 2))
                        _tagGroups.Add(MakeGroup($"Cat_{catGroup.Key}_{_tagGroups.Count}",
                            TagGroupType.SameCategory, proxGroup.Select(s => s.TagId).ToList(), catGroup.Key));

                Logger.Debug("Groups: {N} ({S} system, {R} room, {H} host, {C} category)", _tagGroups.Count,
                    _tagGroups.Count(g => g.GroupType == TagGroupType.SameSystem),
                    _tagGroups.Count(g => g.GroupType == TagGroupType.SameRoom),
                    _tagGroups.Count(g => g.GroupType == TagGroupType.SameHost),
                    _tagGroups.Count(g => g.GroupType == TagGroupType.SameCategory));
            }
        }

        private TagGroup MakeGroup(string id, TagGroupType type, List<string> memberIds, string shared)
        {
            var pos = memberIds.Where(i => _workingStates.ContainsKey(i))
                .Select(i => _workingStates[i].Position).ToList();
            double minX = pos.Min(p => p.X), maxX = pos.Max(p => p.X);
            double minY = pos.Min(p => p.Y), maxY = pos.Max(p => p.Y);
            bool isH = (maxX - minX) >= (maxY - minY);

            return new TagGroup
            {
                GroupId = id, GroupType = type, MemberTagIds = memberIds,
                GroupBounds = new TagBounds2D(minX, minY, maxX, maxY),
                AlignmentAxisPosition = isH ? pos.Average(p => p.Y) : pos.Average(p => p.X),
                IsHorizontalAlignment = isH, SharedProperty = shared
            };
        }

        /// <summary>Single-linkage clustering of tags by spatial proximity.</summary>
        private List<List<TagWorkingState>> ClusterByProximity(List<TagWorkingState> states)
        {
            double threshold = 0.08;
            if (_currentViewContext?.CropRegion != null)
            {
                var cr = _currentViewContext.CropRegion;
                threshold = Math.Sqrt(cr.Width * cr.Width + cr.Height * cr.Height) * 0.1;
            }

            var clusters = new List<List<TagWorkingState>>();
            var assigned = new bool[states.Count];

            for (int i = 0; i < states.Count; i++)
            {
                if (assigned[i]) continue;
                var cluster = new List<TagWorkingState> { states[i] };
                assigned[i] = true;
                var queue = new Queue<int>();
                queue.Enqueue(i);

                while (queue.Count > 0)
                {
                    int cur = queue.Dequeue();
                    for (int j = 0; j < states.Count; j++)
                    {
                        if (assigned[j]) continue;
                        if (states[cur].Position.DistanceTo(states[j].Position) <= threshold)
                        { cluster.Add(states[j]); assigned[j] = true; queue.Enqueue(j); }
                    }
                }
                clusters.Add(cluster);
            }
            return clusters;
        }

        #endregion

        #region Helpers

        private List<TagMoveRecord> BuildMoveRecords(int viewId)
        {
            var records = new List<TagMoveRecord>();
            var tags = _repository.GetTagsByView(viewId);
            if (tags == null) return records;

            var lookup = tags.Where(t => t?.TagId != null)
                .ToDictionary(t => t.TagId, StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in _workingStates)
            {
                if (!lookup.TryGetValue(kvp.Key, out var orig) || orig.Placement == null) continue;
                var s = kvp.Value;
                if (s.Position.DistanceTo(orig.Placement.Position) <= 0.0001) continue;

                MoveType mt = orig.Placement.LeaderType != s.LeaderType ? MoveType.ToggleLeader
                    : Math.Abs(orig.Placement.Rotation - s.Rotation) > 0.01 ? MoveType.RotateTag
                    : _alignmentRails.Any(r => r.TagIds.Contains(s.TagId)) ? MoveType.SnapToRail
                    : MoveType.ShiftSingle;

                records.Add(new TagMoveRecord
                {
                    TagId = kvp.Key, OriginalPosition = orig.Placement.Position,
                    NewPosition = s.Position, MoveType = mt
                });
            }
            return records;
        }

        private void ReportProgress(IProgress<OptimizationProgress> progress,
            string phase, int current, int total, double curE, double bestE, double temp, double pct)
        {
            progress?.Report(new OptimizationProgress
            {
                Phase = phase, CurrentIteration = current, TotalIterations = total,
                CurrentEnergy = curE, BestEnergy = bestE, CurrentTemperature = temp, PercentComplete = pct
            });
        }

        #endregion
    }
}
