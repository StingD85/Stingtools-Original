// ===================================================================
// StingBIM Knowledge Management Engine
// Lessons learned, best practices, and project benchmarking
// Copyright (c) 2026 StingBIM. All rights reserved.
// ===================================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.KnowledgeManagement
{
    /// <summary>
    /// Comprehensive knowledge management for capturing lessons learned,
    /// maintaining best practices, and enabling project benchmarking
    /// </summary>
    public sealed class KnowledgeManagementEngine
    {
        private static readonly Lazy<KnowledgeManagementEngine> _instance =
            new Lazy<KnowledgeManagementEngine>(() => new KnowledgeManagementEngine());
        public static KnowledgeManagementEngine Instance => _instance.Value;

        private readonly ConcurrentDictionary<string, LessonLearned> _lessons;
        private readonly ConcurrentDictionary<string, BestPractice> _bestPractices;
        private readonly ConcurrentDictionary<string, ProjectRecord> _projectRecords;
        private readonly ConcurrentDictionary<string, KnowledgeArticle> _articles;
        private readonly object _lockObject = new object();

        public event EventHandler<KnowledgeEventArgs> NewKnowledgeAdded;

        private KnowledgeManagementEngine()
        {
            _lessons = new ConcurrentDictionary<string, LessonLearned>();
            _bestPractices = new ConcurrentDictionary<string, BestPractice>();
            _projectRecords = new ConcurrentDictionary<string, ProjectRecord>();
            _articles = new ConcurrentDictionary<string, KnowledgeArticle>();

            InitializeBestPractices();
        }

        #region Best Practices Library

        private void InitializeBestPractices()
        {
            var practices = new List<BestPractice>
            {
                // BIM Coordination
                new BestPractice
                {
                    Id = "BP-BIM-001",
                    Title = "Weekly BIM Coordination Meetings",
                    Category = "BIM Coordination",
                    Description = "Hold weekly coordination meetings with all trade modelers to review clash detection results and resolve conflicts",
                    Benefits = new List<string> { "Reduced RFIs", "Fewer field conflicts", "Improved schedule adherence" },
                    Implementation = "Schedule 2-hour weekly meetings with Navisworks review. Assign action items with deadlines.",
                    Metrics = new List<string> { "Clash count trend", "Resolution time", "RFI reduction" },
                    ApplicableProjectTypes = new List<string> { "Commercial", "Healthcare", "Education" },
                    Status = BestPracticeStatus.Active
                },
                new BestPractice
                {
                    Id = "BP-BIM-002",
                    Title = "LOD Specification in BIM Execution Plan",
                    Category = "BIM Coordination",
                    Description = "Define clear Level of Development requirements for each model element at each project phase",
                    Benefits = new List<string> { "Clear expectations", "Reduced rework", "Better coordination" },
                    Implementation = "Include LOD matrix in BEP. Review at kickoff and phase milestones.",
                    Metrics = new List<string> { "Model audit compliance", "Coordination issue count" },
                    ApplicableProjectTypes = new List<string> { "All" },
                    Status = BestPracticeStatus.Active
                },
                // Quality Management
                new BestPractice
                {
                    Id = "BP-QA-001",
                    Title = "Pre-Installation Meetings",
                    Category = "Quality Assurance",
                    Description = "Hold pre-installation meetings with subcontractors before starting any major scope of work",
                    Benefits = new List<string> { "Clear expectations", "Quality first-time", "Reduced defects" },
                    Implementation = "Schedule meetings 2 weeks before work starts. Review specs, drawings, and samples.",
                    Metrics = new List<string> { "First-pass inspection rate", "Defect count by trade" },
                    ApplicableProjectTypes = new List<string> { "All" },
                    Status = BestPracticeStatus.Active
                },
                new BestPractice
                {
                    Id = "BP-QA-002",
                    Title = "Mock-Up Approval Process",
                    Category = "Quality Assurance",
                    Description = "Require approved mock-ups for all major finish assemblies before full installation",
                    Benefits = new List<string> { "Visual quality standard", "Reduced rework", "Owner buy-in" },
                    Implementation = "Identify mock-up scope in specs. Build in dedicated area. Get written approval.",
                    Metrics = new List<string> { "Rework cost reduction", "Punch list reduction" },
                    ApplicableProjectTypes = new List<string> { "Commercial", "Healthcare", "Hospitality" },
                    Status = BestPracticeStatus.Active
                },
                // Safety
                new BestPractice
                {
                    Id = "BP-SAF-001",
                    Title = "Daily Safety Huddles",
                    Category = "Safety",
                    Description = "Conduct brief daily safety meetings at shift start to review hazards and safety focus",
                    Benefits = new List<string> { "Safety awareness", "Incident reduction", "Team engagement" },
                    Implementation = "5-10 minute meetings covering day's work hazards. Document attendance.",
                    Metrics = new List<string> { "TRIR", "Near-miss reports", "Safety observation count" },
                    ApplicableProjectTypes = new List<string> { "All" },
                    Status = BestPracticeStatus.Active
                },
                // Procurement
                new BestPractice
                {
                    Id = "BP-PROC-001",
                    Title = "Early Procurement of Long-Lead Items",
                    Category = "Procurement",
                    Description = "Identify and procure long-lead equipment during design to prevent schedule delays",
                    Benefits = new List<string> { "Schedule protection", "Price certainty", "Risk reduction" },
                    Implementation = "Identify items with >90 day lead time. Issue early packages. Track submittals.",
                    Metrics = new List<string> { "Long-lead item delivery vs. need date" },
                    ApplicableProjectTypes = new List<string> { "Commercial", "Healthcare", "Industrial" },
                    Status = BestPracticeStatus.Active
                },
                // Cost Management
                new BestPractice
                {
                    Id = "BP-COST-001",
                    Title = "Monthly Cost Forecasting",
                    Category = "Cost Management",
                    Description = "Prepare detailed cost forecast monthly including committed costs and anticipated changes",
                    Benefits = new List<string> { "Early issue identification", "Budget control", "No surprises" },
                    Implementation = "Update forecast by 5th of each month. Review with PM and client.",
                    Metrics = new List<string> { "Forecast accuracy", "Cost variance trend" },
                    ApplicableProjectTypes = new List<string> { "All" },
                    Status = BestPracticeStatus.Active
                },
                // Schedule Management
                new BestPractice
                {
                    Id = "BP-SCHED-001",
                    Title = "4-Week Look-Ahead Schedule",
                    Category = "Schedule Management",
                    Description = "Maintain detailed 4-week look-ahead schedule updated weekly with trade input",
                    Benefits = new List<string> { "Resource planning", "Coordination", "Progress tracking" },
                    Implementation = "Update every Monday. Review in weekly OAC meeting. Track PPC.",
                    Metrics = new List<string> { "Percent Plan Complete (PPC)", "Variance analysis" },
                    ApplicableProjectTypes = new List<string> { "All" },
                    Status = BestPracticeStatus.Active
                },
                // Commissioning
                new BestPractice
                {
                    Id = "BP-CX-001",
                    Title = "Enhanced Commissioning",
                    Category = "Commissioning",
                    Description = "Engage commissioning agent during design for review and during construction for execution",
                    Benefits = new List<string> { "Systems perform as designed", "Energy efficiency", "Fewer callbacks" },
                    Implementation = "Include Cx in design reviews. Develop comprehensive Cx plan. Document all testing.",
                    Metrics = new List<string> { "Issue resolution rate", "Energy performance vs. model" },
                    ApplicableProjectTypes = new List<string> { "Commercial", "Healthcare", "Education" },
                    Status = BestPracticeStatus.Active
                }
            };

            foreach (var practice in practices)
            {
                practice.CreatedDate = DateTime.UtcNow.AddYears(-2);
                practice.LastReviewed = DateTime.UtcNow.AddMonths(-3);
                _bestPractices.TryAdd(practice.Id, practice);
            }
        }

        #endregion

        #region Lessons Learned

        public LessonLearned CaptureLessonLearned(LessonLearnedRequest request)
        {
            var lesson = new LessonLearned
            {
                Id = Guid.NewGuid().ToString(),
                ProjectId = request.ProjectId,
                ProjectName = request.ProjectName,
                Title = request.Title,
                Category = request.Category,
                Type = request.Type,
                Description = request.Description,
                Context = request.Context,
                Impact = request.Impact,
                RootCause = request.RootCause,
                ActionTaken = request.ActionTaken,
                Recommendation = request.Recommendation,
                SubmittedBy = request.SubmittedBy,
                SubmittedDate = DateTime.UtcNow,
                Status = LessonStatus.Draft,
                Tags = request.Tags ?? new List<string>(),
                Attachments = request.Attachments ?? new List<string>(),
                RelatedLessons = new List<string>(),
                Views = 0,
                UsefulVotes = 0
            };

            // Find related lessons
            lesson.RelatedLessons = FindRelatedLessons(lesson);

            _lessons.TryAdd(lesson.Id, lesson);

            NewKnowledgeAdded?.Invoke(this, new KnowledgeEventArgs
            {
                Type = "Lesson Learned",
                Id = lesson.Id,
                Title = lesson.Title
            });

            return lesson;
        }

        private List<string> FindRelatedLessons(LessonLearned lesson)
        {
            var related = _lessons.Values
                .Where(l => l.Id != lesson.Id &&
                           (l.Category == lesson.Category ||
                            l.Tags.Any(t => lesson.Tags.Contains(t))))
                .OrderByDescending(l => l.UsefulVotes)
                .Take(5)
                .Select(l => l.Id)
                .ToList();

            return related;
        }

        public LessonLearned ApproveLessonLearned(string lessonId, ApprovalRequest request)
        {
            if (!_lessons.TryGetValue(lessonId, out var lesson))
                return null;

            lesson.Status = request.Approved ? LessonStatus.Approved : LessonStatus.Rejected;
            lesson.ReviewedBy = request.ReviewedBy;
            lesson.ReviewedDate = DateTime.UtcNow;
            lesson.ReviewNotes = request.Notes;

            // If approved, check if it should become a best practice
            if (request.Approved && request.PromoteToBestPractice)
            {
                PromoteToBestPractice(lesson);
            }

            return lesson;
        }

        private void PromoteToBestPractice(LessonLearned lesson)
        {
            var practice = new BestPractice
            {
                Id = $"BP-LL-{lesson.Id.Substring(0, 8)}",
                Title = lesson.Title,
                Category = lesson.Category,
                Description = lesson.Recommendation,
                Benefits = new List<string> { lesson.Impact },
                Implementation = lesson.ActionTaken,
                SourceLessonId = lesson.Id,
                CreatedDate = DateTime.UtcNow,
                Status = BestPracticeStatus.Draft
            };

            _bestPractices.TryAdd(practice.Id, practice);
            lesson.PromotedToBestPractice = practice.Id;
        }

        public List<LessonLearned> SearchLessons(LessonSearchCriteria criteria)
        {
            var query = _lessons.Values.AsQueryable();

            if (!string.IsNullOrEmpty(criteria.Category))
            {
                query = query.Where(l => l.Category == criteria.Category);
            }

            if (criteria.Type.HasValue)
            {
                query = query.Where(l => l.Type == criteria.Type.Value);
            }

            if (!string.IsNullOrEmpty(criteria.ProjectId))
            {
                query = query.Where(l => l.ProjectId == criteria.ProjectId);
            }

            if (criteria.Tags != null && criteria.Tags.Any())
            {
                query = query.Where(l => l.Tags.Any(t => criteria.Tags.Contains(t)));
            }

            if (!string.IsNullOrEmpty(criteria.SearchText))
            {
                var searchLower = criteria.SearchText.ToLower();
                query = query.Where(l =>
                    l.Title.ToLower().Contains(searchLower) ||
                    l.Description.ToLower().Contains(searchLower) ||
                    l.Recommendation.ToLower().Contains(searchLower));
            }

            if (criteria.ApprovedOnly)
            {
                query = query.Where(l => l.Status == LessonStatus.Approved);
            }

            return query
                .OrderByDescending(l => l.UsefulVotes)
                .ThenByDescending(l => l.SubmittedDate)
                .ToList();
        }

        public void VoteLessonUseful(string lessonId, string userId)
        {
            if (_lessons.TryGetValue(lessonId, out var lesson))
            {
                lock (_lockObject)
                {
                    lesson.UsefulVotes++;
                    lesson.Views++;
                }
            }
        }

        #endregion

        #region Best Practices

        public BestPractice CreateBestPractice(BestPracticeRequest request)
        {
            var practice = new BestPractice
            {
                Id = Guid.NewGuid().ToString(),
                Title = request.Title,
                Category = request.Category,
                Description = request.Description,
                Benefits = request.Benefits ?? new List<string>(),
                Implementation = request.Implementation,
                Metrics = request.Metrics ?? new List<string>(),
                ApplicableProjectTypes = request.ApplicableProjectTypes ?? new List<string>(),
                RelatedPractices = request.RelatedPractices ?? new List<string>(),
                CreatedDate = DateTime.UtcNow,
                CreatedBy = request.CreatedBy,
                Status = BestPracticeStatus.Draft,
                AdoptionCount = 0
            };

            _bestPractices.TryAdd(practice.Id, practice);

            NewKnowledgeAdded?.Invoke(this, new KnowledgeEventArgs
            {
                Type = "Best Practice",
                Id = practice.Id,
                Title = practice.Title
            });

            return practice;
        }

        public List<BestPractice> GetBestPractices(BestPracticeSearchCriteria criteria)
        {
            var query = _bestPractices.Values.Where(p => p.Status == BestPracticeStatus.Active);

            if (!string.IsNullOrEmpty(criteria.Category))
            {
                query = query.Where(p => p.Category == criteria.Category);
            }

            if (!string.IsNullOrEmpty(criteria.ProjectType))
            {
                query = query.Where(p =>
                    p.ApplicableProjectTypes.Contains("All") ||
                    p.ApplicableProjectTypes.Contains(criteria.ProjectType));
            }

            if (!string.IsNullOrEmpty(criteria.SearchText))
            {
                var searchLower = criteria.SearchText.ToLower();
                query = query.Where(p =>
                    p.Title.ToLower().Contains(searchLower) ||
                    p.Description.ToLower().Contains(searchLower));
            }

            return query
                .OrderByDescending(p => p.AdoptionCount)
                .ThenBy(p => p.Title)
                .ToList();
        }

        public BestPractice RecordAdoption(string practiceId, AdoptionRecord record)
        {
            if (!_bestPractices.TryGetValue(practiceId, out var practice))
                return null;

            lock (_lockObject)
            {
                practice.AdoptionCount++;
                if (practice.AdoptionRecords == null)
                    practice.AdoptionRecords = new List<AdoptionRecord>();

                record.AdoptedDate = DateTime.UtcNow;
                practice.AdoptionRecords.Add(record);
            }

            return practice;
        }

        #endregion

        #region Project Records (Benchmarking)

        public ProjectRecord CreateProjectRecord(ProjectRecordRequest request)
        {
            var record = new ProjectRecord
            {
                Id = Guid.NewGuid().ToString(),
                ProjectName = request.ProjectName,
                ProjectType = request.ProjectType,
                Location = request.Location,
                Client = request.Client,
                DeliveryMethod = request.DeliveryMethod,
                ContractType = request.ContractType,
                StartDate = request.StartDate,
                CompletionDate = request.CompletionDate,
                GrossArea = request.GrossArea,
                Stories = request.Stories,
                ContractValue = request.ContractValue,
                FinalCost = request.FinalCost,
                ScheduledDuration = request.ScheduledDuration,
                ActualDuration = request.ActualDuration,
                CreatedDate = DateTime.UtcNow,
                Metrics = new ProjectMetrics(),
                LessonsLearned = new List<string>()
            };

            // Calculate metrics
            CalculateProjectMetrics(record);

            _projectRecords.TryAdd(record.Id, record);
            return record;
        }

        private void CalculateProjectMetrics(ProjectRecord record)
        {
            record.Metrics = new ProjectMetrics
            {
                CostPerSF = record.GrossArea > 0 ? record.FinalCost / record.GrossArea : 0,
                CostVariance = record.ContractValue > 0 ? (record.FinalCost - record.ContractValue) / record.ContractValue * 100 : 0,
                ScheduleVariance = record.ScheduledDuration > 0 ? (record.ActualDuration - record.ScheduledDuration) / (decimal)record.ScheduledDuration * 100 : 0
            };

            record.Metrics.OnBudget = Math.Abs(record.Metrics.CostVariance) <= 5;
            record.Metrics.OnSchedule = Math.Abs(record.Metrics.ScheduleVariance) <= 5;
        }

        public void UpdateProjectMetrics(string recordId, ProjectMetricsUpdate update)
        {
            if (!_projectRecords.TryGetValue(recordId, out var record))
                return;

            if (update.SafetyTRIR.HasValue)
                record.Metrics.SafetyTRIR = update.SafetyTRIR.Value;

            if (update.QualityDefectRate.HasValue)
                record.Metrics.QualityDefectRate = update.QualityDefectRate.Value;

            if (update.ChangeOrderPercent.HasValue)
                record.Metrics.ChangeOrderPercent = update.ChangeOrderPercent.Value;

            if (update.ClientSatisfaction.HasValue)
                record.Metrics.ClientSatisfaction = update.ClientSatisfaction.Value;

            if (update.ClashesIdentified.HasValue)
                record.Metrics.ClashesIdentified = update.ClashesIdentified.Value;

            if (update.ClashesResolved.HasValue)
                record.Metrics.ClashesResolved = update.ClashesResolved.Value;

            if (update.RFICount.HasValue)
                record.Metrics.RFICount = update.RFICount.Value;

            if (update.PunchListItems.HasValue)
                record.Metrics.PunchListItems = update.PunchListItems.Value;
        }

        public BenchmarkAnalysis BenchmarkProject(BenchmarkRequest request)
        {
            var analysis = new BenchmarkAnalysis
            {
                Id = Guid.NewGuid().ToString(),
                ProjectId = request.ProjectId,
                AnalysisDate = DateTime.UtcNow,
                Comparisons = new List<MetricComparison>(),
                Recommendations = new List<string>()
            };

            // Get project record
            if (!_projectRecords.TryGetValue(request.ProjectId, out var project))
                return null;

            // Find comparable projects
            var comparables = _projectRecords.Values
                .Where(p => p.Id != request.ProjectId &&
                           p.ProjectType == project.ProjectType &&
                           p.GrossArea >= project.GrossArea * 0.5m &&
                           p.GrossArea <= project.GrossArea * 2.0m)
                .ToList();

            if (!comparables.Any())
            {
                // Fall back to same project type only
                comparables = _projectRecords.Values
                    .Where(p => p.Id != request.ProjectId && p.ProjectType == project.ProjectType)
                    .ToList();
            }

            analysis.ComparableProjectCount = comparables.Count;

            if (comparables.Any())
            {
                // Cost per SF comparison
                AddMetricComparison(analysis, "Cost per SF", project.Metrics.CostPerSF,
                    comparables.Select(c => c.Metrics.CostPerSF).ToList(), "$/SF", false);

                // Cost Variance comparison
                AddMetricComparison(analysis, "Cost Variance", project.Metrics.CostVariance,
                    comparables.Select(c => c.Metrics.CostVariance).ToList(), "%", false);

                // Schedule Variance comparison
                AddMetricComparison(analysis, "Schedule Variance", project.Metrics.ScheduleVariance,
                    comparables.Select(c => c.Metrics.ScheduleVariance).ToList(), "%", false);

                // Safety TRIR comparison
                if (project.Metrics.SafetyTRIR > 0)
                {
                    var safetyComparables = comparables.Where(c => c.Metrics.SafetyTRIR > 0).ToList();
                    if (safetyComparables.Any())
                    {
                        AddMetricComparison(analysis, "Safety TRIR", project.Metrics.SafetyTRIR,
                            safetyComparables.Select(c => c.Metrics.SafetyTRIR).ToList(), "", false);
                    }
                }

                // Client Satisfaction comparison
                if (project.Metrics.ClientSatisfaction > 0)
                {
                    var satisfactionComparables = comparables.Where(c => c.Metrics.ClientSatisfaction > 0).ToList();
                    if (satisfactionComparables.Any())
                    {
                        AddMetricComparison(analysis, "Client Satisfaction", project.Metrics.ClientSatisfaction,
                            satisfactionComparables.Select(c => c.Metrics.ClientSatisfaction).ToList(), "/10", true);
                    }
                }
            }

            // Generate recommendations
            GenerateBenchmarkRecommendations(analysis, project);

            return analysis;
        }

        private void AddMetricComparison(BenchmarkAnalysis analysis, string metricName, decimal projectValue,
            List<decimal> comparableValues, string unit, bool higherIsBetter)
        {
            if (!comparableValues.Any()) return;

            var comparison = new MetricComparison
            {
                MetricName = metricName,
                ProjectValue = projectValue,
                Unit = unit,
                BenchmarkMin = comparableValues.Min(),
                BenchmarkMax = comparableValues.Max(),
                BenchmarkAverage = comparableValues.Average(),
                BenchmarkMedian = comparableValues.OrderBy(v => v).ElementAt(comparableValues.Count / 2),
                SampleSize = comparableValues.Count
            };

            // Calculate percentile
            var belowCount = comparableValues.Count(v => higherIsBetter ? v < projectValue : v > projectValue);
            comparison.Percentile = (decimal)belowCount / comparableValues.Count * 100;

            // Determine performance
            if (comparison.Percentile >= 75)
                comparison.Performance = higherIsBetter ? "Top Quartile" : "Bottom Quartile";
            else if (comparison.Percentile >= 50)
                comparison.Performance = "Above Average";
            else if (comparison.Percentile >= 25)
                comparison.Performance = "Below Average";
            else
                comparison.Performance = higherIsBetter ? "Bottom Quartile" : "Top Quartile";

            analysis.Comparisons.Add(comparison);
        }

        private void GenerateBenchmarkRecommendations(BenchmarkAnalysis analysis, ProjectRecord project)
        {
            foreach (var comparison in analysis.Comparisons)
            {
                if (comparison.MetricName == "Cost per SF" && comparison.ProjectValue > comparison.BenchmarkAverage * 1.1m)
                {
                    analysis.Recommendations.Add("Cost per SF is above average - review value engineering opportunities for future projects");
                }

                if (comparison.MetricName == "Schedule Variance" && comparison.ProjectValue > 10)
                {
                    analysis.Recommendations.Add("Schedule overrun occurred - document causes and improve planning for future projects");
                }

                if (comparison.MetricName == "Safety TRIR" && comparison.ProjectValue > comparison.BenchmarkAverage)
                {
                    analysis.Recommendations.Add("Safety performance below peers - review and enhance safety programs");
                }

                if (comparison.MetricName == "Client Satisfaction" && comparison.ProjectValue < comparison.BenchmarkAverage)
                {
                    analysis.Recommendations.Add("Client satisfaction below average - review client feedback and improve communication");
                }
            }

            if (project.Metrics.ChangeOrderPercent > 10)
            {
                analysis.Recommendations.Add("High change order rate - improve scope definition and design coordination");
            }
        }

        #endregion

        #region Knowledge Articles

        public KnowledgeArticle CreateArticle(ArticleRequest request)
        {
            var article = new KnowledgeArticle
            {
                Id = Guid.NewGuid().ToString(),
                Title = request.Title,
                Category = request.Category,
                Content = request.Content,
                Summary = request.Summary,
                Author = request.Author,
                CreatedDate = DateTime.UtcNow,
                Status = ArticleStatus.Draft,
                Tags = request.Tags ?? new List<string>(),
                RelatedArticles = new List<string>(),
                Views = 0,
                Rating = 0
            };

            _articles.TryAdd(article.Id, article);
            return article;
        }

        public KnowledgeArticle PublishArticle(string articleId, string publishedBy)
        {
            if (!_articles.TryGetValue(articleId, out var article))
                return null;

            article.Status = ArticleStatus.Published;
            article.PublishedDate = DateTime.UtcNow;
            article.PublishedBy = publishedBy;

            return article;
        }

        public List<KnowledgeArticle> SearchArticles(ArticleSearchCriteria criteria)
        {
            var query = _articles.Values.Where(a => a.Status == ArticleStatus.Published);

            if (!string.IsNullOrEmpty(criteria.Category))
            {
                query = query.Where(a => a.Category == criteria.Category);
            }

            if (criteria.Tags != null && criteria.Tags.Any())
            {
                query = query.Where(a => a.Tags.Any(t => criteria.Tags.Contains(t)));
            }

            if (!string.IsNullOrEmpty(criteria.SearchText))
            {
                var searchLower = criteria.SearchText.ToLower();
                query = query.Where(a =>
                    a.Title.ToLower().Contains(searchLower) ||
                    a.Content.ToLower().Contains(searchLower) ||
                    a.Summary.ToLower().Contains(searchLower));
            }

            return query
                .OrderByDescending(a => a.Rating)
                .ThenByDescending(a => a.Views)
                .ToList();
        }

        #endregion

        #region Knowledge Dashboard

        public KnowledgeDashboard GetDashboard()
        {
            return new KnowledgeDashboard
            {
                GeneratedDate = DateTime.UtcNow,
                TotalLessons = _lessons.Count,
                ApprovedLessons = _lessons.Values.Count(l => l.Status == LessonStatus.Approved),
                PendingLessons = _lessons.Values.Count(l => l.Status == LessonStatus.Draft),
                TotalBestPractices = _bestPractices.Values.Count(p => p.Status == BestPracticeStatus.Active),
                TotalProjectRecords = _projectRecords.Count,
                TotalArticles = _articles.Values.Count(a => a.Status == ArticleStatus.Published),
                RecentLessons = _lessons.Values
                    .OrderByDescending(l => l.SubmittedDate)
                    .Take(5)
                    .Select(l => new RecentItem { Id = l.Id, Title = l.Title, Date = l.SubmittedDate, Type = "Lesson" })
                    .ToList(),
                TopPractices = _bestPractices.Values
                    .Where(p => p.Status == BestPracticeStatus.Active)
                    .OrderByDescending(p => p.AdoptionCount)
                    .Take(5)
                    .Select(p => new RecentItem { Id = p.Id, Title = p.Title, Date = p.CreatedDate, Type = "Best Practice" })
                    .ToList(),
                CategoryBreakdown = _lessons.Values
                    .GroupBy(l => l.Category)
                    .ToDictionary(g => g.Key ?? "Uncategorized", g => g.Count())
            };
        }

        #endregion

        #region Helper Methods

        public LessonLearned GetLesson(string lessonId)
        {
            _lessons.TryGetValue(lessonId, out var lesson);
            if (lesson != null) lesson.Views++;
            return lesson;
        }

        public BestPractice GetBestPractice(string practiceId)
        {
            _bestPractices.TryGetValue(practiceId, out var practice);
            return practice;
        }

        public ProjectRecord GetProjectRecord(string recordId)
        {
            _projectRecords.TryGetValue(recordId, out var record);
            return record;
        }

        public List<string> GetCategories()
        {
            var categories = new HashSet<string>();

            foreach (var lesson in _lessons.Values)
                if (!string.IsNullOrEmpty(lesson.Category))
                    categories.Add(lesson.Category);

            foreach (var practice in _bestPractices.Values)
                if (!string.IsNullOrEmpty(practice.Category))
                    categories.Add(practice.Category);

            return categories.OrderBy(c => c).ToList();
        }

        #endregion
    }

    #region Data Models

    public class LessonLearned
    {
        public string Id { get; set; }
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public string Title { get; set; }
        public string Category { get; set; }
        public LessonType Type { get; set; }
        public string Description { get; set; }
        public string Context { get; set; }
        public string Impact { get; set; }
        public string RootCause { get; set; }
        public string ActionTaken { get; set; }
        public string Recommendation { get; set; }
        public string SubmittedBy { get; set; }
        public DateTime SubmittedDate { get; set; }
        public string ReviewedBy { get; set; }
        public DateTime? ReviewedDate { get; set; }
        public string ReviewNotes { get; set; }
        public LessonStatus Status { get; set; }
        public List<string> Tags { get; set; }
        public List<string> Attachments { get; set; }
        public List<string> RelatedLessons { get; set; }
        public int Views { get; set; }
        public int UsefulVotes { get; set; }
        public string PromotedToBestPractice { get; set; }
    }

    public class LessonLearnedRequest
    {
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public string Title { get; set; }
        public string Category { get; set; }
        public LessonType Type { get; set; }
        public string Description { get; set; }
        public string Context { get; set; }
        public string Impact { get; set; }
        public string RootCause { get; set; }
        public string ActionTaken { get; set; }
        public string Recommendation { get; set; }
        public string SubmittedBy { get; set; }
        public List<string> Tags { get; set; }
        public List<string> Attachments { get; set; }
    }

    public class ApprovalRequest
    {
        public bool Approved { get; set; }
        public string ReviewedBy { get; set; }
        public string Notes { get; set; }
        public bool PromoteToBestPractice { get; set; }
    }

    public class LessonSearchCriteria
    {
        public string Category { get; set; }
        public LessonType? Type { get; set; }
        public string ProjectId { get; set; }
        public List<string> Tags { get; set; }
        public string SearchText { get; set; }
        public bool ApprovedOnly { get; set; }
    }

    public class BestPractice
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public List<string> Benefits { get; set; }
        public string Implementation { get; set; }
        public List<string> Metrics { get; set; }
        public List<string> ApplicableProjectTypes { get; set; }
        public List<string> RelatedPractices { get; set; }
        public string SourceLessonId { get; set; }
        public DateTime CreatedDate { get; set; }
        public string CreatedBy { get; set; }
        public DateTime? LastReviewed { get; set; }
        public BestPracticeStatus Status { get; set; }
        public int AdoptionCount { get; set; }
        public List<AdoptionRecord> AdoptionRecords { get; set; }
    }

    public class BestPracticeRequest
    {
        public string Title { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public List<string> Benefits { get; set; }
        public string Implementation { get; set; }
        public List<string> Metrics { get; set; }
        public List<string> ApplicableProjectTypes { get; set; }
        public List<string> RelatedPractices { get; set; }
        public string CreatedBy { get; set; }
    }

    public class BestPracticeSearchCriteria
    {
        public string Category { get; set; }
        public string ProjectType { get; set; }
        public string SearchText { get; set; }
    }

    public class AdoptionRecord
    {
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public DateTime AdoptedDate { get; set; }
        public string AdoptedBy { get; set; }
        public string Notes { get; set; }
    }

    public class ProjectRecord
    {
        public string Id { get; set; }
        public string ProjectName { get; set; }
        public string ProjectType { get; set; }
        public string Location { get; set; }
        public string Client { get; set; }
        public string DeliveryMethod { get; set; }
        public string ContractType { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? CompletionDate { get; set; }
        public decimal GrossArea { get; set; }
        public int Stories { get; set; }
        public decimal ContractValue { get; set; }
        public decimal FinalCost { get; set; }
        public int ScheduledDuration { get; set; }
        public int ActualDuration { get; set; }
        public DateTime CreatedDate { get; set; }
        public ProjectMetrics Metrics { get; set; }
        public List<string> LessonsLearned { get; set; }
    }

    public class ProjectRecordRequest
    {
        public string ProjectName { get; set; }
        public string ProjectType { get; set; }
        public string Location { get; set; }
        public string Client { get; set; }
        public string DeliveryMethod { get; set; }
        public string ContractType { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? CompletionDate { get; set; }
        public decimal GrossArea { get; set; }
        public int Stories { get; set; }
        public decimal ContractValue { get; set; }
        public decimal FinalCost { get; set; }
        public int ScheduledDuration { get; set; }
        public int ActualDuration { get; set; }
    }

    public class ProjectMetrics
    {
        public decimal CostPerSF { get; set; }
        public decimal CostVariance { get; set; }
        public decimal ScheduleVariance { get; set; }
        public bool OnBudget { get; set; }
        public bool OnSchedule { get; set; }
        public decimal SafetyTRIR { get; set; }
        public decimal QualityDefectRate { get; set; }
        public decimal ChangeOrderPercent { get; set; }
        public decimal ClientSatisfaction { get; set; }
        public int ClashesIdentified { get; set; }
        public int ClashesResolved { get; set; }
        public int RFICount { get; set; }
        public int PunchListItems { get; set; }
    }

    public class ProjectMetricsUpdate
    {
        public decimal? SafetyTRIR { get; set; }
        public decimal? QualityDefectRate { get; set; }
        public decimal? ChangeOrderPercent { get; set; }
        public decimal? ClientSatisfaction { get; set; }
        public int? ClashesIdentified { get; set; }
        public int? ClashesResolved { get; set; }
        public int? RFICount { get; set; }
        public int? PunchListItems { get; set; }
    }

    public class BenchmarkRequest
    {
        public string ProjectId { get; set; }
    }

    public class BenchmarkAnalysis
    {
        public string Id { get; set; }
        public string ProjectId { get; set; }
        public DateTime AnalysisDate { get; set; }
        public int ComparableProjectCount { get; set; }
        public List<MetricComparison> Comparisons { get; set; }
        public List<string> Recommendations { get; set; }
    }

    public class MetricComparison
    {
        public string MetricName { get; set; }
        public decimal ProjectValue { get; set; }
        public string Unit { get; set; }
        public decimal BenchmarkMin { get; set; }
        public decimal BenchmarkMax { get; set; }
        public decimal BenchmarkAverage { get; set; }
        public decimal BenchmarkMedian { get; set; }
        public decimal Percentile { get; set; }
        public string Performance { get; set; }
        public int SampleSize { get; set; }
    }

    public class KnowledgeArticle
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Category { get; set; }
        public string Content { get; set; }
        public string Summary { get; set; }
        public string Author { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? PublishedDate { get; set; }
        public string PublishedBy { get; set; }
        public DateTime? LastModified { get; set; }
        public ArticleStatus Status { get; set; }
        public List<string> Tags { get; set; }
        public List<string> RelatedArticles { get; set; }
        public int Views { get; set; }
        public decimal Rating { get; set; }
    }

    public class ArticleRequest
    {
        public string Title { get; set; }
        public string Category { get; set; }
        public string Content { get; set; }
        public string Summary { get; set; }
        public string Author { get; set; }
        public List<string> Tags { get; set; }
    }

    public class ArticleSearchCriteria
    {
        public string Category { get; set; }
        public List<string> Tags { get; set; }
        public string SearchText { get; set; }
    }

    public class KnowledgeDashboard
    {
        public DateTime GeneratedDate { get; set; }
        public int TotalLessons { get; set; }
        public int ApprovedLessons { get; set; }
        public int PendingLessons { get; set; }
        public int TotalBestPractices { get; set; }
        public int TotalProjectRecords { get; set; }
        public int TotalArticles { get; set; }
        public List<RecentItem> RecentLessons { get; set; }
        public List<RecentItem> TopPractices { get; set; }
        public Dictionary<string, int> CategoryBreakdown { get; set; }
    }

    public class RecentItem
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public DateTime Date { get; set; }
        public string Type { get; set; }
    }

    public class KnowledgeEventArgs : EventArgs
    {
        public string Type { get; set; }
        public string Id { get; set; }
        public string Title { get; set; }
    }

    public enum LessonType { Success, Challenge, Innovation, ProcessImprovement }
    public enum LessonStatus { Draft, Approved, Rejected, Archived }
    public enum BestPracticeStatus { Draft, Active, Retired }
    public enum ArticleStatus { Draft, Published, Archived }

    #endregion
}
