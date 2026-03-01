// StingBIM.AI.Collaboration - Model Coordination Engine
// Advanced clash detection and coordination inspired by BIM 360 Coordinate + Navisworks

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StingBIM.AI.Core.Common;

namespace StingBIM.AI.Collaboration.Coordination
{
    /// <summary>
    /// Comprehensive model coordination engine with AI-powered clash detection,
    /// prediction, and resolution suggestions
    /// </summary>
    public class ModelCoordinationEngine : IAsyncDisposable
    {
        private readonly ConcurrentDictionary<string, CoordinationModel> _models = new();
        private readonly ConcurrentDictionary<string, ClashTest> _clashTests = new();
        private readonly ConcurrentDictionary<string, ClashGroup> _clashGroups = new();
        private readonly ConcurrentDictionary<string, ClashResult> _clashes = new();
        private readonly ClashDetectionAI _clashAI;
        private readonly object _lockObject = new();
        private CancellationTokenSource? _monitorCts;

        public event EventHandler<ClashDetectedEventArgs>? ClashDetected;
        public event EventHandler<ClashResolvedEventArgs>? ClashResolved;
        public event EventHandler<ClashPredictedEventArgs>? ClashPredicted;
        public event EventHandler<CoordinationReportReadyEventArgs>? ReportReady;

        public ModelCoordinationEngine()
        {
            _clashAI = new ClashDetectionAI(this);
        }

        #region Model Management

        /// <summary>
        /// Register a model for coordination
        /// </summary>
        public CoordinationModel RegisterModel(
            string modelId,
            string name,
            string discipline,
            string filePath,
            ModelFormat format)
        {
            var model = new CoordinationModel
            {
                Id = modelId,
                Name = name,
                Discipline = discipline,
                FilePath = filePath,
                Format = format,
                RegisteredAt = DateTime.UtcNow,
                Status = ModelStatus.Registered
            };

            _models[modelId] = model;
            return model;
        }

        /// <summary>
        /// Update model geometry (triggers re-coordination)
        /// </summary>
        public async Task UpdateModelAsync(
            string modelId,
            List<ModelElement> elements,
            CancellationToken ct = default)
        {
            if (!_models.TryGetValue(modelId, out var model))
                throw new ModelNotFoundException(modelId);

            model.Elements = elements;
            model.LastUpdated = DateTime.UtcNow;
            model.ElementCount = elements.Count;
            model.Status = ModelStatus.Updated;

            // Build spatial index for fast intersection tests
            model.SpatialIndex = BuildSpatialIndex(elements);

            // Auto-run affected clash tests
            var affectedTests = _clashTests.Values
                .Where(t => t.ModelAId == modelId || t.ModelBId == modelId)
                .ToList();

            foreach (var test in affectedTests)
            {
                await RunClashTestAsync(test.Id, ct);
            }
        }

        /// <summary>
        /// Get all registered models
        /// </summary>
        public List<CoordinationModel> GetModels() => _models.Values.ToList();

        /// <summary>
        /// Remove model from coordination
        /// </summary>
        public void RemoveModel(string modelId)
        {
            _models.TryRemove(modelId, out _);

            // Remove associated clash tests
            var testsToRemove = _clashTests.Values
                .Where(t => t.ModelAId == modelId || t.ModelBId == modelId)
                .Select(t => t.Id)
                .ToList();

            foreach (var testId in testsToRemove)
            {
                _clashTests.TryRemove(testId, out _);
            }
        }

        #endregion

        #region Clash Test Configuration

        /// <summary>
        /// Create a new clash test between two models or sets
        /// </summary>
        public ClashTest CreateClashTest(
            string name,
            ClashTestType type,
            SelectionSet selectionA,
            SelectionSet selectionB,
            ClashTestSettings? settings = null)
        {
            var test = new ClashTest
            {
                Id = Guid.NewGuid().ToString("N")[..12],
                Name = name,
                Type = type,
                SelectionA = selectionA,
                SelectionB = selectionB,
                ModelAId = selectionA.ModelId,
                ModelBId = selectionB.ModelId,
                Settings = settings ?? new ClashTestSettings(),
                CreatedAt = DateTime.UtcNow,
                Status = ClashTestStatus.Ready
            };

            _clashTests[test.Id] = test;
            return test;
        }

        /// <summary>
        /// Create standard discipline clash tests
        /// </summary>
        public List<ClashTest> CreateStandardClashTests()
        {
            var tests = new List<ClashTest>();
            var disciplines = new[] { "Architectural", "Structural", "Mechanical", "Electrical", "Plumbing" };

            // Create inter-discipline clash tests
            for (int i = 0; i < disciplines.Length; i++)
            {
                for (int j = i + 1; j < disciplines.Length; j++)
                {
                    var modelsA = _models.Values.Where(m => m.Discipline == disciplines[i]).ToList();
                    var modelsB = _models.Values.Where(m => m.Discipline == disciplines[j]).ToList();

                    if (modelsA.Any() && modelsB.Any())
                    {
                        foreach (var modelA in modelsA)
                        {
                            foreach (var modelB in modelsB)
                            {
                                var test = CreateClashTest(
                                    $"{disciplines[i]} vs {disciplines[j]}",
                                    ClashTestType.Hard,
                                    new SelectionSet { ModelId = modelA.Id, Name = disciplines[i] },
                                    new SelectionSet { ModelId = modelB.Id, Name = disciplines[j] }
                                );
                                tests.Add(test);
                            }
                        }
                    }
                }
            }

            return tests;
        }

        /// <summary>
        /// Get clash test by ID
        /// </summary>
        public ClashTest? GetClashTest(string testId)
            => _clashTests.TryGetValue(testId, out var test) ? test : null;

        /// <summary>
        /// Get all clash tests
        /// </summary>
        public List<ClashTest> GetClashTests() => _clashTests.Values.ToList();

        #endregion

        #region Clash Detection

        /// <summary>
        /// Run a specific clash test
        /// </summary>
        public async Task<ClashTestResult> RunClashTestAsync(
            string testId,
            CancellationToken ct = default)
        {
            if (!_clashTests.TryGetValue(testId, out var test))
                throw new ClashTestNotFoundException(testId);

            test.Status = ClashTestStatus.Running;
            test.LastRun = DateTime.UtcNow;

            var modelA = _models.GetValueOrDefault(test.ModelAId ?? "");
            var modelB = _models.GetValueOrDefault(test.ModelBId ?? "");

            if (modelA?.Elements == null || modelB?.Elements == null)
            {
                test.Status = ClashTestStatus.Error;
                return new ClashTestResult { TestId = testId, Error = "Models not loaded" };
            }

            var clashes = new List<ClashResult>();
            var tolerance = test.Settings.Tolerance;

            // Perform clash detection using spatial index
            foreach (var elementA in GetFilteredElements(modelA.Elements, test.SelectionA))
            {
                ct.ThrowIfCancellationRequested();

                var potentialClashes = modelB.SpatialIndex?.Query(elementA.BoundingBox.Expand(tolerance))
                    ?? modelB.Elements;

                var filteredB = GetFilteredElements(potentialClashes.ToList(), test.SelectionB);

                foreach (var elementB in filteredB)
                {
                    var clash = DetectClash(elementA, elementB, test.Type, tolerance);
                    if (clash != null)
                    {
                        clash.TestId = testId;
                        clash.TestName = test.Name;
                        clashes.Add(clash);
                    }
                }
            }

            // Group clashes
            var groupedClashes = GroupClashes(clashes, test.Settings.GroupingMethod);

            // AI analysis
            var aiAnalysis = await _clashAI.AnalyzeClashesAsync(groupedClashes, ct);

            // Store results
            foreach (var clash in clashes)
            {
                _clashes[clash.Id] = clash;
                ClashDetected?.Invoke(this, new ClashDetectedEventArgs(clash));
            }

            test.Status = ClashTestStatus.Complete;
            test.LastClashCount = clashes.Count;
            test.NewClashCount = clashes.Count(c => c.Status == ClashStatus.New);

            var result = new ClashTestResult
            {
                TestId = testId,
                Clashes = groupedClashes,
                TotalCount = clashes.Count,
                NewCount = test.NewClashCount,
                ActiveCount = clashes.Count(c => c.Status == ClashStatus.Active),
                ResolvedCount = clashes.Count(c => c.Status == ClashStatus.Resolved),
                AIAnalysis = aiAnalysis,
                ExecutionTime = DateTime.UtcNow - test.LastRun!.Value
            };

            return result;
        }

        /// <summary>
        /// Run all clash tests
        /// </summary>
        public async Task<List<ClashTestResult>> RunAllClashTestsAsync(CancellationToken ct = default)
        {
            var results = new List<ClashTestResult>();

            foreach (var test in _clashTests.Values)
            {
                var result = await RunClashTestAsync(test.Id, ct);
                results.Add(result);
            }

            return results;
        }

        private ClashResult? DetectClash(
            ModelElement elementA,
            ModelElement elementB,
            ClashTestType type,
            double tolerance)
        {
            // Check bounding box intersection first (fast)
            if (!elementA.BoundingBox.Intersects(elementB.BoundingBox, tolerance))
                return null;

            // Detailed geometry check based on type
            var intersection = CalculateIntersection(elementA, elementB, type);
            if (intersection == null)
                return null;

            var clash = new ClashResult
            {
                Id = Guid.NewGuid().ToString("N")[..12],
                Element1 = new ClashElement
                {
                    Id = elementA.Id,
                    Name = elementA.Name,
                    Category = elementA.Category,
                    ModelId = elementA.ModelId,
                    UniqueId = elementA.UniqueId
                },
                Element2 = new ClashElement
                {
                    Id = elementB.Id,
                    Name = elementB.Name,
                    Category = elementB.Category,
                    ModelId = elementB.ModelId,
                    UniqueId = elementB.UniqueId
                },
                ClashPoint = intersection.Point,
                Distance = intersection.Distance,
                Volume = intersection.Volume,
                Type = type,
                Severity = DetermineSeverity(intersection),
                Status = ClashStatus.New,
                DetectedAt = DateTime.UtcNow
            };

            return clash;
        }

        private IntersectionResult? CalculateIntersection(
            ModelElement elementA,
            ModelElement elementB,
            ClashTestType type)
        {
            // Simplified intersection calculation
            // In production, would use actual geometry intersection
            var boxA = elementA.BoundingBox;
            var boxB = elementB.BoundingBox;

            if (!boxA.Intersects(boxB, 0))
                return null;

            // Calculate intersection point (center of overlap)
            var minX = Math.Max(boxA.Min.X, boxB.Min.X);
            var minY = Math.Max(boxA.Min.Y, boxB.Min.Y);
            var minZ = Math.Max(boxA.Min.Z, boxB.Min.Z);
            var maxX = Math.Min(boxA.Max.X, boxB.Max.X);
            var maxY = Math.Min(boxA.Max.Y, boxB.Max.Y);
            var maxZ = Math.Min(boxA.Max.Z, boxB.Max.Z);

            if (minX >= maxX || minY >= maxY || minZ >= maxZ)
                return null;

            var volume = (maxX - minX) * (maxY - minY) * (maxZ - minZ);
            var distance = type == ClashTestType.Clearance
                ? CalculateClearance(boxA, boxB)
                : -Math.Pow(volume, 1.0 / 3.0); // Negative = penetration

            return new IntersectionResult
            {
                Point = new Point3D
                {
                    X = (minX + maxX) / 2,
                    Y = (minY + maxY) / 2,
                    Z = (minZ + maxZ) / 2
                },
                Distance = distance,
                Volume = volume
            };
        }

        private static double CalculateClearance(BoundingBox3D a, BoundingBox3D b)
        {
            var dx = Math.Max(0, Math.Max(a.Min.X - b.Max.X, b.Min.X - a.Max.X));
            var dy = Math.Max(0, Math.Max(a.Min.Y - b.Max.Y, b.Min.Y - a.Max.Y));
            var dz = Math.Max(0, Math.Max(a.Min.Z - b.Max.Z, b.Min.Z - a.Max.Z));
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        private static ClashSeverity DetermineSeverity(IntersectionResult intersection)
        {
            if (intersection.Volume > 1.0) return ClashSeverity.Critical; // > 1 cubic meter
            if (intersection.Volume > 0.1) return ClashSeverity.Major;
            if (intersection.Volume > 0.01) return ClashSeverity.Minor;
            return ClashSeverity.Info;
        }

        private static List<ModelElement> GetFilteredElements(List<ModelElement> elements, SelectionSet selection)
        {
            var filtered = elements.AsEnumerable();

            if (selection.Categories?.Any() == true)
                filtered = filtered.Where(e => selection.Categories.Contains(e.Category));

            if (selection.ExcludeCategories?.Any() == true)
                filtered = filtered.Where(e => !selection.ExcludeCategories.Contains(e.Category));

            if (selection.Levels?.Any() == true)
                filtered = filtered.Where(e => e.Level != null && selection.Levels.Contains(e.Level));

            if (selection.ElementIds?.Any() == true)
                filtered = filtered.Where(e => selection.ElementIds.Contains(e.Id));

            return filtered.ToList();
        }

        private List<ClashResult> GroupClashes(List<ClashResult> clashes, ClashGroupingMethod method)
        {
            if (method == ClashGroupingMethod.None)
                return clashes;

            var groups = method switch
            {
                ClashGroupingMethod.ByElement => clashes.GroupBy(c => c.Element1.Id),
                ClashGroupingMethod.ByCategory => clashes.GroupBy(c => $"{c.Element1.Category}_{c.Element2.Category}"),
                ClashGroupingMethod.ByLocation => clashes.GroupBy(c => GetLocationKey(c.ClashPoint)),
                ClashGroupingMethod.ByLevel => clashes.GroupBy(c => c.Level ?? "Unknown"),
                _ => clashes.GroupBy(c => c.Id)
            };

            var groupedClashes = new List<ClashResult>();
            foreach (var group in groups)
            {
                var clashGroup = new ClashGroup
                {
                    Id = Guid.NewGuid().ToString("N")[..12],
                    Name = $"Group: {group.Key}",
                    ClashIds = group.Select(c => c.Id).ToList(),
                    ClashCount = group.Count(),
                    Severity = group.Max(c => c.Severity)
                };
                _clashGroups[clashGroup.Id] = clashGroup;

                foreach (var clash in group)
                {
                    clash.GroupId = clashGroup.Id;
                    groupedClashes.Add(clash);
                }
            }

            return groupedClashes;
        }

        private static string GetLocationKey(Point3D point)
        {
            // Grid-based location grouping (5m grid)
            var gridX = Math.Floor(point.X / 5000) * 5000;
            var gridY = Math.Floor(point.Y / 5000) * 5000;
            var gridZ = Math.Floor(point.Z / 3000) * 3000; // 3m for levels
            return $"{gridX}_{gridY}_{gridZ}";
        }

        private SpatialIndex BuildSpatialIndex(List<ModelElement> elements)
        {
            return new SpatialIndex(elements);
        }

        #endregion

        #region Clash Resolution

        /// <summary>
        /// Update clash status
        /// </summary>
        public void UpdateClashStatus(
            string clashId,
            ClashStatus status,
            string updatedBy,
            string? comment = null)
        {
            if (!_clashes.TryGetValue(clashId, out var clash))
                throw new ClashNotFoundException(clashId);

            var previousStatus = clash.Status;
            clash.Status = status;
            clash.UpdatedAt = DateTime.UtcNow;
            clash.UpdatedBy = updatedBy;

            if (!string.IsNullOrEmpty(comment))
            {
                clash.Comments ??= new List<ClashComment>();
                clash.Comments.Add(new ClashComment
                {
                    Id = Guid.NewGuid().ToString("N")[..8],
                    Author = updatedBy,
                    Content = comment,
                    CreatedAt = DateTime.UtcNow,
                    StatusChange = $"{previousStatus} → {status}"
                });
            }

            if (status == ClashStatus.Resolved)
            {
                clash.ResolvedAt = DateTime.UtcNow;
                clash.ResolvedBy = updatedBy;
                ClashResolved?.Invoke(this, new ClashResolvedEventArgs(clash, updatedBy));
            }
        }

        /// <summary>
        /// Assign clash to user
        /// </summary>
        public void AssignClash(string clashId, string assignedTo, string assignedBy)
        {
            if (!_clashes.TryGetValue(clashId, out var clash))
                throw new ClashNotFoundException(clashId);

            clash.AssignedTo = assignedTo;
            clash.AssignedBy = assignedBy;
            clash.AssignedAt = DateTime.UtcNow;
            clash.Status = ClashStatus.Active;
        }

        /// <summary>
        /// Bulk update clash statuses
        /// </summary>
        public void BulkUpdateClashStatus(
            List<string> clashIds,
            ClashStatus status,
            string updatedBy,
            string? comment = null)
        {
            foreach (var clashId in clashIds)
            {
                UpdateClashStatus(clashId, status, updatedBy, comment);
            }
        }

        /// <summary>
        /// Get AI resolution suggestions
        /// </summary>
        public async Task<List<ResolutionSuggestion>> GetResolutionSuggestionsAsync(
            string clashId,
            CancellationToken ct = default)
        {
            if (!_clashes.TryGetValue(clashId, out var clash))
                throw new ClashNotFoundException(clashId);

            return await _clashAI.SuggestResolutionsAsync(clash, ct);
        }

        #endregion

        #region Clash Queries

        /// <summary>
        /// Get clash by ID
        /// </summary>
        public ClashResult? GetClash(string clashId)
            => _clashes.TryGetValue(clashId, out var clash) ? clash : null;

        /// <summary>
        /// Query clashes with filtering
        /// </summary>
        public ClashQueryResult QueryClashes(ClashQuery query)
        {
            var clashes = _clashes.Values.AsEnumerable();

            if (!string.IsNullOrEmpty(query.TestId))
                clashes = clashes.Where(c => c.TestId == query.TestId);

            if (query.Statuses?.Any() == true)
                clashes = clashes.Where(c => query.Statuses.Contains(c.Status));

            if (query.Severities?.Any() == true)
                clashes = clashes.Where(c => query.Severities.Contains(c.Severity));

            if (!string.IsNullOrEmpty(query.AssignedTo))
                clashes = clashes.Where(c => c.AssignedTo == query.AssignedTo);

            if (!string.IsNullOrEmpty(query.ModelId))
                clashes = clashes.Where(c => c.Element1.ModelId == query.ModelId || c.Element2.ModelId == query.ModelId);

            if (!string.IsNullOrEmpty(query.Category))
                clashes = clashes.Where(c => c.Element1.Category == query.Category || c.Element2.Category == query.Category);

            if (!string.IsNullOrEmpty(query.GroupId))
                clashes = clashes.Where(c => c.GroupId == query.GroupId);

            var total = clashes.Count();

            clashes = query.SortBy switch
            {
                ClashSortField.Severity => query.SortDescending
                    ? clashes.OrderByDescending(c => c.Severity)
                    : clashes.OrderBy(c => c.Severity),
                ClashSortField.Distance => query.SortDescending
                    ? clashes.OrderByDescending(c => c.Distance)
                    : clashes.OrderBy(c => c.Distance),
                ClashSortField.Status => query.SortDescending
                    ? clashes.OrderByDescending(c => c.Status)
                    : clashes.OrderBy(c => c.Status),
                _ => clashes.OrderByDescending(c => c.DetectedAt)
            };

            return new ClashQueryResult
            {
                Clashes = clashes.Skip(query.Skip).Take(query.Take).ToList(),
                TotalCount = total,
                Skip = query.Skip,
                Take = query.Take
            };
        }

        /// <summary>
        /// Get clashes for an element
        /// </summary>
        public List<ClashResult> GetClashesForElement(string elementId)
        {
            return _clashes.Values
                .Where(c => c.Element1.Id == elementId || c.Element2.Id == elementId)
                .ToList();
        }

        /// <summary>
        /// Get clash statistics
        /// </summary>
        public ClashStatistics GetStatistics(string? testId = null)
        {
            var clashes = testId != null
                ? _clashes.Values.Where(c => c.TestId == testId)
                : _clashes.Values;

            var clashList = clashes.ToList();

            return new ClashStatistics
            {
                TotalCount = clashList.Count,
                NewCount = clashList.Count(c => c.Status == ClashStatus.New),
                ActiveCount = clashList.Count(c => c.Status == ClashStatus.Active),
                ResolvedCount = clashList.Count(c => c.Status == ClashStatus.Resolved),
                ApprovedCount = clashList.Count(c => c.Status == ClashStatus.Approved),
                IgnoredCount = clashList.Count(c => c.Status == ClashStatus.Ignored),

                BySeverity = clashList.GroupBy(c => c.Severity)
                    .ToDictionary(g => g.Key, g => g.Count()),

                ByCategory = clashList
                    .SelectMany(c => new[] { c.Element1.Category, c.Element2.Category })
                    .GroupBy(cat => cat)
                    .ToDictionary(g => g.Key, g => g.Count()),

                ByTest = clashList.GroupBy(c => c.TestName ?? "Unknown")
                    .ToDictionary(g => g.Key, g => g.Count()),

                ByAssignee = clashList
                    .Where(c => !string.IsNullOrEmpty(c.AssignedTo))
                    .GroupBy(c => c.AssignedTo!)
                    .ToDictionary(g => g.Key, g => g.Count())
            };
        }

        #endregion

        #region Reports

        /// <summary>
        /// Generate coordination report
        /// </summary>
        public async Task<CoordinationReport> GenerateReportAsync(
            CoordinationReportRequest request,
            CancellationToken ct = default)
        {
            var statistics = GetStatistics(request.TestId);
            var clashes = request.TestId != null
                ? _clashes.Values.Where(c => c.TestId == request.TestId).ToList()
                : _clashes.Values.ToList();

            var aiInsights = await _clashAI.GenerateInsightsAsync(clashes, ct);

            var report = new CoordinationReport
            {
                Id = Guid.NewGuid().ToString("N")[..12],
                Title = request.Title ?? "Coordination Report",
                GeneratedAt = DateTime.UtcNow,
                GeneratedBy = request.GeneratedBy,
                Period = request.Period,

                Summary = new ReportSummary
                {
                    TotalClashes = statistics.TotalCount,
                    OpenClashes = statistics.NewCount + statistics.ActiveCount,
                    ResolvedClashes = statistics.ResolvedCount,
                    CriticalClashes = statistics.BySeverity.GetValueOrDefault(ClashSeverity.Critical, 0),
                    ResolutionRate = statistics.TotalCount > 0
                        ? (double)statistics.ResolvedCount / statistics.TotalCount * 100
                        : 100
                },

                Statistics = statistics,
                AIInsights = aiInsights,

                ClashesBySeverity = clashes
                    .GroupBy(c => c.Severity)
                    .ToDictionary(g => g.Key, g => g.ToList()),

                TopClashPairs = GetTopClashPairs(clashes, 10),

                RecommendedActions = aiInsights.Recommendations
            };

            ReportReady?.Invoke(this, new CoordinationReportReadyEventArgs(report));

            return report;
        }

        private List<ClashPair> GetTopClashPairs(List<ClashResult> clashes, int count)
        {
            return clashes
                .GroupBy(c => $"{c.Element1.Category}|{c.Element2.Category}")
                .Select(g => new ClashPair
                {
                    Category1 = g.First().Element1.Category,
                    Category2 = g.First().Element2.Category,
                    ClashCount = g.Count(),
                    CriticalCount = g.Count(c => c.Severity == ClashSeverity.Critical)
                })
                .OrderByDescending(p => p.ClashCount)
                .Take(count)
                .ToList();
        }

        #endregion

        #region Real-time Monitoring

        /// <summary>
        /// Start real-time clash monitoring
        /// </summary>
        public void StartMonitoring(TimeSpan interval)
        {
            _monitorCts?.Cancel();
            _monitorCts = new CancellationTokenSource();

            _ = MonitorClashesAsync(interval, _monitorCts.Token);
        }

        /// <summary>
        /// Stop monitoring
        /// </summary>
        public void StopMonitoring()
        {
            _monitorCts?.Cancel();
        }

        private async Task MonitorClashesAsync(TimeSpan interval, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(interval, ct);

                    // Re-run all tests
                    foreach (var test in _clashTests.Values.Where(t => t.Settings.AutoRefresh))
                    {
                        await RunClashTestAsync(test.Id, ct);
                    }

                    // AI prediction
                    var predictions = await _clashAI.PredictUpcomingClashesAsync(_models.Values.ToList(), ct);
                    foreach (var prediction in predictions)
                    {
                        ClashPredicted?.Invoke(this, new ClashPredictedEventArgs(prediction));
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        #endregion

        #region Viewpoint & Markup

        /// <summary>
        /// Create viewpoint for clash
        /// </summary>
        public ClashViewpoint CreateViewpoint(
            string clashId,
            CameraPosition camera,
            string createdBy)
        {
            if (!_clashes.TryGetValue(clashId, out var clash))
                throw new ClashNotFoundException(clashId);

            var viewpoint = new ClashViewpoint
            {
                Id = Guid.NewGuid().ToString("N")[..12],
                ClashId = clashId,
                Camera = camera,
                HighlightedElements = new[] { clash.Element1.Id, clash.Element2.Id }.ToList(),
                CreatedBy = createdBy,
                CreatedAt = DateTime.UtcNow
            };

            clash.Viewpoint = viewpoint;

            return viewpoint;
        }

        /// <summary>
        /// Add markup to clash viewpoint
        /// </summary>
        public void AddMarkup(string clashId, ClashMarkup markup)
        {
            if (!_clashes.TryGetValue(clashId, out var clash))
                throw new ClashNotFoundException(clashId);

            clash.Viewpoint ??= new ClashViewpoint { Id = Guid.NewGuid().ToString("N")[..12] };
            clash.Viewpoint.Markups ??= new List<ClashMarkup>();
            clash.Viewpoint.Markups.Add(markup);
        }

        #endregion

        public async ValueTask DisposeAsync()
        {
            _monitorCts?.Cancel();
            _monitorCts?.Dispose();
            await Task.CompletedTask;
        }
    }

    #region Model Classes

    public class CoordinationModel
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Discipline { get; set; } = "";
        public string FilePath { get; set; } = "";
        public ModelFormat Format { get; set; }
        public DateTime RegisteredAt { get; set; }
        public DateTime? LastUpdated { get; set; }
        public ModelStatus Status { get; set; }
        public int ElementCount { get; set; }
        public List<ModelElement>? Elements { get; set; }
        public SpatialIndex? SpatialIndex { get; set; }
    }

    public enum ModelFormat { Revit, IFC, NWC, NWD, DWG, FBX }
    public enum ModelStatus { Registered, Loading, Updated, Error }

    public class ModelElement
    {
        public string Id { get; set; } = "";
        public string UniqueId { get; set; } = "";
        public string Name { get; set; } = "";
        public string Category { get; set; } = "";
        public string? Level { get; set; }
        public string? ModelId { get; set; }
        public BoundingBox3D BoundingBox { get; set; } = new();
        public List<Point3D>? Geometry { get; set; }
    }

    public class BoundingBox3D
    {
        public Point3D Min { get; set; } = new();
        public Point3D Max { get; set; } = new();

        public bool Intersects(BoundingBox3D other, double tolerance = 0)
        {
            return Min.X - tolerance <= other.Max.X && Max.X + tolerance >= other.Min.X &&
                   Min.Y - tolerance <= other.Max.Y && Max.Y + tolerance >= other.Min.Y &&
                   Min.Z - tolerance <= other.Max.Z && Max.Z + tolerance >= other.Min.Z;
        }

        public BoundingBox3D Expand(double amount)
        {
            return new BoundingBox3D
            {
                Min = new Point3D { X = Min.X - amount, Y = Min.Y - amount, Z = Min.Z - amount },
                Max = new Point3D { X = Max.X + amount, Y = Max.Y + amount, Z = Max.Z + amount }
            };
        }

        public bool Contains(Point3D point)
        {
            return point.X >= Min.X && point.X <= Max.X &&
                   point.Y >= Min.Y && point.Y <= Max.Y &&
                   point.Z >= Min.Z && point.Z <= Max.Z;
        }
    }

    #endregion

    #region Clash Test Classes

    public class ClashTest
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public ClashTestType Type { get; set; }
        public SelectionSet SelectionA { get; set; } = new();
        public SelectionSet SelectionB { get; set; } = new();
        public string? ModelAId { get; set; }
        public string? ModelBId { get; set; }
        public ClashTestSettings Settings { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime? LastRun { get; set; }
        public ClashTestStatus Status { get; set; }
        public int LastClashCount { get; set; }
        public int NewClashCount { get; set; }
    }

    public enum ClashTestType { Hard, Clearance, Duplicate }
    public enum ClashTestStatus { Ready, Running, Complete, Error }

    public class SelectionSet
    {
        public string Name { get; set; } = "";
        public string? ModelId { get; set; }
        public List<string>? Categories { get; set; }
        public List<string>? ExcludeCategories { get; set; }
        public List<string>? Levels { get; set; }
        public List<string>? ElementIds { get; set; }
        public string? SearchQuery { get; set; }
    }

    public class ClashTestSettings
    {
        public double Tolerance { get; set; } = 10; // mm
        public double ClearanceDistance { get; set; } = 50; // mm for clearance tests
        public bool IgnoreSameModel { get; set; } = true;
        public bool IgnoreSameCategory { get; set; }
        public ClashGroupingMethod GroupingMethod { get; set; } = ClashGroupingMethod.ByElement;
        public bool AutoRefresh { get; set; }
        public List<string>? IgnoreElementIds { get; set; }
    }

    public enum ClashGroupingMethod { None, ByElement, ByCategory, ByLocation, ByLevel }

    public class ClashTestResult
    {
        public string TestId { get; set; } = "";
        public List<ClashResult> Clashes { get; set; } = new();
        public int TotalCount { get; set; }
        public int NewCount { get; set; }
        public int ActiveCount { get; set; }
        public int ResolvedCount { get; set; }
        public ClashAIAnalysis? AIAnalysis { get; set; }
        public TimeSpan ExecutionTime { get; set; }
        public string? Error { get; set; }
    }

    #endregion

    #region Clash Result Classes

    public class ClashResult
    {
        public string Id { get; set; } = "";
        public string? TestId { get; set; }
        public string? TestName { get; set; }
        public string? GroupId { get; set; }
        public ClashElement Element1 { get; set; } = new();
        public ClashElement Element2 { get; set; } = new();
        public Point3D ClashPoint { get; set; } = new();
        public double Distance { get; set; }
        public double? Volume { get; set; }
        public string? Level { get; set; }
        public ClashTestType Type { get; set; }
        public ClashSeverity Severity { get; set; }
        public ClashStatus Status { get; set; }
        public DateTime DetectedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public string? ResolvedBy { get; set; }
        public string? AssignedTo { get; set; }
        public string? AssignedBy { get; set; }
        public DateTime? AssignedAt { get; set; }
        public ClashViewpoint? Viewpoint { get; set; }
        public List<ClashComment>? Comments { get; set; }
        public ResolutionInfo? Resolution { get; set; }
    }

    public class ClashElement
    {
        public string Id { get; set; } = "";
        public string UniqueId { get; set; } = "";
        public string Name { get; set; } = "";
        public string Category { get; set; } = "";
        public string? ModelId { get; set; }
        public string? Level { get; set; }
    }

    public enum ClashSeverity { Info, Minor, Major, Critical }
    public enum ClashStatus { New, Active, Resolved, Approved, Ignored }

    public class ClashGroup
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public List<string> ClashIds { get; set; } = new();
        public int ClashCount { get; set; }
        public ClashSeverity Severity { get; set; }
    }

    public class ClashComment
    {
        public string Id { get; set; } = "";
        public string Author { get; set; } = "";
        public string Content { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public string? StatusChange { get; set; }
    }

    public class ClashViewpoint
    {
        public string Id { get; set; } = "";
        public string? ClashId { get; set; }
        public CameraPosition Camera { get; set; } = new();
        public List<string>? HighlightedElements { get; set; }
        public List<string>? HiddenElements { get; set; }
        public string? SectionBox { get; set; }
        public byte[]? Thumbnail { get; set; }
        public List<ClashMarkup>? Markups { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? CreatedAt { get; set; }
    }

    public class CameraPosition
    {
        public Point3D Eye { get; set; } = new();
        public Point3D Target { get; set; } = new();
        public Point3D Up { get; set; } = new() { Z = 1 };
        public double FieldOfView { get; set; } = 45;
        public bool IsPerspective { get; set; } = true;
    }

    public class ClashMarkup
    {
        public string Id { get; set; } = "";
        public MarkupType Type { get; set; }
        public List<Point3D> Points { get; set; } = new();
        public string? Color { get; set; }
        public string? Text { get; set; }
        public string CreatedBy { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }

    public enum MarkupType { Arrow, Cloud, Rectangle, Circle, Line, Text, Freehand }

    public class ResolutionInfo
    {
        public ResolutionMethod Method { get; set; }
        public string? Description { get; set; }
        public List<string>? ModifiedElements { get; set; }
        public string ResolvedBy { get; set; } = "";
        public DateTime ResolvedAt { get; set; }
    }

    public enum ResolutionMethod { Modified, Ignored, Approved, Deferred, Deleted }

    #endregion

    #region Query Classes

    public class ClashQuery
    {
        public string? TestId { get; set; }
        public List<ClashStatus>? Statuses { get; set; }
        public List<ClashSeverity>? Severities { get; set; }
        public string? AssignedTo { get; set; }
        public string? ModelId { get; set; }
        public string? Category { get; set; }
        public string? GroupId { get; set; }
        public ClashSortField SortBy { get; set; } = ClashSortField.Detected;
        public bool SortDescending { get; set; } = true;
        public int Skip { get; set; }
        public int Take { get; set; } = 50;
    }

    public enum ClashSortField { Detected, Severity, Distance, Status }

    public class ClashQueryResult
    {
        public List<ClashResult> Clashes { get; set; } = new();
        public int TotalCount { get; set; }
        public int Skip { get; set; }
        public int Take { get; set; }
    }

    #endregion

    #region Statistics & Reports

    public class ClashStatistics
    {
        public int TotalCount { get; set; }
        public int NewCount { get; set; }
        public int ActiveCount { get; set; }
        public int ResolvedCount { get; set; }
        public int ApprovedCount { get; set; }
        public int IgnoredCount { get; set; }
        public Dictionary<ClashSeverity, int> BySeverity { get; set; } = new();
        public Dictionary<string, int> ByCategory { get; set; } = new();
        public Dictionary<string, int> ByTest { get; set; } = new();
        public Dictionary<string, int> ByAssignee { get; set; } = new();
    }

    public class CoordinationReportRequest
    {
        public string? Title { get; set; }
        public string? TestId { get; set; }
        public string GeneratedBy { get; set; } = "";
        public DateRange? Period { get; set; }
        public bool IncludeResolved { get; set; } = true;
    }

    public class DateRange
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
    }

    public class CoordinationReport
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public DateTime GeneratedAt { get; set; }
        public string GeneratedBy { get; set; } = "";
        public DateRange? Period { get; set; }
        public ReportSummary Summary { get; set; } = new();
        public ClashStatistics Statistics { get; set; } = new();
        public ClashAIInsights AIInsights { get; set; } = new();
        public Dictionary<ClashSeverity, List<ClashResult>> ClashesBySeverity { get; set; } = new();
        public List<ClashPair> TopClashPairs { get; set; } = new();
        public List<string> RecommendedActions { get; set; } = new();
    }

    public class ReportSummary
    {
        public int TotalClashes { get; set; }
        public int OpenClashes { get; set; }
        public int ResolvedClashes { get; set; }
        public int CriticalClashes { get; set; }
        public double ResolutionRate { get; set; }
    }

    public class ClashPair
    {
        public string Category1 { get; set; } = "";
        public string Category2 { get; set; } = "";
        public int ClashCount { get; set; }
        public int CriticalCount { get; set; }
    }

    #endregion

    #region AI Classes

    public class ClashDetectionAI
    {
        private readonly ModelCoordinationEngine _engine;

        public ClashDetectionAI(ModelCoordinationEngine engine)
        {
            _engine = engine;
        }

        public Task<ClashAIAnalysis> AnalyzeClashesAsync(List<ClashResult> clashes, CancellationToken ct)
        {
            var analysis = new ClashAIAnalysis
            {
                TotalClashes = clashes.Count,
                CriticalClashes = clashes.Count(c => c.Severity == ClashSeverity.Critical),
                PotentialDuplicates = FindDuplicates(clashes),
                CommonPatterns = IdentifyPatterns(clashes),
                RiskAreas = IdentifyRiskAreas(clashes),
                Recommendations = GenerateRecommendations(clashes)
            };

            return Task.FromResult(analysis);
        }

        public Task<List<ResolutionSuggestion>> SuggestResolutionsAsync(ClashResult clash, CancellationToken ct)
        {
            var suggestions = new List<ResolutionSuggestion>();

            // Analyze clash type and suggest resolutions
            if (clash.Element1.Category.Contains("Pipe") || clash.Element2.Category.Contains("Pipe"))
            {
                suggestions.Add(new ResolutionSuggestion
                {
                    Method = "Reroute piping",
                    Description = "Adjust pipe routing to avoid the clash point",
                    Confidence = 0.8,
                    AffectedElement = clash.Element1.Category.Contains("Pipe") ? clash.Element1.Id : clash.Element2.Id
                });
            }

            if (clash.Element1.Category.Contains("Duct") || clash.Element2.Category.Contains("Duct"))
            {
                suggestions.Add(new ResolutionSuggestion
                {
                    Method = "Adjust duct elevation",
                    Description = "Raise or lower duct to clear the obstruction",
                    Confidence = 0.75,
                    AffectedElement = clash.Element1.Category.Contains("Duct") ? clash.Element1.Id : clash.Element2.Id
                });
            }

            suggestions.Add(new ResolutionSuggestion
            {
                Method = "Coordinate with discipline lead",
                Description = "Discuss with the responsible discipline to determine the best resolution",
                Confidence = 0.9,
                AffectedElement = null
            });

            return Task.FromResult(suggestions);
        }

        public Task<List<ClashPrediction>> PredictUpcomingClashesAsync(List<CoordinationModel> models, CancellationToken ct)
        {
            var predictions = new List<ClashPrediction>();

            // Analyze model update patterns and predict potential clashes
            foreach (var model in models.Where(m => m.LastUpdated.HasValue))
            {
                if (DateTime.UtcNow - model.LastUpdated < TimeSpan.FromHours(24))
                {
                    predictions.Add(new ClashPrediction
                    {
                        ModelId = model.Id,
                        Probability = 0.7,
                        Reason = $"Model {model.Name} was recently updated",
                        SuggestedAction = "Run coordination check after updates"
                    });
                }
            }

            return Task.FromResult(predictions);
        }

        public Task<ClashAIInsights> GenerateInsightsAsync(List<ClashResult> clashes, CancellationToken ct)
        {
            var insights = new ClashAIInsights
            {
                GeneratedAt = DateTime.UtcNow,
                KeyFindings = new List<string>
                {
                    $"Total of {clashes.Count} clashes detected",
                    $"{clashes.Count(c => c.Severity == ClashSeverity.Critical)} critical clashes require immediate attention",
                    $"Most common clash type: {clashes.GroupBy(c => $"{c.Element1.Category} vs {c.Element2.Category}").OrderByDescending(g => g.Count()).FirstOrDefault()?.Key ?? "N/A"}"
                },
                Recommendations = GenerateRecommendations(clashes),
                Trends = AnalyzeTrends(clashes)
            };

            return Task.FromResult(insights);
        }

        private List<ClashDuplicate> FindDuplicates(List<ClashResult> clashes)
        {
            var duplicates = new List<ClashDuplicate>();

            var groups = clashes.GroupBy(c => $"{c.Element1.Id}_{c.Element2.Id}");
            foreach (var group in groups.Where(g => g.Count() > 1))
            {
                duplicates.Add(new ClashDuplicate
                {
                    ClashIds = group.Select(c => c.Id).ToList(),
                    Reason = "Same element pair"
                });
            }

            return duplicates;
        }

        private List<ClashPattern> IdentifyPatterns(List<ClashResult> clashes)
        {
            var patterns = new List<ClashPattern>();

            var categoryPairs = clashes
                .GroupBy(c => $"{c.Element1.Category}|{c.Element2.Category}")
                .Where(g => g.Count() >= 5)
                .OrderByDescending(g => g.Count());

            foreach (var pair in categoryPairs.Take(5))
            {
                var parts = pair.Key.Split('|');
                patterns.Add(new ClashPattern
                {
                    Category1 = parts[0],
                    Category2 = parts[1],
                    Count = pair.Count(),
                    Severity = pair.Max(c => c.Severity),
                    SuggestedAction = $"Review coordination between {parts[0]} and {parts[1]}"
                });
            }

            return patterns;
        }

        private List<RiskArea> IdentifyRiskAreas(List<ClashResult> clashes)
        {
            var areas = new List<RiskArea>();

            var criticalClashes = clashes.Where(c => c.Severity == ClashSeverity.Critical).ToList();
            if (criticalClashes.Count > 10)
            {
                areas.Add(new RiskArea
                {
                    Name = "Critical Clash Volume",
                    Level = RiskLevel.High,
                    Description = $"{criticalClashes.Count} critical clashes need immediate resolution"
                });
            }

            return areas;
        }

        private List<string> GenerateRecommendations(List<ClashResult> clashes)
        {
            var recommendations = new List<string>();

            var criticalCount = clashes.Count(c => c.Severity == ClashSeverity.Critical);
            if (criticalCount > 0)
            {
                recommendations.Add($"Address {criticalCount} critical clashes immediately");
            }

            var unassigned = clashes.Count(c => string.IsNullOrEmpty(c.AssignedTo) && c.Status == ClashStatus.New);
            if (unassigned > 10)
            {
                recommendations.Add($"Assign {unassigned} unassigned clashes to team members");
            }

            recommendations.Add("Schedule weekly coordination meetings to review progress");

            return recommendations;
        }

        private List<ClashTrend> AnalyzeTrends(List<ClashResult> clashes)
        {
            var trends = new List<ClashTrend>();

            var recentClashes = clashes.Count(c => c.DetectedAt >= DateTime.UtcNow.AddDays(-7));
            var previousClashes = clashes.Count(c => c.DetectedAt >= DateTime.UtcNow.AddDays(-14) && c.DetectedAt < DateTime.UtcNow.AddDays(-7));

            if (recentClashes > previousClashes * 1.2)
            {
                trends.Add(new ClashTrend
                {
                    Type = "Increasing Clashes",
                    Description = "Clash count is increasing compared to last week",
                    Direction = TrendDirection.Worsening
                });
            }
            else if (recentClashes < previousClashes * 0.8)
            {
                trends.Add(new ClashTrend
                {
                    Type = "Decreasing Clashes",
                    Description = "Clash count is decreasing - good progress",
                    Direction = TrendDirection.Improving
                });
            }

            return trends;
        }
    }

    public class ClashAIAnalysis
    {
        public int TotalClashes { get; set; }
        public int CriticalClashes { get; set; }
        public List<ClashDuplicate> PotentialDuplicates { get; set; } = new();
        public List<ClashPattern> CommonPatterns { get; set; } = new();
        public List<RiskArea> RiskAreas { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
    }

    public class ClashDuplicate
    {
        public List<string> ClashIds { get; set; } = new();
        public string Reason { get; set; } = "";
    }

    public class ClashPattern
    {
        public string Category1 { get; set; } = "";
        public string Category2 { get; set; } = "";
        public int Count { get; set; }
        public ClashSeverity Severity { get; set; }
        public string SuggestedAction { get; set; } = "";
    }

    public class RiskArea
    {
        public string Name { get; set; } = "";
        public RiskLevel Level { get; set; }
        public string Description { get; set; } = "";
    }

    public enum RiskLevel { Low, Medium, High, Critical }

    public class ResolutionSuggestion
    {
        public string Method { get; set; } = "";
        public string Description { get; set; } = "";
        public double Confidence { get; set; }
        public string? AffectedElement { get; set; }
        public List<string>? Steps { get; set; }
    }

    public class ClashPrediction
    {
        public string? ModelId { get; set; }
        public double Probability { get; set; }
        public string Reason { get; set; } = "";
        public string SuggestedAction { get; set; } = "";
    }

    public class ClashAIInsights
    {
        public DateTime GeneratedAt { get; set; }
        public List<string> KeyFindings { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
        public List<ClashTrend> Trends { get; set; } = new();
    }

    public class ClashTrend
    {
        public string Type { get; set; } = "";
        public string Description { get; set; } = "";
        public TrendDirection Direction { get; set; }
    }

    public enum TrendDirection { Improving, Worsening, Stable }

    #endregion

    #region Supporting Classes

    public class SpatialIndex
    {
        private readonly List<ModelElement> _elements;
        private readonly BoundingBox3D _bounds;

        public SpatialIndex(List<ModelElement> elements)
        {
            _elements = elements;

            if (elements.Any())
            {
                _bounds = new BoundingBox3D
                {
                    Min = new Point3D
                    {
                        X = elements.Min(e => e.BoundingBox.Min.X),
                        Y = elements.Min(e => e.BoundingBox.Min.Y),
                        Z = elements.Min(e => e.BoundingBox.Min.Z)
                    },
                    Max = new Point3D
                    {
                        X = elements.Max(e => e.BoundingBox.Max.X),
                        Y = elements.Max(e => e.BoundingBox.Max.Y),
                        Z = elements.Max(e => e.BoundingBox.Max.Z)
                    }
                };
            }
            else
            {
                _bounds = new BoundingBox3D();
            }
        }

        public IEnumerable<ModelElement> Query(BoundingBox3D box)
        {
            return _elements.Where(e => e.BoundingBox.Intersects(box));
        }
    }

    public class IntersectionResult
    {
        public Point3D Point { get; set; } = new();
        public double Distance { get; set; }
        public double Volume { get; set; }
    }

    #endregion

    #region Event Args

    public class ClashDetectedEventArgs : EventArgs
    {
        public ClashResult Clash { get; }
        public ClashDetectedEventArgs(ClashResult clash) => Clash = clash;
    }

    public class ClashResolvedEventArgs : EventArgs
    {
        public ClashResult Clash { get; }
        public string ResolvedBy { get; }
        public ClashResolvedEventArgs(ClashResult clash, string resolvedBy)
        {
            Clash = clash;
            ResolvedBy = resolvedBy;
        }
    }

    public class ClashPredictedEventArgs : EventArgs
    {
        public ClashPrediction Prediction { get; }
        public ClashPredictedEventArgs(ClashPrediction prediction) => Prediction = prediction;
    }

    public class CoordinationReportReadyEventArgs : EventArgs
    {
        public CoordinationReport Report { get; }
        public CoordinationReportReadyEventArgs(CoordinationReport report) => Report = report;
    }

    #endregion

    #region Exceptions

    public class ModelNotFoundException : Exception
    {
        public string ModelId { get; }
        public ModelNotFoundException(string modelId) : base($"Model not found: {modelId}")
            => ModelId = modelId;
    }

    public class ClashTestNotFoundException : Exception
    {
        public string TestId { get; }
        public ClashTestNotFoundException(string testId) : base($"Clash test not found: {testId}")
            => TestId = testId;
    }

    public class ClashNotFoundException : Exception
    {
        public string ClashId { get; }
        public ClashNotFoundException(string clashId) : base($"Clash not found: {clashId}")
            => ClashId = clashId;
    }

    #endregion
}
