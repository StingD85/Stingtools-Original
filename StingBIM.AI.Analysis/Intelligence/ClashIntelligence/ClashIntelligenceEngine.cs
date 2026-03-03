// ===================================================================
// StingBIM Clash Intelligence Engine - Advanced Clash Detection & Resolution
// Clash categorization, priority scoring, resolution workflows, trend analysis
// ===================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.ClashIntelligence
{
    /// <summary>
    /// Comprehensive clash detection intelligence with categorization,
    /// priority scoring, automated resolution workflows, and trend analysis
    /// </summary>
    public sealed class ClashIntelligenceEngine
    {
        private static readonly Lazy<ClashIntelligenceEngine> _instance =
            new Lazy<ClashIntelligenceEngine>(() => new ClashIntelligenceEngine());
        public static ClashIntelligenceEngine Instance => _instance.Value;

        private readonly Dictionary<string, ClashProject> _projects;
        private readonly Dictionary<string, ClashTest> _clashTests;
        private readonly Dictionary<string, ClashDetectionResult> _clashResults;
        private readonly Dictionary<string, ClashResolution> _resolutions;
        private readonly List<ClashRule> _clashRules;
        private readonly List<ClashCategory> _categories;
        private readonly object _lockObject = new object();

        public event EventHandler<ClashDetectedEventArgs> ClashDetected;
        public event EventHandler<ClashResolvedEventArgs> ClashResolved;
        public event EventHandler<ClashTrendAlertEventArgs> TrendAlert;

        private ClashIntelligenceEngine()
        {
            _projects = new Dictionary<string, ClashProject>();
            _clashTests = new Dictionary<string, ClashTest>();
            _clashResults = new Dictionary<string, ClashDetectionResult>();
            _resolutions = new Dictionary<string, ClashResolution>();
            _clashRules = new List<ClashRule>();
            _categories = new List<ClashCategory>();
            InitializeClashRules();
            InitializeCategories();
        }

        #region Project & Test Management

        public ClashProject CreateProject(string projectId, string projectName)
        {
            var project = new ClashProject
            {
                Id = Guid.NewGuid().ToString(),
                ProjectId = projectId,
                ProjectName = projectName,
                CreatedDate = DateTime.Now,
                Status = ProjectStatus.Active,
                Models = new List<ClashModel>(),
                ClashSets = new List<ClashSet>(),
                Workflows = new List<ClashWorkflow>()
            };

            lock (_lockObject)
            {
                _projects[project.Id] = project;
            }

            return project;
        }

        public ClashModel AddModel(string clashProjectId, string modelPath, ModelDiscipline discipline)
        {
            lock (_lockObject)
            {
                if (_projects.TryGetValue(clashProjectId, out var project))
                {
                    var model = new ClashModel
                    {
                        Id = Guid.NewGuid().ToString(),
                        FilePath = modelPath,
                        FileName = System.IO.Path.GetFileName(modelPath),
                        Discipline = discipline,
                        LastUpdated = DateTime.Now,
                        ElementCount = 0,
                        Version = 1
                    };
                    project.Models.Add(model);
                    return model;
                }
            }
            return null;
        }

        public ClashTest CreateClashTest(string clashProjectId, ClashTestDefinition definition)
        {
            var test = new ClashTest
            {
                Id = Guid.NewGuid().ToString(),
                ClashProjectId = clashProjectId,
                Name = definition.Name,
                Description = definition.Description,
                SelectionA = definition.SelectionA,
                SelectionB = definition.SelectionB,
                ClashType = definition.ClashType,
                Tolerance = definition.Tolerance,
                Status = ClashTestStatus.Ready,
                CreatedDate = DateTime.Now,
                Rules = definition.Rules ?? new List<string>()
            };

            lock (_lockObject)
            {
                _clashTests[test.Id] = test;
            }

            return test;
        }

        #endregion

        #region Clash Detection

        public async Task<ClashDetectionResult> RunClashDetectionAsync(string clashTestId)
        {
            return await Task.Run(() =>
            {
                lock (_lockObject)
                {
                    if (!_clashTests.TryGetValue(clashTestId, out var test))
                        return null;

                    test.Status = ClashTestStatus.Running;
                    test.LastRunDate = DateTime.Now;

                    var result = new ClashDetectionResult
                    {
                        Id = Guid.NewGuid().ToString(),
                        ClashTestId = clashTestId,
                        RunDate = DateTime.Now,
                        Clashes = new List<Clash>(),
                        Statistics = new ClashStatistics()
                    };

                    // Simulate clash detection (in real implementation, this would use Revit/Navisworks API)
                    var clashes = DetectClashes(test);

                    foreach (var clash in clashes)
                    {
                        // Categorize clash
                        clash.Category = CategorizeClash(clash);

                        // Calculate priority score
                        clash.PriorityScore = CalculatePriorityScore(clash);

                        // Determine severity
                        clash.Severity = DetermineSeverity(clash);

                        // Auto-assign responsibility
                        clash.ResponsibleDiscipline = AssignResponsibility(clash);

                        // Suggest resolution
                        clash.SuggestedResolution = SuggestResolution(clash);

                        result.Clashes.Add(clash);
                    }

                    // Calculate statistics
                    result.Statistics = CalculateStatistics(result.Clashes);

                    // Store result
                    _clashResults[result.Id] = result;

                    test.Status = ClashTestStatus.Completed;
                    test.LastResultId = result.Id;

                    // Raise events for critical clashes
                    var criticalClashes = result.Clashes.Where(c => c.Severity == ClashSeverity.Critical).ToList();
                    if (criticalClashes.Any())
                    {
                        OnClashDetected(new ClashDetectedEventArgs
                        {
                            ClashTestId = clashTestId,
                            TotalClashes = result.Clashes.Count,
                            CriticalClashes = criticalClashes.Count,
                            NewClashes = result.Clashes.Count(c => c.Status == ClashStatus.New)
                        });
                    }

                    return result;
                }
            });
        }

        private List<Clash> DetectClashes(ClashTest test)
        {
            // Simulated clash detection - in real implementation would use geometry intersection
            var clashes = new List<Clash>();

            // Generate sample clashes based on test type
            var clashTypes = new[]
            {
                ("Duct", "Beam", ClashType.Hard, 150),
                ("Pipe", "Duct", ClashType.Hard, 75),
                ("Cable Tray", "Duct", ClashType.Hard, 45),
                ("Sprinkler", "Ceiling", ClashType.Clearance, 30),
                ("Duct", "Column", ClashType.Hard, 25),
                ("Pipe", "Wall", ClashType.Hard, 20),
                ("Conduit", "Beam", ClashType.Hard, 15),
                ("Equipment", "Structure", ClashType.Hard, 10)
            };

            var random = new Random();
            foreach (var (elementA, elementB, type, baseCount) in clashTypes)
            {
                var count = random.Next(baseCount / 2, baseCount);
                for (int i = 0; i < count; i++)
                {
                    clashes.Add(new Clash
                    {
                        Id = Guid.NewGuid().ToString(),
                        ClashTestId = test.Id,
                        ElementAId = $"{elementA}_{Guid.NewGuid():N}".Substring(0, 20),
                        ElementAName = $"{elementA} {i + 1}",
                        ElementADiscipline = GetDiscipline(elementA),
                        ElementBId = $"{elementB}_{Guid.NewGuid():N}".Substring(0, 20),
                        ElementBName = $"{elementB} {i + 1}",
                        ElementBDiscipline = GetDiscipline(elementB),
                        ClashType = type,
                        Distance = type == ClashType.Hard ? -random.Next(10, 200) : random.Next(-50, 0),
                        Location = new ClashLocation
                        {
                            X = random.NextDouble() * 100,
                            Y = random.NextDouble() * 100,
                            Z = random.NextDouble() * 20 + 3,
                            Level = $"Level {random.Next(1, 5)}",
                            GridIntersection = $"{(char)('A' + random.Next(0, 10))}-{random.Next(1, 15)}"
                        },
                        Status = ClashStatus.New,
                        DetectedDate = DateTime.Now
                    });
                }
            }

            return clashes;
        }

        private ModelDiscipline GetDiscipline(string elementType)
        {
            return elementType switch
            {
                "Duct" => ModelDiscipline.Mechanical,
                "Pipe" => ModelDiscipline.Plumbing,
                "Sprinkler" => ModelDiscipline.FireProtection,
                "Cable Tray" or "Conduit" => ModelDiscipline.Electrical,
                "Beam" or "Column" => ModelDiscipline.Structural,
                "Wall" or "Ceiling" => ModelDiscipline.Architectural,
                "Equipment" => ModelDiscipline.Mechanical,
                _ => ModelDiscipline.Coordination
            };
        }

        #endregion

        #region Clash Categorization & Priority

        private ClashCategory CategorizeClash(Clash clash)
        {
            // Find matching category based on rules
            foreach (var category in _categories)
            {
                if (category.MatchesClash(clash))
                    return category;
            }

            return _categories.FirstOrDefault(c => c.Code == "MISC") ?? new ClashCategory { Code = "MISC", Name = "Miscellaneous" };
        }

        private int CalculatePriorityScore(Clash clash)
        {
            var score = 50; // Base score

            // Severity factor
            score += clash.ClashType == ClashType.Hard ? 20 : 10;

            // Distance factor (deeper penetration = higher priority)
            if (clash.Distance < -100) score += 15;
            else if (clash.Distance < -50) score += 10;
            else if (clash.Distance < -25) score += 5;

            // Discipline factor (MEP vs Structure typically higher priority)
            if ((clash.ElementADiscipline == ModelDiscipline.Structural ||
                 clash.ElementBDiscipline == ModelDiscipline.Structural) &&
                (clash.ElementADiscipline != ModelDiscipline.Structural ||
                 clash.ElementBDiscipline != ModelDiscipline.Structural))
            {
                score += 15;
            }

            // Location factor (main corridors, lobbies higher priority)
            if (clash.Location?.GridIntersection?.Contains("A") == true ||
                clash.Location?.GridIntersection?.Contains("1") == true)
            {
                score += 5;
            }

            // Category-specific adjustments
            if (clash.Category?.PriorityModifier != null)
            {
                score += clash.Category.PriorityModifier.Value;
            }

            return Math.Min(100, Math.Max(0, score));
        }

        private ClashSeverity DetermineSeverity(Clash clash)
        {
            if (clash.PriorityScore >= 80) return ClashSeverity.Critical;
            if (clash.PriorityScore >= 60) return ClashSeverity.Major;
            if (clash.PriorityScore >= 40) return ClashSeverity.Moderate;
            return ClashSeverity.Minor;
        }

        private ModelDiscipline AssignResponsibility(Clash clash)
        {
            // General rules for clash responsibility
            var disciplines = new[] { clash.ElementADiscipline, clash.ElementBDiscipline };

            // Structure generally doesn't move
            if (disciplines.Contains(ModelDiscipline.Structural))
            {
                return disciplines.First(d => d != ModelDiscipline.Structural);
            }

            // Architecture generally takes precedence over MEP
            if (disciplines.Contains(ModelDiscipline.Architectural))
            {
                return disciplines.First(d => d != ModelDiscipline.Architectural);
            }

            // Larger systems generally have priority (Mechanical > Electrical > Plumbing)
            var priority = new[]
            {
                ModelDiscipline.Mechanical,
                ModelDiscipline.Electrical,
                ModelDiscipline.Plumbing,
                ModelDiscipline.FireProtection
            };

            foreach (var p in priority)
            {
                if (disciplines.Contains(p))
                {
                    return disciplines.First(d => d != p);
                }
            }

            return clash.ElementADiscipline;
        }

        private ClashResolutionSuggestion SuggestResolution(Clash clash)
        {
            var suggestion = new ClashResolutionSuggestion
            {
                ClashId = clash.Id,
                SuggestedActions = new List<string>(),
                EstimatedEffort = ResolutionEffort.Medium,
                ConfidenceScore = 0.7
            };

            // MEP vs Structure
            if (clash.ElementBDiscipline == ModelDiscipline.Structural ||
                clash.ElementADiscipline == ModelDiscipline.Structural)
            {
                suggestion.PrimaryAction = ResolutionAction.RouteAround;
                suggestion.SuggestedActions.Add("Route MEP element around structural member");
                suggestion.SuggestedActions.Add("Consider vertical offset if horizontal routing not possible");
                suggestion.SuggestedActions.Add("Verify clearance requirements for maintenance access");
            }
            // Duct vs Pipe
            else if ((clash.ElementAName.Contains("Duct") || clash.ElementBName.Contains("Duct")) &&
                     (clash.ElementAName.Contains("Pipe") || clash.ElementBName.Contains("Pipe")))
            {
                suggestion.PrimaryAction = ResolutionAction.AdjustElevation;
                suggestion.SuggestedActions.Add("Adjust duct elevation (typically duct goes higher)");
                suggestion.SuggestedActions.Add("Consider pipe routing adjustment if duct is constrained");
                suggestion.SuggestedActions.Add("Verify adequate slope for drain piping");
            }
            // Sprinkler vs Ceiling
            else if (clash.ElementAName.Contains("Sprinkler") || clash.ElementBName.Contains("Sprinkler"))
            {
                suggestion.PrimaryAction = ResolutionAction.AdjustElevation;
                suggestion.SuggestedActions.Add("Adjust sprinkler head height to meet ceiling");
                suggestion.SuggestedActions.Add("Verify coverage and deflector distance requirements");
                suggestion.EstimatedEffort = ResolutionEffort.Low;
            }
            // Default
            else
            {
                suggestion.PrimaryAction = ResolutionAction.CoordinationRequired;
                suggestion.SuggestedActions.Add("Schedule coordination meeting between disciplines");
                suggestion.SuggestedActions.Add("Review routing options in context");
                suggestion.ConfidenceScore = 0.5;
            }

            return suggestion;
        }

        #endregion

        #region Resolution Workflow

        public ClashResolution CreateResolution(string clashId, ResolutionAction action, string notes)
        {
            lock (_lockObject)
            {
                var clash = _clashResults.Values
                    .SelectMany(r => r.Clashes)
                    .FirstOrDefault(c => c.Id == clashId);

                if (clash == null) return null;

                var resolution = new ClashResolution
                {
                    Id = Guid.NewGuid().ToString(),
                    ClashId = clashId,
                    Action = action,
                    Notes = notes,
                    Status = ResolutionStatus.Proposed,
                    CreatedDate = DateTime.Now,
                    CreatedBy = "Current User",
                    History = new List<ResolutionHistoryEntry>()
                };

                resolution.History.Add(new ResolutionHistoryEntry
                {
                    Date = DateTime.Now,
                    Action = "Created",
                    User = resolution.CreatedBy,
                    Notes = notes
                });

                _resolutions[resolution.Id] = resolution;
                clash.ResolutionId = resolution.Id;
                clash.Status = ClashStatus.Active;

                return resolution;
            }
        }

        public void ApproveResolution(string resolutionId, string approver, string comments)
        {
            lock (_lockObject)
            {
                if (_resolutions.TryGetValue(resolutionId, out var resolution))
                {
                    resolution.Status = ResolutionStatus.Approved;
                    resolution.ApprovedBy = approver;
                    resolution.ApprovedDate = DateTime.Now;

                    resolution.History.Add(new ResolutionHistoryEntry
                    {
                        Date = DateTime.Now,
                        Action = "Approved",
                        User = approver,
                        Notes = comments
                    });

                    // Update clash status
                    var clash = _clashResults.Values
                        .SelectMany(r => r.Clashes)
                        .FirstOrDefault(c => c.Id == resolution.ClashId);

                    if (clash != null)
                    {
                        clash.Status = ClashStatus.Approved;
                    }
                }
            }
        }

        public void ResolveClash(string clashId, string resolver, string verificationNotes)
        {
            lock (_lockObject)
            {
                var clash = _clashResults.Values
                    .SelectMany(r => r.Clashes)
                    .FirstOrDefault(c => c.Id == clashId);

                if (clash != null)
                {
                    clash.Status = ClashStatus.Resolved;
                    clash.ResolvedDate = DateTime.Now;
                    clash.ResolvedBy = resolver;

                    if (_resolutions.TryGetValue(clash.ResolutionId, out var resolution))
                    {
                        resolution.Status = ResolutionStatus.Implemented;
                        resolution.ImplementedDate = DateTime.Now;
                        resolution.History.Add(new ResolutionHistoryEntry
                        {
                            Date = DateTime.Now,
                            Action = "Implemented",
                            User = resolver,
                            Notes = verificationNotes
                        });
                    }

                    OnClashResolved(new ClashResolvedEventArgs
                    {
                        ClashId = clashId,
                        Resolution = resolution?.Action ?? ResolutionAction.Other,
                        Resolver = resolver
                    });
                }
            }
        }

        public void RejectClash(string clashId, string reason)
        {
            lock (_lockObject)
            {
                var clash = _clashResults.Values
                    .SelectMany(r => r.Clashes)
                    .FirstOrDefault(c => c.Id == clashId);

                if (clash != null)
                {
                    clash.Status = ClashStatus.NotAnIssue;
                    clash.RejectionReason = reason;
                }
            }
        }

        #endregion

        #region Trend Analysis

        public async Task<ClashTrendAnalysis> AnalyzeTrendsAsync(string clashProjectId, int periodDays = 30)
        {
            return await Task.Run(() =>
            {
                lock (_lockObject)
                {
                    var results = _clashResults.Values
                        .Where(r => _clashTests.TryGetValue(r.ClashTestId, out var test) &&
                                   test.ClashProjectId == clashProjectId)
                        .Where(r => r.RunDate >= DateTime.Now.AddDays(-periodDays))
                        .OrderBy(r => r.RunDate)
                        .ToList();

                    if (!results.Any())
                        return new ClashTrendAnalysis { ClashProjectId = clashProjectId };

                    var analysis = new ClashTrendAnalysis
                    {
                        ClashProjectId = clashProjectId,
                        AnalysisDate = DateTime.Now,
                        PeriodDays = periodDays,
                        TrendData = new List<TrendDataPoint>(),
                        DisciplineTrends = new Dictionary<ModelDiscipline, DisciplineTrend>(),
                        CategoryTrends = new Dictionary<string, CategoryTrend>(),
                        Insights = new List<string>()
                    };

                    // Build trend data points
                    foreach (var result in results)
                    {
                        analysis.TrendData.Add(new TrendDataPoint
                        {
                            Date = result.RunDate,
                            TotalClashes = result.Clashes.Count,
                            NewClashes = result.Clashes.Count(c => c.Status == ClashStatus.New),
                            ResolvedClashes = result.Clashes.Count(c => c.Status == ClashStatus.Resolved),
                            CriticalClashes = result.Clashes.Count(c => c.Severity == ClashSeverity.Critical)
                        });
                    }

                    // Discipline trends
                    var disciplines = Enum.GetValues<ModelDiscipline>();
                    foreach (var discipline in disciplines)
                    {
                        var disciplineClashes = results
                            .SelectMany(r => r.Clashes)
                            .Where(c => c.ElementADiscipline == discipline || c.ElementBDiscipline == discipline)
                            .ToList();

                        if (disciplineClashes.Any())
                        {
                            var firstHalf = disciplineClashes.Take(disciplineClashes.Count / 2).Count();
                            var secondHalf = disciplineClashes.Skip(disciplineClashes.Count / 2).Count();

                            analysis.DisciplineTrends[discipline] = new DisciplineTrend
                            {
                                Discipline = discipline,
                                TotalClashes = disciplineClashes.Count,
                                ResolvedClashes = disciplineClashes.Count(c => c.Status == ClashStatus.Resolved),
                                TrendDirection = secondHalf > firstHalf ? TrendDirection.Increasing :
                                               secondHalf < firstHalf ? TrendDirection.Decreasing :
                                               TrendDirection.Stable,
                                AverageResolutionDays = CalculateAverageResolutionDays(disciplineClashes)
                            };
                        }
                    }

                    // Category trends
                    var categories = results
                        .SelectMany(r => r.Clashes)
                        .Where(c => c.Category != null)
                        .GroupBy(c => c.Category.Code)
                        .ToList();

                    foreach (var group in categories)
                    {
                        var firstHalf = group.Take(group.Count() / 2).Count();
                        var secondHalf = group.Skip(group.Count() / 2).Count();

                        analysis.CategoryTrends[group.Key] = new CategoryTrend
                        {
                            CategoryCode = group.Key,
                            CategoryName = group.First().Category.Name,
                            TotalClashes = group.Count(),
                            ResolvedClashes = group.Count(c => c.Status == ClashStatus.Resolved),
                            TrendDirection = secondHalf > firstHalf ? TrendDirection.Increasing :
                                           secondHalf < firstHalf ? TrendDirection.Decreasing :
                                           TrendDirection.Stable
                        };
                    }

                    // Generate insights
                    GenerateInsights(analysis);

                    // Check for alerts
                    if (analysis.TrendData.Count >= 2)
                    {
                        var latest = analysis.TrendData.Last();
                        var previous = analysis.TrendData[analysis.TrendData.Count - 2];

                        if (latest.TotalClashes > previous.TotalClashes * 1.5)
                        {
                            OnTrendAlert(new ClashTrendAlertEventArgs
                            {
                                ClashProjectId = clashProjectId,
                                AlertType = TrendAlertType.SignificantIncrease,
                                Message = $"Clash count increased by {(latest.TotalClashes - previous.TotalClashes)} since last run"
                            });
                        }
                    }

                    return analysis;
                }
            });
        }

        private double CalculateAverageResolutionDays(List<Clash> clashes)
        {
            var resolved = clashes.Where(c => c.Status == ClashStatus.Resolved && c.ResolvedDate != default).ToList();
            if (!resolved.Any()) return 0;

            return resolved.Average(c => (c.ResolvedDate - c.DetectedDate).TotalDays);
        }

        private void GenerateInsights(ClashTrendAnalysis analysis)
        {
            // Total trend
            if (analysis.TrendData.Count >= 3)
            {
                var first = analysis.TrendData.First().TotalClashes;
                var last = analysis.TrendData.Last().TotalClashes;
                var change = last - first;
                var percentChange = first > 0 ? (change * 100.0 / first) : 0;

                if (percentChange > 20)
                    analysis.Insights.Add($"Clash count has increased by {percentChange:F0}% over the analysis period");
                else if (percentChange < -20)
                    analysis.Insights.Add($"Clash count has decreased by {Math.Abs(percentChange):F0}% - good progress!");
                else
                    analysis.Insights.Add("Clash count is relatively stable");
            }

            // Discipline insights
            var increasingDisciplines = analysis.DisciplineTrends
                .Where(d => d.Value.TrendDirection == TrendDirection.Increasing)
                .Select(d => d.Key.ToString())
                .ToList();

            if (increasingDisciplines.Any())
            {
                analysis.Insights.Add($"Increasing clashes in: {string.Join(", ", increasingDisciplines)}");
            }

            // Resolution rate
            var totalClashes = analysis.TrendData.Sum(t => t.TotalClashes);
            var resolvedClashes = analysis.TrendData.Sum(t => t.ResolvedClashes);
            var resolutionRate = totalClashes > 0 ? (resolvedClashes * 100.0 / totalClashes) : 0;

            if (resolutionRate < 50)
                analysis.Insights.Add($"Resolution rate is low ({resolutionRate:F0}%) - consider additional coordination meetings");
            else if (resolutionRate > 80)
                analysis.Insights.Add($"Excellent resolution rate ({resolutionRate:F0}%)");

            // Critical clash trend
            var criticalCount = analysis.TrendData.Last().CriticalClashes;
            if (criticalCount > 10)
                analysis.Insights.Add($"High number of critical clashes ({criticalCount}) - prioritize immediate resolution");
        }

        #endregion

        #region Clash Reports

        public ClashReport GenerateClashReport(string clashResultId, ClashReportOptions options)
        {
            lock (_lockObject)
            {
                if (!_clashResults.TryGetValue(clashResultId, out var result))
                    return null;

                var report = new ClashReport
                {
                    Id = Guid.NewGuid().ToString(),
                    ClashResultId = clashResultId,
                    GeneratedDate = DateTime.Now,
                    ReportType = options.ReportType,
                    Sections = new List<ReportSection>()
                };

                // Executive Summary
                report.Sections.Add(new ReportSection
                {
                    Title = "Executive Summary",
                    Content = GenerateExecutiveSummary(result)
                });

                // Statistics
                if (options.IncludeStatistics)
                {
                    report.Sections.Add(new ReportSection
                    {
                        Title = "Clash Statistics",
                        Content = GenerateStatisticsSection(result.Statistics)
                    });
                }

                // By Discipline
                if (options.GroupByDiscipline)
                {
                    var disciplineGroups = result.Clashes
                        .GroupBy(c => c.ResponsibleDiscipline)
                        .OrderByDescending(g => g.Count());

                    foreach (var group in disciplineGroups)
                    {
                        report.Sections.Add(new ReportSection
                        {
                            Title = $"{group.Key} Discipline Clashes",
                            Content = GenerateDisciplineSection(group.Key, group.ToList())
                        });
                    }
                }

                // By Location
                if (options.GroupByLocation)
                {
                    var locationGroups = result.Clashes
                        .Where(c => c.Location?.Level != null)
                        .GroupBy(c => c.Location.Level)
                        .OrderBy(g => g.Key);

                    foreach (var group in locationGroups)
                    {
                        report.Sections.Add(new ReportSection
                        {
                            Title = $"{group.Key} Clashes",
                            Content = GenerateLocationSection(group.Key, group.ToList())
                        });
                    }
                }

                // Critical Clashes Detail
                if (options.IncludeCriticalDetail)
                {
                    var criticalClashes = result.Clashes
                        .Where(c => c.Severity == ClashSeverity.Critical)
                        .OrderByDescending(c => c.PriorityScore)
                        .ToList();

                    report.Sections.Add(new ReportSection
                    {
                        Title = "Critical Clashes - Detailed",
                        Content = GenerateCriticalClashesSection(criticalClashes)
                    });
                }

                // Recommendations
                report.Sections.Add(new ReportSection
                {
                    Title = "Recommendations",
                    Content = GenerateRecommendations(result)
                });

                return report;
            }
        }

        private string GenerateExecutiveSummary(ClashDetectionResult result)
        {
            var summary = new System.Text.StringBuilder();
            summary.AppendLine($"Total Clashes Detected: {result.Clashes.Count}");
            summary.AppendLine($"Critical: {result.Statistics.CriticalCount}");
            summary.AppendLine($"Major: {result.Statistics.MajorCount}");
            summary.AppendLine($"Moderate: {result.Statistics.ModerateCount}");
            summary.AppendLine($"Minor: {result.Statistics.MinorCount}");
            summary.AppendLine();
            summary.AppendLine($"New Clashes: {result.Statistics.NewCount}");
            summary.AppendLine($"Active: {result.Statistics.ActiveCount}");
            summary.AppendLine($"Resolved: {result.Statistics.ResolvedCount}");
            return summary.ToString();
        }

        private string GenerateStatisticsSection(ClashStatistics stats)
        {
            var content = new System.Text.StringBuilder();
            content.AppendLine("Clash Distribution by Type:");
            content.AppendLine($"  Hard Clashes: {stats.HardClashCount}");
            content.AppendLine($"  Clearance Clashes: {stats.ClearanceClashCount}");
            content.AppendLine($"  Duplicate Clashes: {stats.DuplicateClashCount}");
            content.AppendLine();
            content.AppendLine($"Average Priority Score: {stats.AveragePriorityScore:F1}");
            content.AppendLine($"Highest Priority Score: {stats.MaxPriorityScore}");
            return content.ToString();
        }

        private string GenerateDisciplineSection(ModelDiscipline discipline, List<Clash> clashes)
        {
            var content = new System.Text.StringBuilder();
            content.AppendLine($"Total: {clashes.Count} clashes");
            content.AppendLine($"Critical: {clashes.Count(c => c.Severity == ClashSeverity.Critical)}");
            content.AppendLine($"Pending Resolution: {clashes.Count(c => c.Status == ClashStatus.New || c.Status == ClashStatus.Active)}");
            return content.ToString();
        }

        private string GenerateLocationSection(string level, List<Clash> clashes)
        {
            var content = new System.Text.StringBuilder();
            content.AppendLine($"Total: {clashes.Count} clashes on {level}");

            var byGrid = clashes
                .Where(c => c.Location?.GridIntersection != null)
                .GroupBy(c => c.Location.GridIntersection)
                .OrderByDescending(g => g.Count())
                .Take(5);

            content.AppendLine("Top Grid Locations:");
            foreach (var grid in byGrid)
            {
                content.AppendLine($"  {grid.Key}: {grid.Count()} clashes");
            }
            return content.ToString();
        }

        private string GenerateCriticalClashesSection(List<Clash> clashes)
        {
            var content = new System.Text.StringBuilder();
            foreach (var clash in clashes.Take(20))
            {
                content.AppendLine($"[{clash.PriorityScore}] {clash.ElementAName} vs {clash.ElementBName}");
                content.AppendLine($"  Location: {clash.Location?.Level}, {clash.Location?.GridIntersection}");
                content.AppendLine($"  Responsible: {clash.ResponsibleDiscipline}");
                content.AppendLine($"  Suggested: {clash.SuggestedResolution?.PrimaryAction}");
                content.AppendLine();
            }
            return content.ToString();
        }

        private string GenerateRecommendations(ClashDetectionResult result)
        {
            var recommendations = new List<string>();

            if (result.Statistics.CriticalCount > 20)
                recommendations.Add("Schedule urgent coordination meeting to address critical clashes");

            var topDiscipline = result.Clashes
                .GroupBy(c => c.ResponsibleDiscipline)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();

            if (topDiscipline != null)
                recommendations.Add($"Focus coordination efforts on {topDiscipline.Key} - highest clash count");

            if (result.Statistics.NewCount > result.Statistics.ResolvedCount * 2)
                recommendations.Add("Resolution rate is falling behind detection - increase coordination resources");

            return string.Join(Environment.NewLine, recommendations);
        }

        #endregion

        #region Discipline Accountability

        public DisciplineAccountability GetDisciplineAccountability(string clashProjectId)
        {
            lock (_lockObject)
            {
                var results = _clashResults.Values
                    .Where(r => _clashTests.TryGetValue(r.ClashTestId, out var test) &&
                               test.ClashProjectId == clashProjectId)
                    .ToList();

                var allClashes = results.SelectMany(r => r.Clashes).ToList();

                var accountability = new DisciplineAccountability
                {
                    ClashProjectId = clashProjectId,
                    GeneratedDate = DateTime.Now,
                    DisciplineMetrics = new Dictionary<ModelDiscipline, DisciplineMetrics>()
                };

                foreach (var discipline in Enum.GetValues<ModelDiscipline>())
                {
                    var responsibleClashes = allClashes
                        .Where(c => c.ResponsibleDiscipline == discipline)
                        .ToList();

                    if (!responsibleClashes.Any()) continue;

                    var resolved = responsibleClashes.Where(c => c.Status == ClashStatus.Resolved).ToList();

                    accountability.DisciplineMetrics[discipline] = new DisciplineMetrics
                    {
                        Discipline = discipline,
                        TotalResponsible = responsibleClashes.Count,
                        Resolved = resolved.Count,
                        Pending = responsibleClashes.Count - resolved.Count,
                        Critical = responsibleClashes.Count(c => c.Severity == ClashSeverity.Critical),
                        ResolutionRate = responsibleClashes.Count > 0
                            ? (resolved.Count * 100.0 / responsibleClashes.Count) : 0,
                        AverageResolutionDays = resolved.Any()
                            ? resolved.Average(c => (c.ResolvedDate - c.DetectedDate).TotalDays) : 0
                    };
                }

                return accountability;
            }
        }

        #endregion

        #region Helper Methods

        private void InitializeClashRules()
        {
            _clashRules.AddRange(new[]
            {
                new ClashRule
                {
                    Id = "duct-clearance",
                    Name = "Duct Clearance",
                    Description = "Minimum clearance around ducts for maintenance",
                    MinClearance = 100,
                    AppliesTo = new[] { "Duct" }
                },
                new ClashRule
                {
                    Id = "pipe-insulation",
                    Name = "Pipe Insulation Clearance",
                    Description = "Account for pipe insulation thickness",
                    MinClearance = 50,
                    AppliesTo = new[] { "Pipe" }
                },
                new ClashRule
                {
                    Id = "structural-clearance",
                    Name = "Structural Clearance",
                    Description = "Minimum clearance from structural elements",
                    MinClearance = 25,
                    AppliesTo = new[] { "Beam", "Column" }
                }
            });
        }

        private void InitializeCategories()
        {
            _categories.AddRange(new[]
            {
                new ClashCategory
                {
                    Code = "MEP-STR",
                    Name = "MEP vs Structure",
                    Description = "Clashes between MEP systems and structural elements",
                    Disciplines = new[] { ModelDiscipline.Mechanical, ModelDiscipline.Electrical,
                                         ModelDiscipline.Plumbing, ModelDiscipline.Structural },
                    PriorityModifier = 10
                },
                new ClashCategory
                {
                    Code = "MEP-MEP",
                    Name = "MEP vs MEP",
                    Description = "Clashes between different MEP systems",
                    Disciplines = new[] { ModelDiscipline.Mechanical, ModelDiscipline.Electrical,
                                         ModelDiscipline.Plumbing },
                    PriorityModifier = 0
                },
                new ClashCategory
                {
                    Code = "ARCH-MEP",
                    Name = "Architecture vs MEP",
                    Description = "Clashes between architectural and MEP elements",
                    Disciplines = new[] { ModelDiscipline.Architectural, ModelDiscipline.Mechanical,
                                         ModelDiscipline.Electrical, ModelDiscipline.Plumbing },
                    PriorityModifier = 5
                },
                new ClashCategory
                {
                    Code = "FP",
                    Name = "Fire Protection",
                    Description = "Clashes involving fire protection systems",
                    Disciplines = new[] { ModelDiscipline.FireProtection },
                    PriorityModifier = 15
                },
                new ClashCategory
                {
                    Code = "MISC",
                    Name = "Miscellaneous",
                    Description = "Other clashes",
                    Disciplines = Array.Empty<ModelDiscipline>(),
                    PriorityModifier = -5
                }
            });
        }

        private ClashStatistics CalculateStatistics(List<Clash> clashes)
        {
            return new ClashStatistics
            {
                TotalCount = clashes.Count,
                NewCount = clashes.Count(c => c.Status == ClashStatus.New),
                ActiveCount = clashes.Count(c => c.Status == ClashStatus.Active),
                ResolvedCount = clashes.Count(c => c.Status == ClashStatus.Resolved),
                CriticalCount = clashes.Count(c => c.Severity == ClashSeverity.Critical),
                MajorCount = clashes.Count(c => c.Severity == ClashSeverity.Major),
                ModerateCount = clashes.Count(c => c.Severity == ClashSeverity.Moderate),
                MinorCount = clashes.Count(c => c.Severity == ClashSeverity.Minor),
                HardClashCount = clashes.Count(c => c.ClashType == ClashType.Hard),
                ClearanceClashCount = clashes.Count(c => c.ClashType == ClashType.Clearance),
                DuplicateClashCount = clashes.Count(c => c.ClashType == ClashType.Duplicate),
                AveragePriorityScore = clashes.Any() ? clashes.Average(c => c.PriorityScore) : 0,
                MaxPriorityScore = clashes.Any() ? clashes.Max(c => c.PriorityScore) : 0
            };
        }

        #endregion

        #region Events

        private void OnClashDetected(ClashDetectedEventArgs e)
        {
            ClashDetected?.Invoke(this, e);
        }

        private void OnClashResolved(ClashResolvedEventArgs e)
        {
            ClashResolved?.Invoke(this, e);
        }

        private void OnTrendAlert(ClashTrendAlertEventArgs e)
        {
            TrendAlert?.Invoke(this, e);
        }

        #endregion
    }

    #region Data Models

    public class ClashProject
    {
        public string Id { get; set; }
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public ProjectStatus Status { get; set; }
        public DateTime CreatedDate { get; set; }
        public List<ClashModel> Models { get; set; }
        public List<ClashSet> ClashSets { get; set; }
        public List<ClashWorkflow> Workflows { get; set; }
    }

    public class ClashModel
    {
        public string Id { get; set; }
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public ModelDiscipline Discipline { get; set; }
        public DateTime LastUpdated { get; set; }
        public int ElementCount { get; set; }
        public int Version { get; set; }
    }

    public class ClashSet
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public List<string> ModelIds { get; set; }
        public string SelectionCriteria { get; set; }
    }

    public class ClashWorkflow
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public List<WorkflowStep> Steps { get; set; }
    }

    public class WorkflowStep
    {
        public int Order { get; set; }
        public string Name { get; set; }
        public string AssignedRole { get; set; }
        public int MaxDays { get; set; }
    }

    public class ClashTest
    {
        public string Id { get; set; }
        public string ClashProjectId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public ClashSelection SelectionA { get; set; }
        public ClashSelection SelectionB { get; set; }
        public ClashType ClashType { get; set; }
        public double Tolerance { get; set; }
        public ClashTestStatus Status { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime LastRunDate { get; set; }
        public string LastResultId { get; set; }
        public List<string> Rules { get; set; }
    }

    public class ClashTestDefinition
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public ClashSelection SelectionA { get; set; }
        public ClashSelection SelectionB { get; set; }
        public ClashType ClashType { get; set; }
        public double Tolerance { get; set; }
        public List<string> Rules { get; set; }
    }

    public class ClashSelection
    {
        public string Name { get; set; }
        public List<string> ModelIds { get; set; }
        public List<string> Categories { get; set; }
        public Dictionary<string, string> Filters { get; set; }
    }

    public class ClashDetectionResult
    {
        public string Id { get; set; }
        public string ClashTestId { get; set; }
        public DateTime RunDate { get; set; }
        public List<Clash> Clashes { get; set; }
        public ClashStatistics Statistics { get; set; }
    }

    public class Clash
    {
        public string Id { get; set; }
        public string ClashTestId { get; set; }
        public string ElementAId { get; set; }
        public string ElementAName { get; set; }
        public ModelDiscipline ElementADiscipline { get; set; }
        public string ElementBId { get; set; }
        public string ElementBName { get; set; }
        public ModelDiscipline ElementBDiscipline { get; set; }
        public ClashType ClashType { get; set; }
        public double Distance { get; set; }
        public ClashLocation Location { get; set; }
        public ClashCategory Category { get; set; }
        public int PriorityScore { get; set; }
        public ClashSeverity Severity { get; set; }
        public ClashStatus Status { get; set; }
        public ModelDiscipline ResponsibleDiscipline { get; set; }
        public ClashResolutionSuggestion SuggestedResolution { get; set; }
        public string ResolutionId { get; set; }
        public DateTime DetectedDate { get; set; }
        public DateTime ResolvedDate { get; set; }
        public string ResolvedBy { get; set; }
        public string RejectionReason { get; set; }
    }

    public class ClashResult
    {
        public string Id { get; set; }
        public string ClashTestId { get; set; }
        public DateTime RunDate { get; set; }
        public List<Clash> Clashes { get; set; } = new();
        public int TotalClashes { get; set; }
        public int NewClashes { get; set; }
        public int ResolvedClashes { get; set; }
        public ClashStatistics Statistics { get; set; }
    }

    public class ClashLocation
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public string Level { get; set; }
        public string GridIntersection { get; set; }
        public string Room { get; set; }
    }

    public class ClashCategory
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public ModelDiscipline[] Disciplines { get; set; }
        public int? PriorityModifier { get; set; }

        public bool MatchesClash(Clash clash)
        {
            if (Disciplines == null || Disciplines.Length == 0)
                return false;

            return Disciplines.Contains(clash.ElementADiscipline) ||
                   Disciplines.Contains(clash.ElementBDiscipline);
        }
    }

    public class ClashRule
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public double MinClearance { get; set; }
        public string[] AppliesTo { get; set; }
    }

    public class ClashStatistics
    {
        public int TotalCount { get; set; }
        public int NewCount { get; set; }
        public int ActiveCount { get; set; }
        public int ResolvedCount { get; set; }
        public int CriticalCount { get; set; }
        public int MajorCount { get; set; }
        public int ModerateCount { get; set; }
        public int MinorCount { get; set; }
        public int HardClashCount { get; set; }
        public int ClearanceClashCount { get; set; }
        public int DuplicateClashCount { get; set; }
        public double AveragePriorityScore { get; set; }
        public int MaxPriorityScore { get; set; }
    }

    public class ClashResolutionSuggestion
    {
        public string ClashId { get; set; }
        public ResolutionAction PrimaryAction { get; set; }
        public List<string> SuggestedActions { get; set; }
        public ResolutionEffort EstimatedEffort { get; set; }
        public double ConfidenceScore { get; set; }
    }

    public class ClashResolution
    {
        public string Id { get; set; }
        public string ClashId { get; set; }
        public ResolutionAction Action { get; set; }
        public string Notes { get; set; }
        public ResolutionStatus Status { get; set; }
        public DateTime CreatedDate { get; set; }
        public string CreatedBy { get; set; }
        public string ApprovedBy { get; set; }
        public DateTime ApprovedDate { get; set; }
        public DateTime ImplementedDate { get; set; }
        public List<ResolutionHistoryEntry> History { get; set; }
    }

    public class ResolutionHistoryEntry
    {
        public DateTime Date { get; set; }
        public string Action { get; set; }
        public string User { get; set; }
        public string Notes { get; set; }
    }

    public class ClashTrendAnalysis
    {
        public string ClashProjectId { get; set; }
        public DateTime AnalysisDate { get; set; }
        public int PeriodDays { get; set; }
        public List<TrendDataPoint> TrendData { get; set; }
        public Dictionary<ModelDiscipline, DisciplineTrend> DisciplineTrends { get; set; }
        public Dictionary<string, CategoryTrend> CategoryTrends { get; set; }
        public List<string> Insights { get; set; }
    }

    public class TrendDataPoint
    {
        public DateTime Date { get; set; }
        public int TotalClashes { get; set; }
        public int NewClashes { get; set; }
        public int ResolvedClashes { get; set; }
        public int CriticalClashes { get; set; }
    }

    public class DisciplineTrend
    {
        public ModelDiscipline Discipline { get; set; }
        public int TotalClashes { get; set; }
        public int ResolvedClashes { get; set; }
        public TrendDirection TrendDirection { get; set; }
        public double AverageResolutionDays { get; set; }
    }

    public class CategoryTrend
    {
        public string CategoryCode { get; set; }
        public string CategoryName { get; set; }
        public int TotalClashes { get; set; }
        public int ResolvedClashes { get; set; }
        public TrendDirection TrendDirection { get; set; }
    }

    public class ClashReport
    {
        public string Id { get; set; }
        public string ClashResultId { get; set; }
        public DateTime GeneratedDate { get; set; }
        public ClashReportType ReportType { get; set; }
        public List<ReportSection> Sections { get; set; }
    }

    public class ReportSection
    {
        public string Title { get; set; }
        public string Content { get; set; }
    }

    public class ClashReportOptions
    {
        public ClashReportType ReportType { get; set; }
        public bool IncludeStatistics { get; set; } = true;
        public bool GroupByDiscipline { get; set; } = true;
        public bool GroupByLocation { get; set; } = false;
        public bool IncludeCriticalDetail { get; set; } = true;
    }

    public class DisciplineAccountability
    {
        public string ClashProjectId { get; set; }
        public DateTime GeneratedDate { get; set; }
        public Dictionary<ModelDiscipline, DisciplineMetrics> DisciplineMetrics { get; set; }
    }

    public class DisciplineMetrics
    {
        public ModelDiscipline Discipline { get; set; }
        public int TotalResponsible { get; set; }
        public int Resolved { get; set; }
        public int Pending { get; set; }
        public int Critical { get; set; }
        public double ResolutionRate { get; set; }
        public double AverageResolutionDays { get; set; }
    }

    #endregion

    #region Enums

    public enum ModelDiscipline
    {
        Architectural,
        Structural,
        Mechanical,
        Electrical,
        Plumbing,
        FireProtection,
        Civil,
        Landscape,
        Coordination
    }

    public enum ProjectStatus
    {
        Active,
        OnHold,
        Completed,
        Archived
    }

    public enum ClashType
    {
        Hard,
        Clearance,
        Duplicate,
        Workflow
    }

    public enum ClashTestStatus
    {
        Ready,
        Running,
        Completed,
        Error
    }

    public enum ClashSeverity
    {
        Critical,
        Major,
        Moderate,
        Minor
    }

    public enum ClashStatus
    {
        New,
        Active,
        Approved,
        Resolved,
        NotAnIssue
    }

    public enum ResolutionAction
    {
        RouteAround,
        AdjustElevation,
        Resize,
        Relocate,
        SplitRun,
        AddFitting,
        CoordinationRequired,
        AcceptableAsIs,
        Other
    }

    public enum ResolutionEffort
    {
        Low,
        Medium,
        High
    }

    public enum ResolutionStatus
    {
        Proposed,
        UnderReview,
        Approved,
        Rejected,
        Implemented,
        Verified
    }

    public enum TrendDirection
    {
        Increasing,
        Decreasing,
        Stable
    }

    public enum TrendAlertType
    {
        SignificantIncrease,
        HighCriticalCount,
        LowResolutionRate,
        DisciplineIssue
    }

    public enum ClashReportType
    {
        Executive,
        Detailed,
        Discipline,
        Location,
        Trend
    }

    #endregion

    #region Event Args

    public class ClashDetectedEventArgs : EventArgs
    {
        public string ClashTestId { get; set; }
        public int TotalClashes { get; set; }
        public int CriticalClashes { get; set; }
        public int NewClashes { get; set; }
    }

    public class ClashResolvedEventArgs : EventArgs
    {
        public string ClashId { get; set; }
        public ResolutionAction Resolution { get; set; }
        public string Resolver { get; set; }
    }

    public class ClashTrendAlertEventArgs : EventArgs
    {
        public string ClashProjectId { get; set; }
        public TrendAlertType AlertType { get; set; }
        public string Message { get; set; }
    }

    #endregion
}
