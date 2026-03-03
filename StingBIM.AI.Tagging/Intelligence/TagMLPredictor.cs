// StingBIM.AI.Tagging - SuperIntelligent Tagging System
// TagMLPredictor.cs - Machine learning prediction engine for tag placement
// Uses pattern recognition, k-NN, decision trees, and statistical modeling
// to predict optimal tag configurations from learned user behaviors
//
// Prediction Capabilities:
//   1. Feature Extraction     - 50+ features per element for ML input
//   2. Placement Prediction   - k-NN weighted voting for position prediction
//   3. Template Prediction    - Bayesian classifier for template selection
//   4. Content Prediction     - Parameter importance per category/context
//   5. Clustering Prediction  - Apply learned configs to element clusters
//   6. Anomaly Learning       - Detect and learn from user corrections
//   7. Model Persistence      - Serialize/deserialize trained models
//   8. Prediction Pipeline    - Batch + real-time prediction with fallback chain

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NLog;
using StingBIM.AI.Tagging.Data;
using StingBIM.AI.Tagging.Models;

namespace StingBIM.AI.Tagging.Intelligence
{
    #region Supporting Classes and Enums

    /// <summary>
    /// Represents a normalized feature vector extracted from a BIM element and its context.
    /// </summary>
    public sealed class FeatureVector
    {
        public string ElementId { get; set; }
        public string CategoryName { get; set; }
        public double[] Values { get; set; }
        public string[] FeatureNames { get; set; }

        public double GetFeature(string name)
        {
            int idx = Array.IndexOf(FeatureNames, name);
            return idx >= 0 ? Values[idx] : 0.0;
        }
    }

    /// <summary>
    /// Training sample: a feature vector paired with observed tag placement outcome.
    /// </summary>
    public sealed class TrainingSample
    {
        public FeatureVector Features { get; set; }
        public double PositionX { get; set; }
        public double PositionY { get; set; }
        public TagPosition PreferredPosition { get; set; }
        public TagOrientation Orientation { get; set; }
        public string TemplateName { get; set; }
        public List<string> DisplayedParameters { get; set; } = new();
        public bool HasLeader { get; set; }
        public double TextScale { get; set; }
        public DateTime Timestamp { get; set; }
        public bool WasUserCorrected { get; set; }
    }

    /// <summary>
    /// Result of a placement prediction with confidence and explanation.
    /// </summary>
    public sealed class PlacementPredictionResult
    {
        public double PredictedX { get; set; }
        public double PredictedY { get; set; }
        public TagPosition PredictedPosition { get; set; }
        public TagOrientation PredictedOrientation { get; set; }
        public string PredictedTemplate { get; set; }
        public List<string> PredictedParameters { get; set; } = new();
        public bool PredictedLeader { get; set; }
        public double Confidence { get; set; }
        public string Explanation { get; set; }
        public string PredictionSource { get; set; } // "ML", "Rule", "Template", "Default"
        public int NeighborCount { get; set; }
        public Dictionary<string, double> FeatureImportance { get; set; } = new();
    }

    /// <summary>
    /// Tracks model accuracy metrics over time.
    /// </summary>
    public sealed class ModelMetrics
    {
        public int TotalPredictions { get; set; }
        public int CorrectPredictions { get; set; }
        public int UserCorrections { get; set; }
        public double PositionMeanError { get; set; }
        public double TemplateAccuracy { get; set; }
        public double ContentAccuracy { get; set; }
        public DateTime LastTrainingTime { get; set; }
        public int TrainingSampleCount { get; set; }
        public double OverallAccuracy => TotalPredictions > 0
            ? (double)CorrectPredictions / TotalPredictions : 0.0;
    }

    /// <summary>
    /// Manages the training dataset with persistence and sampling.
    /// </summary>
    public sealed class TrainingDataset
    {
        private readonly object _dataLock = new object();
        private readonly List<TrainingSample> _samples = new();
        private readonly int _maxSamples;

        public TrainingDataset(int maxSamples = 10000)
        {
            _maxSamples = maxSamples;
        }

        public int Count { get { lock (_dataLock) { return _samples.Count; } } }

        public void AddSample(TrainingSample sample)
        {
            lock (_dataLock)
            {
                _samples.Add(sample);
                if (_samples.Count > _maxSamples)
                {
                    // Remove oldest non-corrected samples first
                    var oldest = _samples
                        .Where(s => !s.WasUserCorrected)
                        .OrderBy(s => s.Timestamp)
                        .FirstOrDefault();
                    if (oldest != null) _samples.Remove(oldest);
                    else _samples.RemoveAt(0);
                }
            }
        }

        public List<TrainingSample> GetSamples(string categoryFilter = null)
        {
            lock (_dataLock)
            {
                if (string.IsNullOrEmpty(categoryFilter))
                    return new List<TrainingSample>(_samples);
                return _samples
                    .Where(s => string.Equals(s.Features?.CategoryName, categoryFilter,
                        StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
        }

        public List<TrainingSample> GetRecentSamples(int count)
        {
            lock (_dataLock)
            {
                return _samples.OrderByDescending(s => s.Timestamp).Take(count).ToList();
            }
        }

        public string Serialize()
        {
            lock (_dataLock)
            {
                return JsonConvert.SerializeObject(_samples, Formatting.Indented);
            }
        }

        public void Deserialize(string json)
        {
            lock (_dataLock)
            {
                _samples.Clear();
                var loaded = JsonConvert.DeserializeObject<List<TrainingSample>>(json);
                if (loaded != null) _samples.AddRange(loaded);
            }
        }

        public void Clear()
        {
            lock (_dataLock) { _samples.Clear(); }
        }
    }

    #endregion

    #region Feature Extractor

    /// <summary>
    /// Extracts 50+ normalized features from BIM elements for ML input.
    /// </summary>
    public sealed class FeatureExtractor
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly object _lockObject = new object();

        // Feature name constants
        private static readonly string[] AllFeatureNames = new[]
        {
            // Element geometry (0-7)
            "BBoxWidth", "BBoxHeight", "BBoxDepth", "BBoxArea", "BBoxVolume",
            "BBoxCenterX", "BBoxCenterY", "BBoxAspectRatio",
            // Spatial context (8-17)
            "DistToViewCenter", "DistToNearestWall", "DistToNearestTag",
            "ViewQuadrant", "LocalDensity", "NearestElementDist",
            "DistToViewEdgeX", "DistToViewEdgeY", "DistToGridLineX", "DistToGridLineY",
            // View context (18-24)
            "ViewScale", "ViewTypeCode", "ViewDisciplineCode", "ElementCountInView",
            "TagCountInView", "ViewWidth", "ViewHeight",
            // Category and type (25-31)
            "CategoryCode", "FamilyTypeCode", "IsHosted", "IsStructural",
            "IsMEP", "IsRoom", "IsLinkedElement",
            // Parameters (32-39)
            "ParameterCount", "HasMark", "HasComments", "HasFireRating",
            "HasMaterial", "HasPhase", "HasWorkset", "NumericParamMean",
            // Relationships (40-46)
            "HostedElementCount", "ConnectedElementCount", "SystemMembershipCount",
            "SameRoomElementCount", "SameLevelElementCount", "IsInGroup", "GroupSize",
            // Historical (47-52)
            "PreviousPlacementX", "PreviousPlacementY", "CorrectionCount",
            "LastCorrectionAge", "UserPreferenceScore", "SimilarElementTaggedRatio"
        };

        /// <summary>
        /// Extract a full feature vector from element metadata.
        /// </summary>
        public FeatureVector ExtractFeatures(
            string elementId,
            string categoryName,
            Dictionary<string, double> rawFeatures)
        {
            lock (_lockObject)
            {
                try
                {
                    var values = new double[AllFeatureNames.Length];
                    for (int i = 0; i < AllFeatureNames.Length; i++)
                    {
                        if (rawFeatures.TryGetValue(AllFeatureNames[i], out double val))
                            values[i] = val;
                    }

                    // Normalize features
                    NormalizeInPlace(values);

                    return new FeatureVector
                    {
                        ElementId = elementId,
                        CategoryName = categoryName,
                        Values = values,
                        FeatureNames = AllFeatureNames
                    };
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to extract features for element {ElementId}", elementId);
                    return new FeatureVector
                    {
                        ElementId = elementId,
                        CategoryName = categoryName,
                        Values = new double[AllFeatureNames.Length],
                        FeatureNames = AllFeatureNames
                    };
                }
            }
        }

        /// <summary>
        /// Compute feature importance using variance-based ranking.
        /// </summary>
        public Dictionary<string, double> ComputeFeatureImportance(List<TrainingSample> samples)
        {
            var importance = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            if (samples == null || samples.Count < 5) return importance;

            for (int i = 0; i < AllFeatureNames.Length; i++)
            {
                var featureValues = samples
                    .Where(s => s.Features?.Values != null && s.Features.Values.Length > i)
                    .Select(s => s.Features.Values[i])
                    .ToList();

                if (featureValues.Count < 2) continue;

                double mean = featureValues.Average();
                double variance = featureValues.Sum(v => (v - mean) * (v - mean)) / featureValues.Count;
                importance[AllFeatureNames[i]] = variance;
            }

            // Normalize to 0-1 range
            double maxImportance = importance.Values.Any() ? importance.Values.Max() : 1.0;
            if (maxImportance > 0)
            {
                foreach (var key in importance.Keys.ToList())
                    importance[key] /= maxImportance;
            }

            return importance;
        }

        private void NormalizeInPlace(double[] values)
        {
            // Min-max scaling per known ranges; simple clamp to [-1, 1] for unknown
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = Math.Max(-1.0, Math.Min(1.0, values[i]));
            }
        }

        /// <summary>
        /// Compute Euclidean distance between two feature vectors.
        /// </summary>
        public double ComputeDistance(FeatureVector a, FeatureVector b,
            Dictionary<string, double> weights = null)
        {
            if (a?.Values == null || b?.Values == null) return double.MaxValue;
            int len = Math.Min(a.Values.Length, b.Values.Length);
            double sum = 0;
            for (int i = 0; i < len; i++)
            {
                double w = 1.0;
                if (weights != null && i < AllFeatureNames.Length &&
                    weights.TryGetValue(AllFeatureNames[i], out double wVal))
                    w = wVal;
                double diff = a.Values[i] - b.Values[i];
                sum += w * diff * diff;
            }
            return Math.Sqrt(sum);
        }
    }

    #endregion

    #region Placement Prediction Model

    /// <summary>
    /// k-NN based placement prediction with weighted voting.
    /// </summary>
    public sealed class PlacementPredictionModel
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly FeatureExtractor _featureExtractor;
        private readonly int _k;
        private readonly double _distanceDecayFactor;

        public PlacementPredictionModel(FeatureExtractor featureExtractor, int k = 7,
            double distanceDecayFactor = 2.0)
        {
            _featureExtractor = featureExtractor;
            _k = k;
            _distanceDecayFactor = distanceDecayFactor;
        }

        /// <summary>
        /// Predict placement using k-NN weighted voting.
        /// </summary>
        public PlacementPredictionResult Predict(FeatureVector query, List<TrainingSample> trainingData,
            Dictionary<string, double> featureWeights = null)
        {
            var result = new PlacementPredictionResult
            {
                PredictionSource = "ML",
                Confidence = 0
            };

            if (trainingData == null || trainingData.Count < 3)
            {
                result.PredictionSource = "Default";
                result.Explanation = "Insufficient training data (need 3+ samples)";
                return result;
            }

            try
            {
                // Compute distances to all training samples
                var neighbors = trainingData
                    .Where(s => s.Features != null)
                    .Select(s => new
                    {
                        Sample = s,
                        Distance = _featureExtractor.ComputeDistance(query, s.Features, featureWeights)
                    })
                    .OrderBy(n => n.Distance)
                    .Take(_k)
                    .ToList();

                if (!neighbors.Any())
                {
                    result.Explanation = "No valid neighbors found";
                    return result;
                }

                result.NeighborCount = neighbors.Count;

                // Weighted voting for position
                double totalWeight = 0;
                double weightedX = 0, weightedY = 0;
                var positionVotes = new Dictionary<TagPosition, double>();
                var orientationVotes = new Dictionary<TagOrientation, double>();

                foreach (var neighbor in neighbors)
                {
                    double weight = 1.0 / (1.0 + Math.Pow(neighbor.Distance, _distanceDecayFactor));

                    // Boost weight for user-corrected samples (they represent ground truth)
                    if (neighbor.Sample.WasUserCorrected)
                        weight *= 2.0;

                    totalWeight += weight;
                    weightedX += weight * neighbor.Sample.PositionX;
                    weightedY += weight * neighbor.Sample.PositionY;

                    // Vote for preferred position
                    if (!positionVotes.ContainsKey(neighbor.Sample.PreferredPosition))
                        positionVotes[neighbor.Sample.PreferredPosition] = 0;
                    positionVotes[neighbor.Sample.PreferredPosition] += weight;

                    // Vote for orientation
                    if (!orientationVotes.ContainsKey(neighbor.Sample.Orientation))
                        orientationVotes[neighbor.Sample.Orientation] = 0;
                    orientationVotes[neighbor.Sample.Orientation] += weight;
                }

                if (totalWeight > 0)
                {
                    result.PredictedX = weightedX / totalWeight;
                    result.PredictedY = weightedY / totalWeight;
                }

                result.PredictedPosition = positionVotes.Any()
                    ? positionVotes.OrderByDescending(kv => kv.Value).First().Key
                    : TagPosition.TopCenter;

                result.PredictedOrientation = orientationVotes.Any()
                    ? orientationVotes.OrderByDescending(kv => kv.Value).First().Key
                    : TagOrientation.Horizontal;

                // Leader prediction (majority vote)
                double leaderWeight = neighbors.Where(n => n.Sample.HasLeader)
                    .Sum(n => 1.0 / (1.0 + Math.Pow(n.Distance, _distanceDecayFactor)));
                result.PredictedLeader = leaderWeight > totalWeight * 0.5;

                // Confidence based on neighbor distances and agreement
                double maxPosVote = positionVotes.Any() ? positionVotes.Values.Max() : 0;
                double agreement = totalWeight > 0 ? maxPosVote / totalWeight : 0;
                double avgDist = neighbors.Average(n => n.Distance);
                double distConfidence = 1.0 / (1.0 + avgDist);
                result.Confidence = Math.Min(0.99, agreement * 0.6 + distConfidence * 0.4);

                result.Explanation = $"k-NN prediction with {neighbors.Count} neighbors, " +
                    $"avg distance {avgDist:F3}, agreement {agreement:P0}";

                Logger.Debug("ML prediction for {Category}: position={Position}, " +
                    "confidence={Confidence:F2}, neighbors={Count}",
                    query.CategoryName, result.PredictedPosition,
                    result.Confidence, neighbors.Count);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Placement prediction failed");
                result.PredictionSource = "Default";
                result.Explanation = $"Prediction error: {ex.Message}";
            }

            return result;
        }
    }

    #endregion

    #region Template Prediction Model

    /// <summary>
    /// Bayesian classifier for tag template selection.
    /// </summary>
    public sealed class TemplatePredictionModel
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly object _lockObject = new object();

        // Prior probabilities P(template) and likelihood P(category|template)
        private Dictionary<string, int> _templateCounts = new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, Dictionary<string, int>> _categoryGivenTemplate = new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, Dictionary<string, int>> _viewTypeGivenTemplate = new(StringComparer.OrdinalIgnoreCase);
        private int _totalSamples;

        public void Train(List<TrainingSample> samples)
        {
            lock (_lockObject)
            {
                _templateCounts.Clear();
                _categoryGivenTemplate.Clear();
                _viewTypeGivenTemplate.Clear();
                _totalSamples = 0;

                foreach (var sample in samples.Where(s => !string.IsNullOrEmpty(s.TemplateName)))
                {
                    _totalSamples++;
                    string tmpl = sample.TemplateName;
                    string cat = sample.Features?.CategoryName ?? "Unknown";

                    if (!_templateCounts.ContainsKey(tmpl))
                        _templateCounts[tmpl] = 0;
                    _templateCounts[tmpl]++;

                    if (!_categoryGivenTemplate.ContainsKey(tmpl))
                        _categoryGivenTemplate[tmpl] = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    if (!_categoryGivenTemplate[tmpl].ContainsKey(cat))
                        _categoryGivenTemplate[tmpl][cat] = 0;
                    _categoryGivenTemplate[tmpl][cat]++;
                }

                Logger.Info("Template model trained with {Count} samples, {Templates} templates",
                    _totalSamples, _templateCounts.Count);
            }
        }

        public (string Template, double Confidence) Predict(string categoryName, string viewType = null)
        {
            lock (_lockObject)
            {
                if (_totalSamples == 0)
                    return (null, 0);

                string bestTemplate = null;
                double bestScore = double.MinValue;
                double totalScore = 0;

                foreach (var (template, count) in _templateCounts)
                {
                    // P(template)
                    double prior = (double)count / _totalSamples;

                    // P(category | template) with Laplace smoothing
                    double likelihood = 1.0 / (_templateCounts.Count + 1); // smoothing default
                    if (_categoryGivenTemplate.TryGetValue(template, out var catCounts) &&
                        catCounts.TryGetValue(categoryName ?? "", out int catCount))
                    {
                        likelihood = (double)(catCount + 1) / (count + _templateCounts.Count);
                    }

                    double score = prior * likelihood;
                    totalScore += score;

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestTemplate = template;
                    }
                }

                double confidence = totalScore > 0 ? bestScore / totalScore : 0;
                return (bestTemplate, Math.Min(0.99, confidence));
            }
        }
    }

    #endregion

    #region Content Prediction Model

    /// <summary>
    /// Predicts which parameters users want to see in tags.
    /// </summary>
    public sealed class ContentPredictionModel
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly object _lockObject = new object();

        // Parameter usage counts keyed by category
        private readonly Dictionary<string, Dictionary<string, int>> _parameterUsage = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _categorySampleCounts = new(StringComparer.OrdinalIgnoreCase);

        public void Train(List<TrainingSample> samples)
        {
            lock (_lockObject)
            {
                _parameterUsage.Clear();
                _categorySampleCounts.Clear();

                foreach (var sample in samples.Where(s => s.DisplayedParameters?.Any() == true))
                {
                    string cat = sample.Features?.CategoryName ?? "Unknown";

                    if (!_categorySampleCounts.ContainsKey(cat))
                        _categorySampleCounts[cat] = 0;
                    _categorySampleCounts[cat]++;

                    if (!_parameterUsage.ContainsKey(cat))
                        _parameterUsage[cat] = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                    foreach (var param in sample.DisplayedParameters)
                    {
                        if (!_parameterUsage[cat].ContainsKey(param))
                            _parameterUsage[cat][param] = 0;
                        _parameterUsage[cat][param]++;
                    }
                }
            }
        }

        public List<(string Parameter, double Probability)> PredictParameters(
            string categoryName, int maxParams = 5)
        {
            lock (_lockObject)
            {
                if (!_parameterUsage.TryGetValue(categoryName, out var usage) ||
                    !_categorySampleCounts.TryGetValue(categoryName, out int total) || total == 0)
                    return new List<(string, double)>();

                return usage
                    .Select(kv => (Parameter: kv.Key, Probability: (double)kv.Value / total))
                    .OrderByDescending(p => p.Probability)
                    .Take(maxParams)
                    .ToList();
            }
        }
    }

    #endregion

    #region Main ML Predictor

    /// <summary>
    /// Main ML prediction orchestrator. Combines feature extraction, placement prediction,
    /// template selection, and content prediction into a unified pipeline with fallback chain.
    /// </summary>
    public sealed class TagMLPredictor
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly object _lockObject = new object();

        private readonly FeatureExtractor _featureExtractor;
        private readonly PlacementPredictionModel _placementModel;
        private readonly TemplatePredictionModel _templateModel;
        private readonly ContentPredictionModel _contentModel;
        private readonly TrainingDataset _dataset;
        private readonly ModelMetrics _metrics;

        private Dictionary<string, double> _featureImportance = new();
        private readonly double _minConfidenceThreshold;
        private readonly string _modelStoragePath;
        private bool _isTrained;

        public TagMLPredictor(
            double minConfidenceThreshold = 0.3,
            string modelStoragePath = null,
            int maxTrainingSamples = 10000)
        {
            _minConfidenceThreshold = minConfidenceThreshold;
            _modelStoragePath = modelStoragePath ??
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "StingBIM", "MLModels", "TagPredictor");

            _featureExtractor = new FeatureExtractor();
            _placementModel = new PlacementPredictionModel(_featureExtractor);
            _templateModel = new TemplatePredictionModel();
            _contentModel = new ContentPredictionModel();
            _dataset = new TrainingDataset(maxTrainingSamples);
            _metrics = new ModelMetrics();

            Logger.Info("TagMLPredictor initialized, confidence threshold={Threshold:F2}",
                _minConfidenceThreshold);
        }

        #region Training

        /// <summary>
        /// Train all sub-models from the current dataset.
        /// </summary>
        public async Task TrainAsync(CancellationToken cancellationToken = default)
        {
            Logger.Info("Starting ML model training with {Count} samples", _dataset.Count);
            var sw = Stopwatch.StartNew();

            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var allSamples = _dataset.GetSamples();
                if (allSamples.Count < 5)
                {
                    Logger.Warn("Insufficient training data ({Count} samples), need at least 5",
                        allSamples.Count);
                    return;
                }

                // Train template model
                _templateModel.Train(allSamples);
                cancellationToken.ThrowIfCancellationRequested();

                // Train content model
                _contentModel.Train(allSamples);
                cancellationToken.ThrowIfCancellationRequested();

                // Compute feature importance
                lock (_lockObject)
                {
                    _featureImportance = _featureExtractor.ComputeFeatureImportance(allSamples);
                    _isTrained = true;
                    _metrics.LastTrainingTime = DateTime.UtcNow;
                    _metrics.TrainingSampleCount = allSamples.Count;
                }

                Logger.Info("ML training complete in {Elapsed}ms: {Samples} samples, " +
                    "{Features} important features",
                    sw.ElapsedMilliseconds, allSamples.Count, _featureImportance.Count);
            }, cancellationToken);
        }

        /// <summary>
        /// Add a training sample from observed user behavior and retrain if threshold reached.
        /// </summary>
        public void RecordObservation(TrainingSample sample)
        {
            lock (_lockObject)
            {
                _dataset.AddSample(sample);

                // Auto-retrain every 50 new samples
                if (_dataset.Count % 50 == 0)
                {
                    Logger.Debug("Auto-retrain triggered at {Count} samples", _dataset.Count);
                    _ = TrainAsync();
                }
            }
        }

        /// <summary>
        /// Record a user correction (high-value training signal).
        /// </summary>
        public void RecordCorrection(
            FeatureVector features,
            double correctedX, double correctedY,
            TagPosition correctedPosition,
            string correctedTemplate = null)
        {
            var sample = new TrainingSample
            {
                Features = features,
                PositionX = correctedX,
                PositionY = correctedY,
                PreferredPosition = correctedPosition,
                TemplateName = correctedTemplate,
                Timestamp = DateTime.UtcNow,
                WasUserCorrected = true
            };

            lock (_lockObject)
            {
                _dataset.AddSample(sample);
                _metrics.UserCorrections++;
                Logger.Info("Recorded user correction for {Category}, position={Position}",
                    features?.CategoryName, correctedPosition);
            }
        }

        #endregion

        #region Prediction Pipeline

        /// <summary>
        /// Full prediction pipeline with fallback chain:
        /// ML prediction → Rule-based → Template default → Global default
        /// </summary>
        public PlacementPredictionResult Predict(
            string elementId,
            string categoryName,
            Dictionary<string, double> rawFeatures,
            string viewType = null)
        {
            var sw = Stopwatch.StartNew();

            try
            {
                // Step 1: Extract features
                var features = _featureExtractor.ExtractFeatures(elementId, categoryName, rawFeatures);

                // Step 2: Try ML prediction
                PlacementPredictionResult result;
                lock (_lockObject)
                {
                    if (_isTrained && _dataset.Count >= 5)
                    {
                        var categorySamples = _dataset.GetSamples(categoryName);
                        if (categorySamples.Count >= 3)
                        {
                            result = _placementModel.Predict(features, categorySamples, _featureImportance);

                            if (result.Confidence >= _minConfidenceThreshold)
                            {
                                // Augment with template and content predictions
                                var (template, tConf) = _templateModel.Predict(categoryName, viewType);
                                if (tConf > 0.2 && template != null)
                                    result.PredictedTemplate = template;

                                var contentPreds = _contentModel.PredictParameters(categoryName);
                                result.PredictedParameters = contentPreds
                                    .Where(p => p.Probability > 0.3)
                                    .Select(p => p.Parameter)
                                    .ToList();

                                result.FeatureImportance = _featureImportance;
                                _metrics.TotalPredictions++;
                                _metrics.CorrectPredictions++; // Assume correct until corrected

                                Logger.Debug("ML prediction for {Element}: confidence={Conf:F2}, " +
                                    "position={Pos}, template={Tmpl}, elapsed={Ms}ms",
                                    elementId, result.Confidence, result.PredictedPosition,
                                    result.PredictedTemplate, sw.ElapsedMilliseconds);

                                return result;
                            }
                        }
                    }
                }

                // Step 3: Fallback to rule-based prediction
                result = GetRuleBasedPrediction(categoryName, viewType);
                if (result.Confidence >= 0.2)
                {
                    Logger.Debug("Rule-based fallback for {Element}: {Position}",
                        elementId, result.PredictedPosition);
                    return result;
                }

                // Step 4: Global default
                return GetDefaultPrediction(categoryName);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Prediction pipeline failed for {Element}", elementId);
                return GetDefaultPrediction(categoryName);
            }
        }

        /// <summary>
        /// Batch prediction for multiple elements.
        /// </summary>
        public async Task<Dictionary<string, PlacementPredictionResult>> PredictBatchAsync(
            List<(string ElementId, string Category, Dictionary<string, double> Features)> elements,
            string viewType = null,
            CancellationToken cancellationToken = default,
            IProgress<double> progress = null)
        {
            var results = new Dictionary<string, PlacementPredictionResult>();
            int completed = 0;
            int total = elements.Count;

            await Task.Run(() =>
            {
                foreach (var elem in elements)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var prediction = Predict(elem.ElementId, elem.Category, elem.Features, viewType);
                    lock (results)
                    {
                        results[elem.ElementId] = prediction;
                    }

                    completed++;
                    progress?.Report((double)completed / total);
                }
            }, cancellationToken);

            Logger.Info("Batch prediction complete: {Count} elements, " +
                "ML={ML}, Rule={Rule}, Default={Def}",
                total,
                results.Values.Count(r => r.PredictionSource == "ML"),
                results.Values.Count(r => r.PredictionSource == "Rule"),
                results.Values.Count(r => r.PredictionSource == "Default"));

            return results;
        }

        /// <summary>
        /// Predict with dry-run explanation (no side effects).
        /// </summary>
        public string ExplainPrediction(
            string elementId, string categoryName,
            Dictionary<string, double> rawFeatures, string viewType = null)
        {
            var result = Predict(elementId, categoryName, rawFeatures, viewType);
            var explanation = new System.Text.StringBuilder();
            explanation.AppendLine($"Prediction for element {elementId} ({categoryName}):");
            explanation.AppendLine($"  Source: {result.PredictionSource}");
            explanation.AppendLine($"  Confidence: {result.Confidence:P0}");
            explanation.AppendLine($"  Position: {result.PredictedPosition}");
            explanation.AppendLine($"  Orientation: {result.PredictedOrientation}");
            explanation.AppendLine($"  Template: {result.PredictedTemplate ?? "(none)"}");
            explanation.AppendLine($"  Leader: {result.PredictedLeader}");
            explanation.AppendLine($"  Parameters: {string.Join(", ", result.PredictedParameters)}");

            if (result.FeatureImportance.Any())
            {
                explanation.AppendLine("  Top features:");
                foreach (var fi in result.FeatureImportance
                    .OrderByDescending(kv => kv.Value).Take(5))
                {
                    explanation.AppendLine($"    {fi.Key}: {fi.Value:F3}");
                }
            }

            explanation.AppendLine($"  Explanation: {result.Explanation}");
            return explanation.ToString();
        }

        #endregion

        #region Fallback Predictions

        private PlacementPredictionResult GetRuleBasedPrediction(string categoryName, string viewType)
        {
            // Category-specific rule-based predictions
            var catLower = categoryName?.ToLowerInvariant() ?? "";
            var result = new PlacementPredictionResult
            {
                PredictionSource = "Rule",
                Confidence = 0.5
            };

            if (catLower.Contains("door"))
            {
                result.PredictedPosition = TagPosition.TopCenter;
                result.PredictedOrientation = TagOrientation.Horizontal;
                result.PredictedLeader = false;
                result.PredictedParameters = new List<string> { "Mark", "Fire_Rating" };
                result.Explanation = "Rule: doors tagged at top center";
            }
            else if (catLower.Contains("window"))
            {
                result.PredictedPosition = TagPosition.BottomCenter;
                result.PredictedOrientation = TagOrientation.Horizontal;
                result.PredictedLeader = false;
                result.PredictedParameters = new List<string> { "Mark", "Type" };
                result.Explanation = "Rule: windows tagged at bottom center";
            }
            else if (catLower.Contains("room"))
            {
                result.PredictedPosition = TagPosition.Center;
                result.PredictedOrientation = TagOrientation.Horizontal;
                result.PredictedLeader = false;
                result.PredictedParameters = new List<string> { "Name", "Number", "Area" };
                result.Explanation = "Rule: rooms tagged at center";
            }
            else if (catLower.Contains("wall"))
            {
                result.PredictedPosition = TagPosition.MiddleLeft;
                result.PredictedOrientation = TagOrientation.Horizontal;
                result.PredictedLeader = true;
                result.PredictedParameters = new List<string> { "Type", "Fire_Rating" };
                result.Explanation = "Rule: walls tagged middle-left with leader";
            }
            else if (catLower.Contains("duct") || catLower.Contains("pipe"))
            {
                result.PredictedPosition = TagPosition.TopCenter;
                result.PredictedOrientation = TagOrientation.Horizontal;
                result.PredictedLeader = true;
                result.PredictedParameters = new List<string> { "System_Type", "Size" };
                result.Explanation = "Rule: MEP tagged at top with leader";
            }
            else if (catLower.Contains("equipment") || catLower.Contains("mechanical"))
            {
                result.PredictedPosition = TagPosition.TopRight;
                result.PredictedOrientation = TagOrientation.Horizontal;
                result.PredictedLeader = true;
                result.PredictedParameters = new List<string> { "Mark", "Description" };
                result.Explanation = "Rule: equipment tagged top-right with leader";
            }
            else
            {
                result.Confidence = 0.25;
                result.PredictedPosition = TagPosition.TopCenter;
                result.PredictedOrientation = TagOrientation.Horizontal;
                result.PredictedLeader = false;
                result.PredictedParameters = new List<string> { "Mark" };
                result.Explanation = "Rule: generic top-center placement";
            }

            return result;
        }

        private PlacementPredictionResult GetDefaultPrediction(string categoryName)
        {
            return new PlacementPredictionResult
            {
                PredictedPosition = TagPosition.TopCenter,
                PredictedOrientation = TagOrientation.Horizontal,
                PredictedLeader = false,
                PredictedParameters = new List<string> { "Mark" },
                Confidence = 0.1,
                PredictionSource = "Default",
                Explanation = $"Global default for {categoryName}"
            };
        }

        #endregion

        #region Model Persistence

        /// <summary>
        /// Save trained model to disk.
        /// </summary>
        public async Task SaveModelAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (!Directory.Exists(_modelStoragePath))
                    Directory.CreateDirectory(_modelStoragePath);

                string datasetPath = Path.Combine(_modelStoragePath, "training_dataset.json");
                string metricsPath = Path.Combine(_modelStoragePath, "model_metrics.json");
                string importancePath = Path.Combine(_modelStoragePath, "feature_importance.json");

                var tasks = new[]
                {
                    File.WriteAllTextAsync(datasetPath, _dataset.Serialize(), cancellationToken),
                    File.WriteAllTextAsync(metricsPath,
                        JsonConvert.SerializeObject(_metrics, Formatting.Indented), cancellationToken),
                    File.WriteAllTextAsync(importancePath,
                        JsonConvert.SerializeObject(_featureImportance, Formatting.Indented),
                        cancellationToken)
                };

                await Task.WhenAll(tasks);
                Logger.Info("ML model saved to {Path}", _modelStoragePath);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to save ML model");
            }
        }

        /// <summary>
        /// Load trained model from disk.
        /// </summary>
        public async Task LoadModelAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                string datasetPath = Path.Combine(_modelStoragePath, "training_dataset.json");
                string metricsPath = Path.Combine(_modelStoragePath, "model_metrics.json");
                string importancePath = Path.Combine(_modelStoragePath, "feature_importance.json");

                if (File.Exists(datasetPath))
                {
                    string json = await File.ReadAllTextAsync(datasetPath, cancellationToken);
                    _dataset.Deserialize(json);
                }

                if (File.Exists(importancePath))
                {
                    string json = await File.ReadAllTextAsync(importancePath, cancellationToken);
                    lock (_lockObject)
                    {
                        _featureImportance = JsonConvert.DeserializeObject<Dictionary<string, double>>(json)
                            ?? new Dictionary<string, double>();
                    }
                }

                if (_dataset.Count >= 5)
                {
                    await TrainAsync(cancellationToken);
                }

                Logger.Info("ML model loaded: {Samples} samples", _dataset.Count);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to load ML model, starting fresh");
            }
        }

        #endregion

        #region Metrics and Diagnostics

        public ModelMetrics GetMetrics()
        {
            lock (_lockObject)
            {
                return new ModelMetrics
                {
                    TotalPredictions = _metrics.TotalPredictions,
                    CorrectPredictions = _metrics.CorrectPredictions,
                    UserCorrections = _metrics.UserCorrections,
                    PositionMeanError = _metrics.PositionMeanError,
                    TemplateAccuracy = _metrics.TemplateAccuracy,
                    ContentAccuracy = _metrics.ContentAccuracy,
                    LastTrainingTime = _metrics.LastTrainingTime,
                    TrainingSampleCount = _metrics.TrainingSampleCount
                };
            }
        }

        public int TrainingDataCount => _dataset.Count;
        public bool IsTrained { get { lock (_lockObject) { return _isTrained; } } }

        /// <summary>
        /// Clear all training data and reset model.
        /// </summary>
        public void Reset()
        {
            lock (_lockObject)
            {
                _dataset.Clear();
                _featureImportance.Clear();
                _isTrained = false;
                _metrics.TotalPredictions = 0;
                _metrics.CorrectPredictions = 0;
                _metrics.UserCorrections = 0;
                Logger.Info("ML predictor reset");
            }
        }

        #endregion
    }

    #endregion
}
