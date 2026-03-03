// StingBIM.AI.Tagging - SuperIntelligent Tagging System
// TagIntelligenceEngine.cs - ML-based learning engine for optimal tag placement prediction
// Learns from user corrections to predict ideal positions - UNIQUE to StingBIM
//
// No competitor offers adaptive tag placement learning:
// - Smart Annotation: Static rule-based only
// - Naviate: Fixed priority templates, no learning
// - Ideate: No placement intelligence at all
// - BIMLOGIQ: Strict/Relaxed modes only, no adaptation
//
// StingBIM learns from every user correction and converges on the user's preferred
// placement style per category, per view type, per scale, per user.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using StingBIM.AI.Tagging.Data;
using StingBIM.AI.Tagging.Models;

namespace StingBIM.AI.Tagging.Intelligence
{
    /// <summary>
    /// ML-based learning engine that observes user corrections to automated tag placements
    /// and builds statistical models to predict optimal placements for future tags.
    ///
    /// <para>
    /// The engine maintains a per-(category, viewType) pattern library. Each pattern
    /// accumulates observations from user corrections, extracts the statistical mode
    /// of preferred positions, weighted-average offsets, and preferred leader types.
    /// Confidence grows asymptotically toward 1.0 with each reinforcing observation
    /// and decays over time if not reinforced.
    /// </para>
    ///
    /// <para>
    /// Context-aware adjustments account for view scale, element density, and per-user
    /// preference profiles. Patterns persist across sessions via <see cref="TagRepository"/>.
    /// </para>
    ///
    /// <para>
    /// This capability is unique to StingBIM. No competing Revit annotation tool offers
    /// adaptive placement learning from user behavior.
    /// </para>
    /// </summary>
    public class TagIntelligenceEngine
    {
        #region Private Fields

        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Learned placement patterns keyed by pattern ID (CategoryName|ViewType).
        /// </summary>
        private readonly ConcurrentDictionary<string, PlacementPattern> _patterns;

        /// <summary>
        /// Accumulated corrections keyed by pattern key (CategoryName|ViewType).
        /// </summary>
        private readonly ConcurrentDictionary<string, List<PlacementCorrection>> _corrections;

        /// <summary>
        /// Per-user preference profiles keyed by user identifier.
        /// </summary>
        private readonly ConcurrentDictionary<string, UserPreferenceProfile> _userProfiles;

        /// <summary>
        /// Cached scale adjustment factors keyed by "sourceScale|targetScale".
        /// </summary>
        private readonly ConcurrentDictionary<string, ScaleAdjustmentFactor> _scaleFactors;

        /// <summary>
        /// Tracks correction distances for analytics, keyed by pattern key.
        /// </summary>
        private readonly ConcurrentDictionary<string, List<double>> _correctionDistances;

        /// <summary>
        /// Convergence history for each pattern key: list of (timestamp, averageDistance) tuples.
        /// </summary>
        private readonly ConcurrentDictionary<string, List<ConvergenceDataPoint>> _convergenceHistory;

        /// <summary>
        /// Repository for persistent storage of patterns and corrections.
        /// </summary>
        private readonly TagRepository _repository;

        /// <summary>
        /// Lock for batch pattern extraction operations.
        /// </summary>
        private readonly object _extractionLock = new object();

        /// <summary>
        /// Lock for convergence history updates.
        /// </summary>
        private readonly object _convergenceLock = new object();

        /// <summary>
        /// Lock for user profile aggregation.
        /// </summary>
        private readonly object _profileLock = new object();

        /// <summary>
        /// Timer for periodic confidence decay and persistence.
        /// </summary>
        private Timer _maintenanceTimer;

        /// <summary>
        /// Whether the engine has been initialized.
        /// </summary>
        private volatile bool _isInitialized;

        /// <summary>
        /// Total number of corrections ever recorded (across all patterns).
        /// </summary>
        private long _totalCorrections;

        /// <summary>
        /// Timestamp of last persistence flush.
        /// </summary>
        private DateTime _lastPersistenceFlush;

        /// <summary>
        /// Maximum number of corrections retained per pattern key before pruning old entries.
        /// </summary>
        private const int MaxCorrectionsPerPattern = 500;

        /// <summary>
        /// Maintenance interval for decay and persistence (1 hour).
        /// </summary>
        private static readonly TimeSpan MaintenanceInterval = TimeSpan.FromHours(1);

        /// <summary>
        /// Minimum persistence flush interval to avoid excessive I/O.
        /// </summary>
        private static readonly TimeSpan MinFlushInterval = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Default view scale used when no scale information is available.
        /// </summary>
        private const double DefaultViewScale = 100.0;

        /// <summary>
        /// Element density threshold above which offsets are increased.
        /// </summary>
        private const int HighDensityThreshold = 15;

        /// <summary>
        /// Element density threshold below which offsets may be reduced.
        /// </summary>
        private const int LowDensityThreshold = 3;

        /// <summary>
        /// Maximum offset multiplier applied by density adjustment.
        /// </summary>
        private const double MaxDensityMultiplier = 1.6;

        /// <summary>
        /// Minimum offset multiplier applied by density adjustment.
        /// </summary>
        private const double MinDensityMultiplier = 0.7;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="TagIntelligenceEngine"/> class.
        /// </summary>
        /// <param name="repository">
        /// The tag repository used for persistent storage of patterns and corrections.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="repository"/> is null.
        /// </exception>
        public TagIntelligenceEngine(TagRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));

            _patterns = new ConcurrentDictionary<string, PlacementPattern>(StringComparer.OrdinalIgnoreCase);
            _corrections = new ConcurrentDictionary<string, List<PlacementCorrection>>(StringComparer.OrdinalIgnoreCase);
            _userProfiles = new ConcurrentDictionary<string, UserPreferenceProfile>(StringComparer.OrdinalIgnoreCase);
            _scaleFactors = new ConcurrentDictionary<string, ScaleAdjustmentFactor>(StringComparer.OrdinalIgnoreCase);
            _correctionDistances = new ConcurrentDictionary<string, List<double>>(StringComparer.OrdinalIgnoreCase);
            _convergenceHistory = new ConcurrentDictionary<string, List<ConvergenceDataPoint>>(StringComparer.OrdinalIgnoreCase);

            _lastPersistenceFlush = DateTime.UtcNow;

            Logger.Info("TagIntelligenceEngine created");
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes the engine by loading persisted patterns and corrections from the repository,
        /// then starting the maintenance timer for periodic confidence decay and persistence.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the initialization.</param>
        /// <returns>A task representing the asynchronous initialization operation.</returns>
        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (_isInitialized)
            {
                Logger.Warn("TagIntelligenceEngine.InitializeAsync called when already initialized");
                return;
            }

            Logger.Info("Initializing TagIntelligenceEngine...");

            try
            {
                await LoadPatternsFromRepositoryAsync(cancellationToken);
                await LoadCorrectionsFromRepositoryAsync(cancellationToken);

                StartMaintenanceTimer();

                _isInitialized = true;
                Logger.Info("TagIntelligenceEngine initialized with {0} patterns and corrections across {1} keys",
                    _patterns.Count, _corrections.Count);
            }
            catch (OperationCanceledException)
            {
                Logger.Warn("TagIntelligenceEngine initialization cancelled");
                throw;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to initialize TagIntelligenceEngine");
                _isInitialized = true; // Allow operation with empty state
            }
        }

        /// <summary>
        /// Loads all persisted patterns from the repository into the in-memory dictionary.
        /// </summary>
        private async Task LoadPatternsFromRepositoryAsync(CancellationToken ct)
        {
            await Task.Run(() =>
            {
                var patterns = _repository.GetAllPatterns();
                foreach (var pattern in patterns)
                {
                    if (!string.IsNullOrEmpty(pattern.PatternId))
                    {
                        _patterns[pattern.PatternId] = pattern;
                    }
                }

                Logger.Debug("Loaded {0} patterns from repository", patterns.Count);
            }, ct);
        }

        /// <summary>
        /// Loads all persisted corrections from the repository, grouped by pattern key.
        /// </summary>
        private async Task LoadCorrectionsFromRepositoryAsync(CancellationToken ct)
        {
            await Task.Run(() =>
            {
                var allCorrections = _repository.GetCorrections();
                foreach (var correction in allCorrections)
                {
                    string key = BuildPatternKey(correction.CategoryName, correction.ViewType);
                    var list = _corrections.GetOrAdd(key, _ => new List<PlacementCorrection>());
                    lock (list)
                    {
                        list.Add(correction);
                    }

                    // Track correction distances for analytics
                    double distance = ComputeCorrectionDistance(correction);
                    var distances = _correctionDistances.GetOrAdd(key, _ => new List<double>());
                    lock (distances)
                    {
                        distances.Add(distance);
                    }
                }

                Interlocked.Add(ref _totalCorrections, allCorrections.Count);
                Logger.Debug("Loaded {0} corrections from repository", allCorrections.Count);
            }, ct);
        }

        /// <summary>
        /// Starts the background maintenance timer for confidence decay and auto-persistence.
        /// </summary>
        private void StartMaintenanceTimer()
        {
            _maintenanceTimer = new Timer(
                OnMaintenanceTimerElapsed,
                null,
                MaintenanceInterval,
                MaintenanceInterval);
        }

        /// <summary>
        /// Callback for the maintenance timer. Applies confidence decay and flushes persistence.
        /// </summary>
        private void OnMaintenanceTimerElapsed(object state)
        {
            try
            {
                ApplyConfidenceDecay();
                FlushPersistenceIfNeeded();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during maintenance cycle");
            }
        }

        #endregion

        #region Correction Recording

        /// <summary>
        /// Records a user correction to an automated tag placement. This is the primary
        /// input to the learning system. When a user manually moves a tag after automated
        /// placement, call this method with the original and corrected positions.
        /// </summary>
        /// <param name="correction">The correction observation to record.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="correction"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown when the correction has no category name or placements.
        /// </exception>
        public void RecordCorrection(PlacementCorrection correction)
        {
            if (correction == null)
                throw new ArgumentNullException(nameof(correction));
            if (string.IsNullOrEmpty(correction.CategoryName))
                throw new ArgumentException("Correction must have a CategoryName", nameof(correction));
            if (correction.OriginalPlacement == null)
                throw new ArgumentException("Correction must have an OriginalPlacement", nameof(correction));
            if (correction.CorrectedPlacement == null)
                throw new ArgumentException("Correction must have a CorrectedPlacement", nameof(correction));

            var settings = TagConfiguration.Instance.Settings;
            if (!settings.LearningEnabled)
            {
                Logger.Trace("Learning disabled, ignoring correction for {0}", correction.CategoryName);
                return;
            }

            if (correction.CorrectedAt == default)
                correction.CorrectedAt = DateTime.UtcNow;

            string key = BuildPatternKey(correction.CategoryName, correction.ViewType);

            // Add to in-memory corrections store
            var corrections = _corrections.GetOrAdd(key, _ => new List<PlacementCorrection>());
            lock (corrections)
            {
                corrections.Add(correction);

                // Prune oldest corrections if we exceed the limit
                if (corrections.Count > MaxCorrectionsPerPattern)
                {
                    int removeCount = corrections.Count - MaxCorrectionsPerPattern;
                    corrections.RemoveRange(0, removeCount);
                    Logger.Trace("Pruned {0} old corrections for key {1}", removeCount, key);
                }
            }

            // Track correction distance for analytics
            double distance = ComputeCorrectionDistance(correction);
            var distances = _correctionDistances.GetOrAdd(key, _ => new List<double>());
            lock (distances)
            {
                distances.Add(distance);
            }

            // Record convergence data point
            RecordConvergencePoint(key, distance);

            // Persist correction to repository
            _repository.AddCorrection(correction);
            Interlocked.Increment(ref _totalCorrections);

            // Re-extract pattern for this key
            ExtractPatternForKey(key);

            Logger.Debug("Recorded correction for {0} in {1} view (distance: {2:F4})",
                correction.CategoryName, correction.ViewType, distance);
        }

        /// <summary>
        /// Records a batch of corrections efficiently. Useful when importing historical data
        /// or processing multiple corrections from a single editing session.
        /// </summary>
        /// <param name="corrections">The corrections to record.</param>
        /// <returns>The number of corrections successfully recorded.</returns>
        public int RecordCorrectionBatch(IEnumerable<PlacementCorrection> corrections)
        {
            if (corrections == null)
                throw new ArgumentNullException(nameof(corrections));

            var settings = TagConfiguration.Instance.Settings;
            if (!settings.LearningEnabled)
            {
                Logger.Debug("Learning disabled, ignoring correction batch");
                return 0;
            }

            int count = 0;
            var affectedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var correction in corrections)
            {
                if (correction == null || string.IsNullOrEmpty(correction.CategoryName))
                    continue;
                if (correction.OriginalPlacement == null || correction.CorrectedPlacement == null)
                    continue;

                if (correction.CorrectedAt == default)
                    correction.CorrectedAt = DateTime.UtcNow;

                string key = BuildPatternKey(correction.CategoryName, correction.ViewType);

                var list = _corrections.GetOrAdd(key, _ => new List<PlacementCorrection>());
                lock (list)
                {
                    list.Add(correction);
                    if (list.Count > MaxCorrectionsPerPattern)
                    {
                        int removeCount = list.Count - MaxCorrectionsPerPattern;
                        list.RemoveRange(0, removeCount);
                    }
                }

                double distance = ComputeCorrectionDistance(correction);
                var distances = _correctionDistances.GetOrAdd(key, _ => new List<double>());
                lock (distances)
                {
                    distances.Add(distance);
                }

                _repository.AddCorrection(correction);
                affectedKeys.Add(key);
                count++;
            }

            Interlocked.Add(ref _totalCorrections, count);

            // Re-extract patterns for all affected keys
            foreach (string key in affectedKeys)
            {
                ExtractPatternForKey(key);
                RecordConvergencePoint(key, GetRecentAverageDistance(key, 10));
            }

            Logger.Info("Recorded batch of {0} corrections across {1} pattern keys", count, affectedKeys.Count);
            return count;
        }

        /// <summary>
        /// Records a user preference observation within a specific user profile.
        /// Used when per-user tracking is enabled to maintain individual preference models.
        /// </summary>
        /// <param name="userId">Identifier of the user (e.g., Revit username).</param>
        /// <param name="correction">The correction made by this user.</param>
        public void RecordUserPreference(string userId, PlacementCorrection correction)
        {
            if (string.IsNullOrEmpty(userId) || correction == null) return;

            var profile = _userProfiles.GetOrAdd(userId, _ => new UserPreferenceProfile
            {
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            });

            lock (_profileLock)
            {
                string key = BuildPatternKey(correction.CategoryName, correction.ViewType);

                // Update per-user position preferences
                if (!profile.PositionPreferences.ContainsKey(key))
                    profile.PositionPreferences[key] = new List<TagPosition>();
                profile.PositionPreferences[key].Add(correction.CorrectedPlacement.ResolvedPosition);

                // Update per-user offset preferences
                if (!profile.OffsetPreferences.ContainsKey(key))
                    profile.OffsetPreferences[key] = new List<Point2D>();
                profile.OffsetPreferences[key].Add(new Point2D(
                    correction.CorrectedPlacement.OffsetX,
                    correction.CorrectedPlacement.OffsetY));

                // Update per-user leader preferences
                if (!profile.LeaderPreferences.ContainsKey(key))
                    profile.LeaderPreferences[key] = new List<LeaderType>();
                profile.LeaderPreferences[key].Add(correction.CorrectedPlacement.LeaderType);

                // Update per-user scale preferences
                if (correction.ViewScale > 0)
                {
                    if (!profile.ScalePreferences.ContainsKey(key))
                        profile.ScalePreferences[key] = new List<double>();
                    profile.ScalePreferences[key].Add(correction.ViewScale);
                }

                profile.TotalCorrections++;
                profile.LastActiveAt = DateTime.UtcNow;
            }

            Logger.Trace("Recorded user preference for {0} key={1}", userId,
                BuildPatternKey(correction.CategoryName, correction.ViewType));
        }

        /// <summary>
        /// Computes the Euclidean distance between the original and corrected positions.
        /// </summary>
        private double ComputeCorrectionDistance(PlacementCorrection correction)
        {
            var original = correction.OriginalPlacement.Position;
            var corrected = correction.CorrectedPlacement.Position;
            return original.DistanceTo(corrected);
        }

        /// <summary>
        /// Records a data point in the convergence history for the given pattern key.
        /// </summary>
        private void RecordConvergencePoint(string key, double averageDistance)
        {
            var history = _convergenceHistory.GetOrAdd(key, _ => new List<ConvergenceDataPoint>());
            lock (_convergenceLock)
            {
                history.Add(new ConvergenceDataPoint
                {
                    Timestamp = DateTime.UtcNow,
                    AverageCorrectionDistance = averageDistance,
                    ObservationCount = GetObservationCount(key)
                });

                // Keep last 200 convergence data points per key
                if (history.Count > 200)
                {
                    history.RemoveRange(0, history.Count - 200);
                }
            }
        }

        /// <summary>
        /// Gets the number of observations stored for a given pattern key.
        /// </summary>
        private int GetObservationCount(string key)
        {
            if (_corrections.TryGetValue(key, out var list))
            {
                lock (list) { return list.Count; }
            }
            return 0;
        }

        /// <summary>
        /// Gets the average correction distance for the most recent N corrections.
        /// </summary>
        private double GetRecentAverageDistance(string key, int recentCount)
        {
            if (_correctionDistances.TryGetValue(key, out var distances))
            {
                lock (distances)
                {
                    if (distances.Count == 0) return 0.0;
                    return distances.Skip(Math.Max(0, distances.Count - recentCount)).Average();
                }
            }
            return 0.0;
        }

        #endregion

        #region Pattern Extraction

        /// <summary>
        /// Extracts or updates the placement pattern for a specific pattern key
        /// based on accumulated corrections. Uses statistical analysis to determine
        /// the mode of preferred positions, weighted averages of offsets, and mode
        /// of leader types.
        /// </summary>
        /// <param name="key">The pattern key (CategoryName|ViewType).</param>
        private void ExtractPatternForKey(string key)
        {
            lock (_extractionLock)
            {
                if (!_corrections.TryGetValue(key, out var corrections))
                    return;

                List<PlacementCorrection> snapshot;
                lock (corrections)
                {
                    if (corrections.Count == 0) return;
                    snapshot = corrections.ToList();
                }

                ParsePatternKey(key, out string categoryName, out TagViewType viewType);

                // Compute the statistical mode of corrected tag positions
                TagPosition preferredPosition = ComputePositionMode(snapshot);

                // Compute recency-weighted average offsets
                ComputeWeightedOffsets(snapshot, out double preferredOffsetX, out double preferredOffsetY);

                // Compute the statistical mode of leader types
                LeaderType preferredLeaderType = ComputeLeaderTypeMode(snapshot);

                // Compute confidence based on observation count and recency
                int observationCount = snapshot.Count;
                DateTime lastReinforced = snapshot.Max(c => c.CorrectedAt);
                double confidence = CalculateConfidence(observationCount, lastReinforced);

                // Compute consistency factor: how uniform are the corrections?
                double consistencyFactor = ComputeConsistencyFactor(snapshot);
                confidence *= consistencyFactor;
                confidence = Math.Min(1.0, Math.Max(0.0, confidence));

                // Create or update pattern
                var pattern = new PlacementPattern
                {
                    PatternId = key,
                    CategoryName = categoryName,
                    ViewType = viewType,
                    PreferredPosition = preferredPosition,
                    PreferredOffsetX = preferredOffsetX,
                    PreferredOffsetY = preferredOffsetY,
                    PreferredLeaderType = preferredLeaderType,
                    Confidence = confidence,
                    ObservationCount = observationCount,
                    LastReinforced = lastReinforced
                };

                _patterns[key] = pattern;
                _repository.SavePattern(pattern);

                Logger.Debug("Extracted pattern {0}: position={1}, offset=({2:F4},{3:F4}), leader={4}, confidence={5:F3}, observations={6}",
                    key, preferredPosition, preferredOffsetX, preferredOffsetY,
                    preferredLeaderType, confidence, observationCount);
            }
        }

        /// <summary>
        /// Re-extracts all patterns from accumulated corrections. Useful after bulk
        /// import of correction data or periodic maintenance.
        /// </summary>
        public void ExtractAllPatterns()
        {
            var keys = _corrections.Keys.ToList();
            int updated = 0;

            foreach (string key in keys)
            {
                ExtractPatternForKey(key);
                updated++;
            }

            Logger.Info("Re-extracted {0} patterns from accumulated corrections", updated);
        }

        /// <summary>
        /// Computes the statistical mode (most frequent value) of corrected tag positions.
        /// Recent corrections are weighted more heavily by counting them multiple times
        /// proportional to their recency.
        /// </summary>
        private TagPosition ComputePositionMode(List<PlacementCorrection> corrections)
        {
            var positionCounts = new Dictionary<TagPosition, double>();
            DateTime now = DateTime.UtcNow;

            foreach (var correction in corrections)
            {
                TagPosition pos = correction.CorrectedPlacement.ResolvedPosition;
                double recencyWeight = ComputeRecencyWeight(correction.CorrectedAt, now);

                if (!positionCounts.ContainsKey(pos))
                    positionCounts[pos] = 0.0;
                positionCounts[pos] += recencyWeight;
            }

            if (positionCounts.Count == 0)
                return TagPosition.Auto;

            return positionCounts.OrderByDescending(kvp => kvp.Value).First().Key;
        }

        /// <summary>
        /// Computes recency-weighted average X and Y offsets from corrected placements.
        /// More recent corrections have exponentially higher weight.
        /// </summary>
        private void ComputeWeightedOffsets(
            List<PlacementCorrection> corrections,
            out double weightedOffsetX,
            out double weightedOffsetY)
        {
            double totalWeight = 0.0;
            double sumX = 0.0;
            double sumY = 0.0;
            DateTime now = DateTime.UtcNow;

            foreach (var correction in corrections)
            {
                double weight = ComputeRecencyWeight(correction.CorrectedAt, now);
                sumX += correction.CorrectedPlacement.OffsetX * weight;
                sumY += correction.CorrectedPlacement.OffsetY * weight;
                totalWeight += weight;
            }

            if (totalWeight < 1e-10)
            {
                weightedOffsetX = 0.0;
                weightedOffsetY = 0.0;
            }
            else
            {
                weightedOffsetX = sumX / totalWeight;
                weightedOffsetY = sumY / totalWeight;
            }
        }

        /// <summary>
        /// Computes the statistical mode of leader types from corrections,
        /// weighted by recency.
        /// </summary>
        private LeaderType ComputeLeaderTypeMode(List<PlacementCorrection> corrections)
        {
            var leaderCounts = new Dictionary<LeaderType, double>();
            DateTime now = DateTime.UtcNow;

            foreach (var correction in corrections)
            {
                LeaderType lt = correction.CorrectedPlacement.LeaderType;
                double weight = ComputeRecencyWeight(correction.CorrectedAt, now);

                if (!leaderCounts.ContainsKey(lt))
                    leaderCounts[lt] = 0.0;
                leaderCounts[lt] += weight;
            }

            if (leaderCounts.Count == 0)
                return LeaderType.Auto;

            return leaderCounts.OrderByDescending(kvp => kvp.Value).First().Key;
        }

        /// <summary>
        /// Computes a recency weight for a correction based on how recent it is.
        /// Uses an exponential decay function so that corrections from today are
        /// weighted 1.0, and corrections from 30 days ago are weighted ~0.37.
        /// </summary>
        /// <param name="correctionTime">When the correction was made.</param>
        /// <param name="now">Current time reference.</param>
        /// <returns>Weight in the range (0.0, 1.0].</returns>
        private double ComputeRecencyWeight(DateTime correctionTime, DateTime now)
        {
            double daysSince = (now - correctionTime).TotalDays;
            if (daysSince < 0) daysSince = 0;

            // Exponential decay with half-life of ~21 days
            return Math.Exp(-daysSince / 30.0);
        }

        /// <summary>
        /// Computes a consistency factor (0.0 to 1.0) measuring how uniform the corrections are.
        /// If the user always corrects to the same position, consistency is high.
        /// If corrections are scattered across many positions, consistency is lower.
        /// </summary>
        private double ComputeConsistencyFactor(List<PlacementCorrection> corrections)
        {
            if (corrections.Count < 2)
                return 0.5; // Neutral when insufficient data

            // Measure position consistency
            var positions = corrections.Select(c => c.CorrectedPlacement.ResolvedPosition).ToList();
            var positionGroups = positions.GroupBy(p => p).OrderByDescending(g => g.Count()).ToList();
            double positionDominance = (double)positionGroups[0].Count() / positions.Count;

            // Measure offset consistency via coefficient of variation
            var offsetsX = corrections.Select(c => c.CorrectedPlacement.OffsetX).ToList();
            var offsetsY = corrections.Select(c => c.CorrectedPlacement.OffsetY).ToList();
            double offsetConsistency = ComputeOffsetConsistency(offsetsX, offsetsY);

            // Combined consistency: position mode dominance * offset consistency
            double consistency = positionDominance * 0.6 + offsetConsistency * 0.4;
            return Math.Min(1.0, Math.Max(0.1, consistency));
        }

        /// <summary>
        /// Computes offset consistency using the inverse coefficient of variation.
        /// Low variance means high consistency.
        /// </summary>
        private double ComputeOffsetConsistency(List<double> offsetsX, List<double> offsetsY)
        {
            if (offsetsX.Count < 2) return 0.5;

            double meanX = offsetsX.Average();
            double meanY = offsetsY.Average();
            double stdDevX = Math.Sqrt(offsetsX.Sum(x => (x - meanX) * (x - meanX)) / offsetsX.Count);
            double stdDevY = Math.Sqrt(offsetsY.Sum(y => (y - meanY) * (y - meanY)) / offsetsY.Count);

            // Use the mean of the absolute values as the denominator to avoid division by near-zero
            double denomX = Math.Max(Math.Abs(meanX), 0.001);
            double denomY = Math.Max(Math.Abs(meanY), 0.001);

            double cvX = stdDevX / denomX;
            double cvY = stdDevY / denomY;

            // Convert CV to a consistency score: low CV = high consistency
            // CV of 0 => consistency 1.0; CV of 2+ => consistency near 0.1
            double avgCv = (cvX + cvY) / 2.0;
            double consistency = 1.0 / (1.0 + avgCv);
            return Math.Min(1.0, Math.Max(0.1, consistency));
        }

        #endregion

        #region Confidence Management

        /// <summary>
        /// Calculates the confidence level for a pattern based on the number of observations
        /// and the recency of the last reinforcement. Confidence approaches 1.0 asymptotically
        /// with more observations and is reduced by time decay.
        /// </summary>
        /// <param name="observationCount">Number of correction observations.</param>
        /// <param name="lastReinforced">Timestamp of the most recent correction.</param>
        /// <returns>Confidence value in the range [0.0, 1.0].</returns>
        private double CalculateConfidence(int observationCount, DateTime lastReinforced)
        {
            var settings = TagConfiguration.Instance.Settings;
            int threshold = settings.LearningConfidenceThreshold;

            // Base confidence: asymptotic function approaching 1.0
            // f(n) = 1 - e^(-n / threshold)
            // At n = threshold: confidence ~0.63
            // At n = 2*threshold: confidence ~0.86
            // At n = 3*threshold: confidence ~0.95
            double baseConfidence = 1.0 - Math.Exp(-(double)observationCount / Math.Max(1, threshold));

            // Time decay: reduce confidence for stale patterns
            double daysSinceReinforcement = (DateTime.UtcNow - lastReinforced).TotalDays;
            double decayRate = settings.ConfidenceDecayRate;
            double timeDecay = Math.Exp(-decayRate * daysSinceReinforcement);

            double confidence = baseConfidence * timeDecay;
            return Math.Min(1.0, Math.Max(0.0, confidence));
        }

        /// <summary>
        /// Applies time-based confidence decay to all stored patterns.
        /// Called periodically by the maintenance timer.
        /// </summary>
        private void ApplyConfidenceDecay()
        {
            int decayedCount = 0;
            int removedCount = 0;
            var settings = TagConfiguration.Instance.Settings;

            foreach (var kvp in _patterns)
            {
                var pattern = kvp.Value;
                double newConfidence = CalculateConfidence(pattern.ObservationCount, pattern.LastReinforced);

                if (Math.Abs(newConfidence - pattern.Confidence) > 0.001)
                {
                    pattern.Confidence = newConfidence;
                    decayedCount++;

                    // Remove patterns that have decayed to near-zero confidence with few observations
                    if (newConfidence < 0.01 && pattern.ObservationCount < 3)
                    {
                        _patterns.TryRemove(kvp.Key, out _);
                        removedCount++;
                    }
                }
            }

            if (decayedCount > 0)
            {
                Logger.Debug("Confidence decay applied to {0} patterns, removed {1} negligible patterns",
                    decayedCount, removedCount);
            }
        }

        /// <summary>
        /// Gets the effective confidence for a pattern, accounting for current time decay
        /// without modifying the stored value. Used for real-time prediction decisions.
        /// </summary>
        /// <param name="pattern">The pattern to evaluate.</param>
        /// <returns>The effective confidence value.</returns>
        private double GetEffectiveConfidence(PlacementPattern pattern)
        {
            return CalculateConfidence(pattern.ObservationCount, pattern.LastReinforced);
        }

        /// <summary>
        /// Explicitly reinforces a pattern by updating its last-reinforced timestamp,
        /// causing confidence to recover from time decay. Call this when the user accepts
        /// a prediction without modification.
        /// </summary>
        /// <param name="patternId">The pattern ID to reinforce.</param>
        public void ReinforcePattern(string patternId)
        {
            if (_patterns.TryGetValue(patternId, out var pattern))
            {
                pattern.LastReinforced = DateTime.UtcNow;
                pattern.Confidence = CalculateConfidence(pattern.ObservationCount, pattern.LastReinforced);

                _repository.SavePattern(pattern);
                Logger.Trace("Reinforced pattern {0}, confidence now {1:F3}", patternId, pattern.Confidence);
            }
        }

        #endregion

        #region Predictive Placement

        /// <summary>
        /// Predicts the optimal tag placement for a given category and view type.
        /// Returns null if no reliable prediction can be made (insufficient observations
        /// or confidence below threshold).
        /// </summary>
        /// <param name="categoryName">The Revit category of the element to tag.</param>
        /// <param name="viewType">The type of view the tag will be placed in.</param>
        /// <returns>
        /// A <see cref="PlacementPrediction"/> with the predicted placement and confidence,
        /// or null if no reliable prediction is available.
        /// </returns>
        public PlacementPrediction PredictPlacement(string categoryName, TagViewType viewType)
        {
            if (string.IsNullOrEmpty(categoryName)) return null;

            var settings = TagConfiguration.Instance.Settings;
            if (!settings.LearningEnabled) return null;

            string key = BuildPatternKey(categoryName, viewType);

            if (!_patterns.TryGetValue(key, out var pattern))
                return null;

            double effectiveConfidence = GetEffectiveConfidence(pattern);

            if (!MeetsReliabilityThreshold(pattern, effectiveConfidence))
                return null;

            var predictedPlacement = new TagPlacement
            {
                PreferredPosition = pattern.PreferredPosition,
                ResolvedPosition = pattern.PreferredPosition,
                OffsetX = pattern.PreferredOffsetX,
                OffsetY = pattern.PreferredOffsetY,
                LeaderType = pattern.PreferredLeaderType,
                Orientation = TagOrientation.Auto
            };

            return new PlacementPrediction
            {
                PredictedPlacement = predictedPlacement,
                Confidence = effectiveConfidence,
                PatternId = pattern.PatternId,
                IsReliable = true
            };
        }

        /// <summary>
        /// Predicts the optimal tag placement with full context awareness, adjusting for
        /// view scale, element density, and user preferences.
        /// </summary>
        /// <param name="categoryName">The Revit category of the element to tag.</param>
        /// <param name="viewType">The type of view the tag will be placed in.</param>
        /// <param name="context">
        /// The view context providing scale, existing annotations, and crop bounds.
        /// </param>
        /// <param name="nearbyElementCount">
        /// Number of elements near the element being tagged.
        /// </param>
        /// <param name="averageSpacing">
        /// Average spacing between nearby elements in model units.
        /// </param>
        /// <param name="userId">
        /// Optional user identifier for per-user preference adjustment.
        /// </param>
        /// <returns>
        /// A <see cref="PlacementPrediction"/> with the context-adjusted placement,
        /// or null if no reliable prediction is available.
        /// </returns>
        public PlacementPrediction PredictPlacementWithContext(
            string categoryName,
            TagViewType viewType,
            ViewTagContext context,
            int nearbyElementCount = 0,
            double averageSpacing = 0.0,
            string userId = null)
        {
            // Get base prediction
            var basePrediction = PredictPlacement(categoryName, viewType);
            if (basePrediction == null) return null;

            var adjustedPlacement = ClonePlacement(basePrediction.PredictedPlacement);
            double confidenceModifier = 1.0;

            // Apply view scale adjustment
            if (context != null && context.Scale > 0)
            {
                adjustedPlacement = AdjustForViewScale(adjustedPlacement, DefaultViewScale, context.Scale);
                confidenceModifier *= ComputeScaleConfidenceModifier(categoryName, viewType, context.Scale);
            }

            // Apply element density adjustment
            if (nearbyElementCount > 0)
            {
                adjustedPlacement = AdjustForElementDensity(adjustedPlacement, nearbyElementCount, averageSpacing);
                confidenceModifier *= ComputeDensityConfidenceModifier(nearbyElementCount);
            }

            // Apply user preference adjustment
            if (!string.IsNullOrEmpty(userId))
            {
                adjustedPlacement = AdjustForUserProfile(adjustedPlacement, userId, categoryName, viewType);
            }

            double adjustedConfidence = basePrediction.Confidence * confidenceModifier;
            adjustedConfidence = Math.Min(1.0, Math.Max(0.0, adjustedConfidence));

            return new PlacementPrediction
            {
                PredictedPlacement = adjustedPlacement,
                Confidence = adjustedConfidence,
                PatternId = basePrediction.PatternId,
                IsReliable = adjustedConfidence >= GetMinimumConfidenceThreshold()
            };
        }

        /// <summary>
        /// Determines whether a pattern meets the reliability threshold for predictions.
        /// A pattern must have sufficient observations and confidence.
        /// </summary>
        private bool MeetsReliabilityThreshold(PlacementPattern pattern, double effectiveConfidence)
        {
            var settings = TagConfiguration.Instance.Settings;
            int minObservations = settings.LearningConfidenceThreshold;
            double minConfidence = GetMinimumConfidenceThreshold();

            return pattern.ObservationCount >= minObservations && effectiveConfidence >= minConfidence;
        }

        /// <summary>
        /// Gets the minimum confidence threshold for predictions to be considered reliable.
        /// Derived from the intelligence score weight setting.
        /// </summary>
        private double GetMinimumConfidenceThreshold()
        {
            var settings = TagConfiguration.Instance.Settings;
            // Use half the intelligence weight as the minimum confidence threshold
            // At default weight of 0.3, minimum confidence is 0.15
            return settings.IntelligenceScoreWeight * 0.5;
        }

        /// <summary>
        /// Creates a deep copy of a TagPlacement.
        /// </summary>
        private TagPlacement ClonePlacement(TagPlacement source)
        {
            return new TagPlacement
            {
                Position = source.Position,
                LeaderEndPoint = source.LeaderEndPoint,
                LeaderElbowPoint = source.LeaderElbowPoint,
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

        #region Context-Aware Adjustment

        /// <summary>
        /// Adjusts a predicted placement for the difference between the source view scale
        /// (at which the pattern was learned) and the target view scale. Offsets scale
        /// proportionally so that tags remain visually consistent across scales.
        /// </summary>
        /// <param name="placement">The base predicted placement to adjust.</param>
        /// <param name="sourceScale">The view scale at which the pattern was learned (e.g., 100 for 1:100).</param>
        /// <param name="targetScale">The actual view scale for the current placement.</param>
        /// <returns>An adjusted copy of the placement.</returns>
        private TagPlacement AdjustForViewScale(TagPlacement placement, double sourceScale, double targetScale)
        {
            if (sourceScale <= 0 || targetScale <= 0) return placement;
            if (Math.Abs(sourceScale - targetScale) < 1e-6) return placement;

            string factorKey = $"{sourceScale:F1}|{targetScale:F1}";
            var factor = _scaleFactors.GetOrAdd(factorKey, _ => new ScaleAdjustmentFactor
            {
                SourceScale = sourceScale,
                TargetScale = targetScale,
                OffsetMultiplier = ComputeScaleOffsetMultiplier(sourceScale, targetScale),
                LeaderLengthMultiplier = ComputeScaleLeaderMultiplier(sourceScale, targetScale)
            });

            placement.OffsetX *= factor.OffsetMultiplier;
            placement.OffsetY *= factor.OffsetMultiplier;
            placement.LeaderLength *= factor.LeaderLengthMultiplier;

            return placement;
        }

        /// <summary>
        /// Computes the offset multiplier for a scale transition. Larger scale numbers
        /// (more zoomed out) need proportionally larger offsets in model units.
        /// The relationship is not purely linear: a square root scaling provides
        /// better visual results than linear scaling.
        /// </summary>
        private double ComputeScaleOffsetMultiplier(double sourceScale, double targetScale)
        {
            // Square root scaling: moderate offsets at extreme scale differences
            double ratio = targetScale / sourceScale;
            return Math.Sqrt(ratio);
        }

        /// <summary>
        /// Computes the leader length multiplier for a scale transition.
        /// Leader lengths scale more linearly with view scale than offsets.
        /// </summary>
        private double ComputeScaleLeaderMultiplier(double sourceScale, double targetScale)
        {
            double ratio = targetScale / sourceScale;
            // Leader lengths scale nearly linearly but damped slightly
            return Math.Pow(ratio, 0.8);
        }

        /// <summary>
        /// Computes a confidence modifier based on how much scale data we have for
        /// the target scale. If most corrections were recorded at the target scale,
        /// confidence remains high. If the target scale is very different from observed
        /// scales, confidence is reduced.
        /// </summary>
        private double ComputeScaleConfidenceModifier(string categoryName, TagViewType viewType, double targetScale)
        {
            string key = BuildPatternKey(categoryName, viewType);
            if (!_corrections.TryGetValue(key, out var corrections))
                return 0.7; // Default moderate modifier for unknown

            List<double> observedScales;
            lock (corrections)
            {
                observedScales = corrections
                    .Where(c => c.ViewScale > 0)
                    .Select(c => c.ViewScale)
                    .ToList();
            }

            if (observedScales.Count == 0)
                return 0.8; // No scale data, moderate confidence

            // Find the closest observed scale
            double closestScale = observedScales.OrderBy(s => Math.Abs(s - targetScale)).First();
            double scaleDistance = Math.Abs(closestScale - targetScale) / Math.Max(closestScale, targetScale);

            // If scale distance is small (< 20%), full confidence; larger distance reduces confidence
            if (scaleDistance < 0.2) return 1.0;
            if (scaleDistance < 0.5) return 0.85;
            if (scaleDistance < 1.0) return 0.7;
            return 0.5;
        }

        /// <summary>
        /// Adjusts a predicted placement based on element density in the vicinity.
        /// High density areas need larger offsets to avoid crowding; low density areas
        /// can use tighter offsets.
        /// </summary>
        /// <param name="placement">The base predicted placement to adjust.</param>
        /// <param name="nearbyElementCount">Number of elements near the one being tagged.</param>
        /// <param name="averageSpacing">Average spacing between nearby elements.</param>
        /// <returns>An adjusted copy of the placement.</returns>
        private TagPlacement AdjustForElementDensity(TagPlacement placement, int nearbyElementCount, double averageSpacing)
        {
            double densityMultiplier = 1.0;

            if (nearbyElementCount >= HighDensityThreshold)
            {
                // High density: increase offsets to reduce crowding
                double excessRatio = (double)(nearbyElementCount - HighDensityThreshold) / HighDensityThreshold;
                densityMultiplier = Math.Min(MaxDensityMultiplier, 1.0 + excessRatio * 0.3);

                // In high density, prefer leader-based placement to move tags further away
                if (placement.LeaderType == LeaderType.None)
                    placement.LeaderType = LeaderType.Auto;
            }
            else if (nearbyElementCount <= LowDensityThreshold)
            {
                // Low density: tighten offsets for a cleaner look
                densityMultiplier = MinDensityMultiplier +
                    (1.0 - MinDensityMultiplier) * ((double)nearbyElementCount / LowDensityThreshold);
            }

            // If average spacing is very tight, push offsets up further
            if (averageSpacing > 0 && averageSpacing < 0.01) // < 10mm
            {
                double spacingFactor = 0.01 / Math.Max(averageSpacing, 0.001);
                densityMultiplier *= Math.Min(1.5, spacingFactor);
                densityMultiplier = Math.Min(MaxDensityMultiplier, densityMultiplier);
            }

            placement.OffsetX *= densityMultiplier;
            placement.OffsetY *= densityMultiplier;

            return placement;
        }

        /// <summary>
        /// Computes a confidence modifier based on element density. Predictions are
        /// slightly less reliable in very high or very low density situations since
        /// the pattern may have been learned under different density conditions.
        /// </summary>
        private double ComputeDensityConfidenceModifier(int nearbyElementCount)
        {
            if (nearbyElementCount >= HighDensityThreshold)
                return 0.85;
            if (nearbyElementCount <= LowDensityThreshold && nearbyElementCount > 0)
                return 0.9;
            return 1.0;
        }

        /// <summary>
        /// Adjusts a predicted placement based on a specific user's preference profile.
        /// If the user has a recorded profile, their preferences are blended with the
        /// global pattern using a weighted merge. If no user profile exists, the placement
        /// is returned unchanged.
        /// </summary>
        /// <param name="placement">The base predicted placement to adjust.</param>
        /// <param name="userId">Identifier of the current user.</param>
        /// <param name="categoryName">Category name for the pattern key.</param>
        /// <param name="viewType">View type for the pattern key.</param>
        /// <returns>A user-preference-adjusted placement.</returns>
        private TagPlacement AdjustForUserProfile(
            TagPlacement placement,
            string userId,
            string categoryName,
            TagViewType viewType)
        {
            if (!_userProfiles.TryGetValue(userId, out var profile))
                return placement;

            string key = BuildPatternKey(categoryName, viewType);

            lock (_profileLock)
            {
                // Blend user's preferred position if available
                if (profile.PositionPreferences.TryGetValue(key, out var userPositions) && userPositions.Count >= 3)
                {
                    var userMode = userPositions.GroupBy(p => p)
                        .OrderByDescending(g => g.Count())
                        .First().Key;

                    double userDominance = (double)userPositions.Count(p => p == userMode) / userPositions.Count;

                    // Only override global preference if user has a strong preference (>60%)
                    if (userDominance > 0.6)
                    {
                        placement.PreferredPosition = userMode;
                        placement.ResolvedPosition = userMode;
                    }
                }

                // Blend user's preferred offsets if available
                if (profile.OffsetPreferences.TryGetValue(key, out var userOffsets) && userOffsets.Count >= 3)
                {
                    double userAvgX = userOffsets.Average(o => o.X);
                    double userAvgY = userOffsets.Average(o => o.Y);

                    // Weighted blend: 60% user preference, 40% global pattern
                    double userWeight = Math.Min(0.6, (double)userOffsets.Count / 20.0);
                    double globalWeight = 1.0 - userWeight;

                    placement.OffsetX = placement.OffsetX * globalWeight + userAvgX * userWeight;
                    placement.OffsetY = placement.OffsetY * globalWeight + userAvgY * userWeight;
                }

                // Blend user's preferred leader type if available
                if (profile.LeaderPreferences.TryGetValue(key, out var userLeaders) && userLeaders.Count >= 3)
                {
                    var userLeaderMode = userLeaders.GroupBy(l => l)
                        .OrderByDescending(g => g.Count())
                        .First().Key;

                    double leaderDominance = (double)userLeaders.Count(l => l == userLeaderMode) / userLeaders.Count;
                    if (leaderDominance > 0.5)
                    {
                        placement.LeaderType = userLeaderMode;
                    }
                }
            }

            return placement;
        }

        /// <summary>
        /// Gets the user preference profile for the specified user, or null if none exists.
        /// </summary>
        /// <param name="userId">The user identifier.</param>
        /// <returns>The user's preference profile, or null.</returns>
        public UserPreferenceProfile GetUserProfile(string userId)
        {
            if (string.IsNullOrEmpty(userId)) return null;
            return _userProfiles.TryGetValue(userId, out var profile) ? profile : null;
        }

        /// <summary>
        /// Gets a list of all user IDs that have recorded preference profiles.
        /// </summary>
        /// <returns>List of user identifiers.</returns>
        public List<string> GetTrackedUserIds()
        {
            return _userProfiles.Keys.ToList();
        }

        /// <summary>
        /// Clears the preference profile for a specific user.
        /// </summary>
        /// <param name="userId">The user identifier.</param>
        public void ClearUserProfile(string userId)
        {
            if (!string.IsNullOrEmpty(userId))
            {
                _userProfiles.TryRemove(userId, out _);
                Logger.Info("Cleared user preference profile for {0}", userId);
            }
        }

        #endregion

        #region Pattern Persistence

        /// <summary>
        /// Flushes all learned patterns to the repository for persistent storage.
        /// This is called automatically by the maintenance timer and can also be
        /// called manually before application shutdown.
        /// </summary>
        /// <returns>A task representing the asynchronous flush operation.</returns>
        public async Task FlushPatternsAsync(CancellationToken cancellationToken = default)
        {
            int savedCount = 0;

            await Task.Run(() =>
            {
                foreach (var kvp in _patterns)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    _repository.SavePattern(kvp.Value);
                    savedCount++;
                }
            }, cancellationToken);

            _lastPersistenceFlush = DateTime.UtcNow;
            Logger.Info("Flushed {0} patterns to repository", savedCount);
        }

        /// <summary>
        /// Synchronizes the in-memory pattern store with the repository, merging any
        /// patterns that may have been modified externally (e.g., imported from a package).
        /// </summary>
        public void SyncWithRepository()
        {
            var repositoryPatterns = _repository.GetAllPatterns();
            int added = 0;
            int updated = 0;

            foreach (var repoPattern in repositoryPatterns)
            {
                if (string.IsNullOrEmpty(repoPattern.PatternId)) continue;

                if (_patterns.TryGetValue(repoPattern.PatternId, out var existing))
                {
                    // Keep the version with more observations or more recent reinforcement
                    if (repoPattern.ObservationCount > existing.ObservationCount ||
                        repoPattern.LastReinforced > existing.LastReinforced)
                    {
                        _patterns[repoPattern.PatternId] = repoPattern;
                        updated++;
                    }
                }
                else
                {
                    _patterns[repoPattern.PatternId] = repoPattern;
                    added++;
                }
            }

            if (added > 0 || updated > 0)
            {
                Logger.Info("Synced with repository: {0} patterns added, {1} patterns updated", added, updated);
            }
        }

        /// <summary>
        /// Flushes persistence if the minimum interval has elapsed.
        /// </summary>
        private void FlushPersistenceIfNeeded()
        {
            if ((DateTime.UtcNow - _lastPersistenceFlush) < MinFlushInterval)
                return;

            try
            {
                foreach (var kvp in _patterns)
                {
                    _repository.SavePattern(kvp.Value);
                }
                _lastPersistenceFlush = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to flush patterns to repository during maintenance");
            }
        }

        #endregion

        #region Analytics

        /// <summary>
        /// Returns comprehensive learning statistics including total corrections,
        /// pattern count, average confidence, and per-category breakdowns.
        /// </summary>
        /// <returns>A <see cref="LearningStatistics"/> summary.</returns>
        public LearningStatistics GetLearningStatistics()
        {
            var stats = new LearningStatistics
            {
                TotalCorrectionsRecorded = Interlocked.Read(ref _totalCorrections),
                TotalPatternsLearned = _patterns.Count,
                TotalUserProfiles = _userProfiles.Count,
                IsLearningEnabled = TagConfiguration.Instance.Settings.LearningEnabled,
                LastMaintenanceFlush = _lastPersistenceFlush
            };

            // Per-pattern statistics
            foreach (var kvp in _patterns)
            {
                var pattern = kvp.Value;
                double effectiveConfidence = GetEffectiveConfidence(pattern);

                stats.PatternDetails.Add(new PatternStatistics
                {
                    PatternId = pattern.PatternId,
                    CategoryName = pattern.CategoryName,
                    ViewType = pattern.ViewType,
                    ObservationCount = pattern.ObservationCount,
                    Confidence = effectiveConfidence,
                    PreferredPosition = pattern.PreferredPosition,
                    PreferredLeaderType = pattern.PreferredLeaderType,
                    LastReinforced = pattern.LastReinforced,
                    MeetsReliabilityThreshold = MeetsReliabilityThreshold(pattern, effectiveConfidence)
                });
            }

            // Aggregate confidence
            if (stats.PatternDetails.Count > 0)
            {
                stats.AverageConfidence = stats.PatternDetails.Average(p => p.Confidence);
                stats.ReliablePatternCount = stats.PatternDetails.Count(p => p.MeetsReliabilityThreshold);
            }

            // Category breakdown
            foreach (var group in stats.PatternDetails.GroupBy(p => p.CategoryName))
            {
                stats.CorrectionsPerCategory[group.Key] = group.Sum(p => p.ObservationCount);
            }

            return stats;
        }

        /// <summary>
        /// Gets the categories with the most corrections, indicating areas where
        /// automated placement needs the most improvement.
        /// </summary>
        /// <param name="topN">Number of top categories to return.</param>
        /// <returns>
        /// List of (CategoryName, CorrectionCount) tuples ordered by correction count descending.
        /// </returns>
        public List<CategoryCorrectionSummary> GetMostCorrectedCategories(int topN = 10)
        {
            var categorySums = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in _corrections)
            {
                ParsePatternKey(kvp.Key, out string categoryName, out _);
                int count;
                lock (kvp.Value)
                {
                    count = kvp.Value.Count;
                }

                if (!categorySums.ContainsKey(categoryName))
                    categorySums[categoryName] = 0;
                categorySums[categoryName] += count;
            }

            return categorySums
                .OrderByDescending(kvp => kvp.Value)
                .Take(topN)
                .Select(kvp => new CategoryCorrectionSummary
                {
                    CategoryName = kvp.Key,
                    CorrectionCount = kvp.Value,
                    AverageDistance = GetAverageCorrectionDistance(kvp.Key),
                    PatternConfidence = GetCategoryAverageConfidence(kvp.Key)
                })
                .ToList();
        }

        /// <summary>
        /// Gets the average distance between original and corrected positions for
        /// a specific category across all view types. A decreasing trend indicates
        /// the system is learning and improving.
        /// </summary>
        /// <param name="categoryName">The category to analyze.</param>
        /// <returns>Average correction distance in model units, or 0.0 if no data.</returns>
        public double GetAverageCorrectionDistance(string categoryName)
        {
            if (string.IsNullOrEmpty(categoryName)) return 0.0;

            var allDistances = new List<double>();

            foreach (var kvp in _correctionDistances)
            {
                ParsePatternKey(kvp.Key, out string cat, out _);
                if (!string.Equals(cat, categoryName, StringComparison.OrdinalIgnoreCase))
                    continue;

                lock (kvp.Value)
                {
                    allDistances.AddRange(kvp.Value);
                }
            }

            return allDistances.Count > 0 ? allDistances.Average() : 0.0;
        }

        /// <summary>
        /// Gets the average confidence across all patterns for a specific category.
        /// </summary>
        private double GetCategoryAverageConfidence(string categoryName)
        {
            var categoryPatterns = _patterns.Values
                .Where(p => string.Equals(p.CategoryName, categoryName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (categoryPatterns.Count == 0) return 0.0;
            return categoryPatterns.Average(p => GetEffectiveConfidence(p));
        }

        /// <summary>
        /// Gets the convergence trend for a specific category and view type. This shows
        /// how the average correction distance changes over time. A downward trend indicates
        /// the learning engine is converging on the user's preferred placement style.
        /// </summary>
        /// <param name="categoryName">The category to analyze.</param>
        /// <param name="viewType">The view type to analyze.</param>
        /// <returns>
        /// A chronologically ordered list of <see cref="ConvergenceDataPoint"/> entries.
        /// </returns>
        public List<ConvergenceDataPoint> GetConvergenceTrend(string categoryName, TagViewType viewType)
        {
            string key = BuildPatternKey(categoryName, viewType);

            if (_convergenceHistory.TryGetValue(key, out var history))
            {
                lock (_convergenceLock)
                {
                    return history.ToList();
                }
            }

            return new List<ConvergenceDataPoint>();
        }

        /// <summary>
        /// Generates a correction heatmap showing the spatial distribution of user corrections
        /// for a specific category and view type. Each entry represents a bucket of corrected
        /// positions with the count of corrections in that area.
        /// </summary>
        /// <param name="categoryName">The category to analyze.</param>
        /// <param name="viewType">The view type to analyze.</param>
        /// <param name="bucketSize">
        /// Size of each bucket in model units (default ~10mm).
        /// </param>
        /// <returns>List of heatmap entries showing spatial correction density.</returns>
        public List<CorrectionHeatmapEntry> GetCorrectionHeatmap(
            string categoryName,
            TagViewType viewType,
            double bucketSize = 0.01)
        {
            string key = BuildPatternKey(categoryName, viewType);

            if (!_corrections.TryGetValue(key, out var corrections))
                return new List<CorrectionHeatmapEntry>();

            List<PlacementCorrection> snapshot;
            lock (corrections)
            {
                snapshot = corrections.ToList();
            }

            if (snapshot.Count == 0)
                return new List<CorrectionHeatmapEntry>();

            // Bucket corrected positions into a grid
            var buckets = new Dictionary<string, CorrectionHeatmapEntry>();

            foreach (var correction in snapshot)
            {
                var pos = correction.CorrectedPlacement.Position;
                int bucketX = (int)Math.Floor(pos.X / bucketSize);
                int bucketY = (int)Math.Floor(pos.Y / bucketSize);
                string bucketKey = $"{bucketX}|{bucketY}";

                if (!buckets.TryGetValue(bucketKey, out var entry))
                {
                    entry = new CorrectionHeatmapEntry
                    {
                        BucketCenter = new Point2D(
                            (bucketX + 0.5) * bucketSize,
                            (bucketY + 0.5) * bucketSize),
                        CorrectionCount = 0,
                        AverageDistance = 0.0
                    };
                    buckets[bucketKey] = entry;
                }

                entry.CorrectionCount++;
                double distance = ComputeCorrectionDistance(correction);
                entry.AverageDistance = ((entry.AverageDistance * (entry.CorrectionCount - 1)) + distance)
                                       / entry.CorrectionCount;
            }

            // Normalize density values
            int maxCount = buckets.Values.Max(e => e.CorrectionCount);
            foreach (var entry in buckets.Values)
            {
                entry.NormalizedDensity = maxCount > 0 ? (double)entry.CorrectionCount / maxCount : 0.0;
            }

            return buckets.Values
                .OrderByDescending(e => e.CorrectionCount)
                .ToList();
        }

        /// <summary>
        /// Gets a summary of correction distance statistics for a specific category,
        /// including mean, median, standard deviation, and percentiles.
        /// </summary>
        /// <param name="categoryName">The category to analyze.</param>
        /// <returns>Distance statistics, or null if no data is available.</returns>
        public CorrectionDistanceSummary GetCorrectionDistanceStatistics(string categoryName)
        {
            var allDistances = new List<double>();

            foreach (var kvp in _correctionDistances)
            {
                ParsePatternKey(kvp.Key, out string cat, out _);
                if (!string.Equals(cat, categoryName, StringComparison.OrdinalIgnoreCase))
                    continue;

                lock (kvp.Value)
                {
                    allDistances.AddRange(kvp.Value);
                }
            }

            if (allDistances.Count == 0) return null;

            allDistances.Sort();
            int count = allDistances.Count;
            double mean = allDistances.Average();
            double median = count % 2 == 0
                ? (allDistances[count / 2 - 1] + allDistances[count / 2]) / 2.0
                : allDistances[count / 2];
            double stdDev = Math.Sqrt(allDistances.Sum(d => (d - mean) * (d - mean)) / count);

            return new CorrectionDistanceSummary
            {
                CategoryName = categoryName,
                SampleCount = count,
                MeanDistance = mean,
                MedianDistance = median,
                StandardDeviation = stdDev,
                MinDistance = allDistances[0],
                MaxDistance = allDistances[count - 1],
                Percentile25 = allDistances[(int)(count * 0.25)],
                Percentile75 = allDistances[(int)(count * 0.75)],
                Percentile90 = allDistances[(int)(count * 0.90)]
            };
        }

        /// <summary>
        /// Determines whether the learning engine has converged for a specific pattern,
        /// meaning the average correction distance is below a threshold and stable.
        /// </summary>
        /// <param name="categoryName">The category to check.</param>
        /// <param name="viewType">The view type to check.</param>
        /// <param name="distanceThreshold">
        /// Maximum average correction distance to consider converged (default ~5mm).
        /// </param>
        /// <returns>True if the pattern has converged.</returns>
        public bool HasConverged(string categoryName, TagViewType viewType, double distanceThreshold = 0.005)
        {
            string key = BuildPatternKey(categoryName, viewType);

            if (!_convergenceHistory.TryGetValue(key, out var history))
                return false;

            lock (_convergenceLock)
            {
                if (history.Count < 5) return false;

                // Check the last 5 data points
                var recent = history.Skip(Math.Max(0, history.Count - 5)).ToList();
                double recentAvg = recent.Average(p => p.AverageCorrectionDistance);

                // Check that distance is below threshold and stable (low variance)
                if (recentAvg > distanceThreshold) return false;

                double variance = recent.Sum(p =>
                    (p.AverageCorrectionDistance - recentAvg) * (p.AverageCorrectionDistance - recentAvg))
                    / recent.Count;

                // Stable if variance is less than 10% of the threshold
                return variance < distanceThreshold * 0.1;
            }
        }

        #endregion

        #region Pattern Query

        /// <summary>
        /// Gets all learned patterns currently in memory.
        /// </summary>
        /// <returns>List of all placement patterns.</returns>
        public List<PlacementPattern> GetAllPatterns()
        {
            return _patterns.Values.ToList();
        }

        /// <summary>
        /// Gets the learned pattern for a specific category and view type, or null if none exists.
        /// </summary>
        /// <param name="categoryName">The Revit category name.</param>
        /// <param name="viewType">The view type.</param>
        /// <returns>The matching pattern, or null.</returns>
        public PlacementPattern GetPattern(string categoryName, TagViewType viewType)
        {
            string key = BuildPatternKey(categoryName, viewType);
            return _patterns.TryGetValue(key, out var pattern) ? pattern : null;
        }

        /// <summary>
        /// Gets all patterns for a specific category across all view types.
        /// </summary>
        /// <param name="categoryName">The Revit category name.</param>
        /// <returns>List of matching patterns.</returns>
        public List<PlacementPattern> GetPatternsForCategory(string categoryName)
        {
            return _patterns.Values
                .Where(p => string.Equals(p.CategoryName, categoryName, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(p => GetEffectiveConfidence(p))
                .ToList();
        }

        /// <summary>
        /// Gets the number of corrections recorded for a specific category and view type.
        /// </summary>
        /// <param name="categoryName">The Revit category name.</param>
        /// <param name="viewType">The view type.</param>
        /// <returns>Number of corrections.</returns>
        public int GetCorrectionCount(string categoryName, TagViewType viewType)
        {
            string key = BuildPatternKey(categoryName, viewType);
            return GetObservationCount(key);
        }

        /// <summary>
        /// Checks whether a prediction is available for the given category and view type
        /// without computing the full prediction. Lighter weight than PredictPlacement.
        /// </summary>
        /// <param name="categoryName">The Revit category name.</param>
        /// <param name="viewType">The view type.</param>
        /// <returns>True if a reliable prediction can be made.</returns>
        public bool HasReliablePrediction(string categoryName, TagViewType viewType)
        {
            if (!TagConfiguration.Instance.Settings.LearningEnabled) return false;

            string key = BuildPatternKey(categoryName, viewType);
            if (!_patterns.TryGetValue(key, out var pattern)) return false;

            double confidence = GetEffectiveConfidence(pattern);
            return MeetsReliabilityThreshold(pattern, confidence);
        }

        #endregion

        #region Pattern Management

        /// <summary>
        /// Clears all learned patterns and corrections for a specific category and view type.
        /// This is a destructive operation that cannot be undone.
        /// </summary>
        /// <param name="categoryName">The category to clear.</param>
        /// <param name="viewType">The view type to clear.</param>
        public void ClearPattern(string categoryName, TagViewType viewType)
        {
            string key = BuildPatternKey(categoryName, viewType);

            _patterns.TryRemove(key, out _);
            if (_corrections.TryRemove(key, out var corrections))
            {
                lock (corrections) { corrections.Clear(); }
            }
            _correctionDistances.TryRemove(key, out _);
            _convergenceHistory.TryRemove(key, out _);

            Logger.Info("Cleared pattern and corrections for {0}", key);
        }

        /// <summary>
        /// Clears all learned data: patterns, corrections, user profiles, and analytics.
        /// This is a destructive operation intended for resetting the learning engine.
        /// </summary>
        public void ClearAllLearningData()
        {
            _patterns.Clear();

            foreach (var kvp in _corrections)
            {
                lock (kvp.Value) { kvp.Value.Clear(); }
            }
            _corrections.Clear();

            _userProfiles.Clear();
            _correctionDistances.Clear();
            _convergenceHistory.Clear();
            _scaleFactors.Clear();

            Interlocked.Exchange(ref _totalCorrections, 0);

            Logger.Warn("All learning data cleared");
        }

        /// <summary>
        /// Imports patterns from an external source, merging with existing patterns.
        /// For each pattern, the version with higher confidence is retained.
        /// </summary>
        /// <param name="patterns">The patterns to import.</param>
        /// <returns>Number of patterns imported or updated.</returns>
        public int ImportPatterns(IEnumerable<PlacementPattern> patterns)
        {
            if (patterns == null) return 0;

            int count = 0;
            foreach (var pattern in patterns)
            {
                if (pattern == null || string.IsNullOrEmpty(pattern.PatternId))
                    continue;

                if (_patterns.TryGetValue(pattern.PatternId, out var existing))
                {
                    if (pattern.Confidence > existing.Confidence ||
                        pattern.ObservationCount > existing.ObservationCount)
                    {
                        _patterns[pattern.PatternId] = pattern;
                        _repository.SavePattern(pattern);
                        count++;
                    }
                }
                else
                {
                    _patterns[pattern.PatternId] = pattern;
                    _repository.SavePattern(pattern);
                    count++;
                }
            }

            Logger.Info("Imported {0} patterns", count);
            return count;
        }

        #endregion

        #region Placement Recording and Preference Scoring

        /// <summary>
        /// Records a successful tag placement for future learning.
        /// Converts the placement into a correction observation for the pattern library.
        /// </summary>
        public void RecordPlacement(TagInstance tag)
        {
            if (tag?.Placement == null || string.IsNullOrEmpty(tag.CategoryName))
                return;

            var correction = new PlacementCorrection
            {
                TagId = tag.TagId,
                CategoryName = tag.CategoryName,
                ViewType = TagViewType.FloorPlan, // Default view type; actual view type set by context
                OriginalPlacement = tag.Placement,
                CorrectedPlacement = tag.Placement,
                CorrectedAt = DateTime.UtcNow,
                ViewScale = 100
            };

            RecordCorrection(correction);
        }

        /// <summary>
        /// Computes a preference score (0.0 to 1.0) for a candidate position based on
        /// learned patterns for the given category and view type.
        /// </summary>
        public double GetPreferenceScore(
            PlacementCandidate candidate, string categoryName, TagViewType viewType)
        {
            if (candidate == null || string.IsNullOrEmpty(categoryName))
                return 0.5;

            string key = BuildPatternKey(categoryName, viewType);
            if (!_patterns.TryGetValue(key, out var pattern) || pattern.Confidence < 0.1)
                return 0.5; // No reliable pattern — neutral score

            double score = 0.5;

            // Bonus if the candidate's relative position matches the pattern's preferred position
            if (candidate.RelativePosition == pattern.PreferredPosition)
                score += 0.3 * pattern.Confidence;

            // Bonus if leader type matches
            if (candidate.LeaderType == pattern.PreferredLeaderType)
                score += 0.1 * pattern.Confidence;

            // Bonus for proximity to the pattern's preferred offset
            double offsetDist = Math.Sqrt(
                Math.Pow(candidate.Position.X - pattern.PreferredOffsetX, 2) +
                Math.Pow(candidate.Position.Y - pattern.PreferredOffsetY, 2));
            if (offsetDist < 0.1)
            {
                double proximityBonus = Math.Max(0, 0.1 * (1.0 - offsetDist / 0.1));
                score += proximityBonus * pattern.Confidence;
            }

            return Math.Max(0.0, Math.Min(1.0, score));
        }

        #endregion

        #region Shutdown

        /// <summary>
        /// Performs a graceful shutdown of the engine, flushing all data to the repository
        /// and stopping the maintenance timer.
        /// </summary>
        /// <returns>A task representing the asynchronous shutdown operation.</returns>
        public async Task ShutdownAsync(CancellationToken cancellationToken = default)
        {
            Logger.Info("Shutting down TagIntelligenceEngine...");

            // Stop the maintenance timer
            if (_maintenanceTimer != null)
            {
                _maintenanceTimer.Change(Timeout.Infinite, Timeout.Infinite);
                _maintenanceTimer.Dispose();
                _maintenanceTimer = null;
            }

            // Final flush of all patterns
            await FlushPatternsAsync(cancellationToken);

            _isInitialized = false;
            Logger.Info("TagIntelligenceEngine shut down. {0} patterns saved, {1} total corrections processed.",
                _patterns.Count, Interlocked.Read(ref _totalCorrections));
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Builds a pattern key from a category name and view type.
        /// The key format is "CategoryName|ViewType".
        /// </summary>
        private static string BuildPatternKey(string categoryName, TagViewType viewType)
        {
            return $"{categoryName}|{viewType}";
        }

        /// <summary>
        /// Parses a pattern key back into its constituent parts.
        /// </summary>
        private static void ParsePatternKey(string key, out string categoryName, out TagViewType viewType)
        {
            int separatorIndex = key.LastIndexOf('|');
            if (separatorIndex < 0)
            {
                categoryName = key;
                viewType = TagViewType.FloorPlan;
                return;
            }

            categoryName = key.Substring(0, separatorIndex);
            string viewTypeStr = key.Substring(separatorIndex + 1);

            if (!Enum.TryParse(viewTypeStr, true, out viewType))
                viewType = TagViewType.FloorPlan;
        }

        #endregion

        #region Inner Types

        /// <summary>
        /// Per-user preference profile that tracks individual placement style preferences.
        /// When per-user tracking is enabled, predictions are blended with the user's
        /// personal preferences for a more personalized experience.
        /// </summary>
        public class UserPreferenceProfile
        {
            /// <summary>User identifier (e.g., Revit username or Windows account).</summary>
            public string UserId { get; set; }

            /// <summary>When this profile was created.</summary>
            public DateTime CreatedAt { get; set; }

            /// <summary>When the user last made a correction.</summary>
            public DateTime LastActiveAt { get; set; }

            /// <summary>Total corrections made by this user.</summary>
            public int TotalCorrections { get; set; }

            /// <summary>
            /// Per-pattern-key list of the user's preferred tag positions.
            /// Key: pattern key (CategoryName|ViewType), Value: list of chosen positions.
            /// </summary>
            public Dictionary<string, List<TagPosition>> PositionPreferences { get; set; }
                = new Dictionary<string, List<TagPosition>>(StringComparer.OrdinalIgnoreCase);

            /// <summary>
            /// Per-pattern-key list of the user's preferred offsets.
            /// Key: pattern key (CategoryName|ViewType), Value: list of offset points.
            /// </summary>
            public Dictionary<string, List<Point2D>> OffsetPreferences { get; set; }
                = new Dictionary<string, List<Point2D>>(StringComparer.OrdinalIgnoreCase);

            /// <summary>
            /// Per-pattern-key list of the user's preferred leader types.
            /// Key: pattern key (CategoryName|ViewType), Value: list of chosen leader types.
            /// </summary>
            public Dictionary<string, List<LeaderType>> LeaderPreferences { get; set; }
                = new Dictionary<string, List<LeaderType>>(StringComparer.OrdinalIgnoreCase);

            /// <summary>
            /// Per-pattern-key list of view scales the user has worked at.
            /// Key: pattern key (CategoryName|ViewType), Value: list of observed scales.
            /// </summary>
            public Dictionary<string, List<double>> ScalePreferences { get; set; }
                = new Dictionary<string, List<double>>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Cached scale adjustment factors for a pair of source/target view scales.
        /// </summary>
        public class ScaleAdjustmentFactor
        {
            /// <summary>Source view scale at which the pattern was learned.</summary>
            public double SourceScale { get; set; }

            /// <summary>Target view scale for the current placement.</summary>
            public double TargetScale { get; set; }

            /// <summary>Multiplier for offset values (X and Y).</summary>
            public double OffsetMultiplier { get; set; }

            /// <summary>Multiplier for leader length.</summary>
            public double LeaderLengthMultiplier { get; set; }
        }

        /// <summary>
        /// Comprehensive learning statistics for the intelligence engine.
        /// </summary>
        public class LearningStatistics
        {
            /// <summary>Total number of corrections ever recorded.</summary>
            public long TotalCorrectionsRecorded { get; set; }

            /// <summary>Total number of patterns currently in memory.</summary>
            public int TotalPatternsLearned { get; set; }

            /// <summary>Number of patterns that meet the reliability threshold.</summary>
            public int ReliablePatternCount { get; set; }

            /// <summary>Average confidence across all patterns.</summary>
            public double AverageConfidence { get; set; }

            /// <summary>Total number of per-user preference profiles.</summary>
            public int TotalUserProfiles { get; set; }

            /// <summary>Whether the learning engine is currently enabled.</summary>
            public bool IsLearningEnabled { get; set; }

            /// <summary>Timestamp of the last persistence flush.</summary>
            public DateTime LastMaintenanceFlush { get; set; }

            /// <summary>Correction counts per category.</summary>
            public Dictionary<string, int> CorrectionsPerCategory { get; set; }
                = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            /// <summary>Detailed statistics for each learned pattern.</summary>
            public List<PatternStatistics> PatternDetails { get; set; }
                = new List<PatternStatistics>();
        }

        /// <summary>
        /// Statistics for a single learned placement pattern.
        /// </summary>
        public class PatternStatistics
        {
            /// <summary>Pattern identifier.</summary>
            public string PatternId { get; set; }

            /// <summary>Category name.</summary>
            public string CategoryName { get; set; }

            /// <summary>View type.</summary>
            public TagViewType ViewType { get; set; }

            /// <summary>Number of observations.</summary>
            public int ObservationCount { get; set; }

            /// <summary>Current effective confidence.</summary>
            public double Confidence { get; set; }

            /// <summary>Learned preferred position.</summary>
            public TagPosition PreferredPosition { get; set; }

            /// <summary>Learned preferred leader type.</summary>
            public LeaderType PreferredLeaderType { get; set; }

            /// <summary>When the pattern was last reinforced.</summary>
            public DateTime LastReinforced { get; set; }

            /// <summary>Whether the pattern meets the reliability threshold for predictions.</summary>
            public bool MeetsReliabilityThreshold { get; set; }
        }

        /// <summary>
        /// Summary of correction activity for a specific category.
        /// </summary>
        public class CategoryCorrectionSummary
        {
            /// <summary>Revit category name.</summary>
            public string CategoryName { get; set; }

            /// <summary>Total number of corrections for this category.</summary>
            public int CorrectionCount { get; set; }

            /// <summary>Average distance between original and corrected positions.</summary>
            public double AverageDistance { get; set; }

            /// <summary>Average confidence across patterns for this category.</summary>
            public double PatternConfidence { get; set; }
        }

        /// <summary>
        /// A single data point in the convergence trend, showing how average correction
        /// distance changes over time.
        /// </summary>
        public class ConvergenceDataPoint
        {
            /// <summary>When this data point was recorded.</summary>
            public DateTime Timestamp { get; set; }

            /// <summary>Average correction distance at this point in time.</summary>
            public double AverageCorrectionDistance { get; set; }

            /// <summary>Total observation count at this point in time.</summary>
            public int ObservationCount { get; set; }
        }

        /// <summary>
        /// An entry in the correction heatmap showing spatial density of corrections.
        /// </summary>
        public class CorrectionHeatmapEntry
        {
            /// <summary>Center of the spatial bucket.</summary>
            public Point2D BucketCenter { get; set; }

            /// <summary>Number of corrections in this bucket.</summary>
            public int CorrectionCount { get; set; }

            /// <summary>Average correction distance for corrections in this bucket.</summary>
            public double AverageDistance { get; set; }

            /// <summary>Normalized density value (0.0 to 1.0) relative to the densest bucket.</summary>
            public double NormalizedDensity { get; set; }
        }

        /// <summary>
        /// Statistical summary of correction distances for a category.
        /// </summary>
        public class CorrectionDistanceSummary
        {
            /// <summary>Category name analyzed.</summary>
            public string CategoryName { get; set; }

            /// <summary>Number of distance samples.</summary>
            public int SampleCount { get; set; }

            /// <summary>Mean (average) correction distance.</summary>
            public double MeanDistance { get; set; }

            /// <summary>Median correction distance.</summary>
            public double MedianDistance { get; set; }

            /// <summary>Standard deviation of correction distances.</summary>
            public double StandardDeviation { get; set; }

            /// <summary>Minimum correction distance observed.</summary>
            public double MinDistance { get; set; }

            /// <summary>Maximum correction distance observed.</summary>
            public double MaxDistance { get; set; }

            /// <summary>25th percentile correction distance.</summary>
            public double Percentile25 { get; set; }

            /// <summary>75th percentile correction distance.</summary>
            public double Percentile75 { get; set; }

            /// <summary>90th percentile correction distance.</summary>
            public double Percentile90 { get; set; }
        }

        #endregion
    }
}
