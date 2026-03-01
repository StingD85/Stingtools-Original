// ============================================================================
// StingBIM AI - Model Coordination Engine
// Advanced trade coordination, 4D/5D integration, and change management
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StingBIM.AI.Intelligence.ModelCoordination
{
    /// <summary>
    /// Model Coordination Engine for comprehensive BIM coordination
    /// including trade coordination, 4D scheduling, 5D costing, and change tracking.
    /// </summary>
    public sealed class ModelCoordinationEngine
    {
        private static readonly Lazy<ModelCoordinationEngine> _instance =
            new Lazy<ModelCoordinationEngine>(() => new ModelCoordinationEngine());
        public static ModelCoordinationEngine Instance => _instance.Value;

        private readonly object _lock = new object();
        private readonly Dictionary<string, FederatedModel> _federatedModels = new();
        private readonly Dictionary<string, CoordinationSession> _sessions = new();
        private readonly Dictionary<string, ScheduleLink> _scheduleLinks = new();
        private readonly Dictionary<string, CostLink> _costLinks = new();
        private readonly Dictionary<string, ModelChange> _changes = new();
        private readonly List<TradeCoordinationRule> _coordinationRules = new();

        public event EventHandler<CoordinationEventArgs> CoordinationAlert;
        public event EventHandler<ChangeEventArgs> ModelChanged;

        private ModelCoordinationEngine()
        {
            InitializeCoordinationRules();
        }

        #region Initialization

        private void InitializeCoordinationRules()
        {
            _coordinationRules.AddRange(new[]
            {
                // Clearance rules
                new TradeCoordinationRule
                {
                    RuleId = "CR001",
                    Name = "Duct to Structure Clearance",
                    Trade1 = "Mechanical",
                    Trade2 = "Structural",
                    RuleType = CoordinationRuleType.Clearance,
                    MinClearance = 50, // mm
                    Description = "Minimum clearance between ductwork and structural elements"
                },
                new TradeCoordinationRule
                {
                    RuleId = "CR002",
                    Name = "Pipe to Structure Clearance",
                    Trade1 = "Plumbing",
                    Trade2 = "Structural",
                    RuleType = CoordinationRuleType.Clearance,
                    MinClearance = 25,
                    Description = "Minimum clearance between piping and structural elements"
                },
                new TradeCoordinationRule
                {
                    RuleId = "CR003",
                    Name = "Electrical to HVAC Clearance",
                    Trade1 = "Electrical",
                    Trade2 = "Mechanical",
                    RuleType = CoordinationRuleType.Clearance,
                    MinClearance = 150,
                    Description = "Minimum clearance for electrical maintenance access"
                },
                new TradeCoordinationRule
                {
                    RuleId = "CR004",
                    Name = "Sprinkler Head Clearance",
                    Trade1 = "Fire Protection",
                    Trade2 = "All",
                    RuleType = CoordinationRuleType.Clearance,
                    MinClearance = 450,
                    Description = "Minimum clearance below sprinkler heads for coverage"
                },

                // Priority rules
                new TradeCoordinationRule
                {
                    RuleId = "PR001",
                    Name = "Gravity Drainage Priority",
                    Trade1 = "Plumbing",
                    Trade2 = "All",
                    RuleType = CoordinationRuleType.Priority,
                    Description = "Gravity drainage takes priority - cannot be rerouted easily"
                },
                new TradeCoordinationRule
                {
                    RuleId = "PR002",
                    Name = "Structural Priority",
                    Trade1 = "Structural",
                    Trade2 = "All",
                    RuleType = CoordinationRuleType.Priority,
                    Description = "Structural elements take priority over MEP"
                },
                new TradeCoordinationRule
                {
                    RuleId = "PR003",
                    Name = "Large Duct Priority",
                    Trade1 = "Mechanical",
                    Trade2 = "Electrical",
                    RuleType = CoordinationRuleType.Priority,
                    Description = "Large ducts harder to reroute than conduit"
                },

                // Access rules
                new TradeCoordinationRule
                {
                    RuleId = "AR001",
                    Name = "Valve Access",
                    Trade1 = "Plumbing",
                    Trade2 = "All",
                    RuleType = CoordinationRuleType.Access,
                    MinClearance = 600,
                    Description = "Valves require maintenance access clearance"
                },
                new TradeCoordinationRule
                {
                    RuleId = "AR002",
                    Name = "Equipment Access",
                    Trade1 = "Mechanical",
                    Trade2 = "All",
                    RuleType = CoordinationRuleType.Access,
                    MinClearance = 900,
                    Description = "Mechanical equipment requires service access"
                },
                new TradeCoordinationRule
                {
                    RuleId = "AR003",
                    Name = "Panel Access",
                    Trade1 = "Electrical",
                    Trade2 = "All",
                    RuleType = CoordinationRuleType.Access,
                    MinClearance = 900,
                    Description = "Electrical panels require NEC clearance"
                }
            });
        }

        #endregion

        #region Federated Model Management

        /// <summary>
        /// Create a federated model for coordination
        /// </summary>
        public FederatedModel CreateFederatedModel(FederatedModelRequest request)
        {
            var fedModel = new FederatedModel
            {
                FederatedModelId = Guid.NewGuid().ToString(),
                ProjectId = request.ProjectId,
                Name = request.Name,
                Description = request.Description,
                CreatedDate = DateTime.UtcNow,
                LinkedModels = request.LinkedModels ?? new List<LinkedModel>(),
                CoordinationZones = request.CoordinationZones ?? new List<CoordinationZone>(),
                Status = FederatedModelStatus.Active,
                LastUpdated = DateTime.UtcNow
            };

            lock (_lock)
            {
                _federatedModels[fedModel.FederatedModelId] = fedModel;
            }

            return fedModel;
        }

        /// <summary>
        /// Add a linked model to the federation
        /// </summary>
        public void AddLinkedModel(string federatedModelId, LinkedModel model)
        {
            lock (_lock)
            {
                if (_federatedModels.TryGetValue(federatedModelId, out var fedModel))
                {
                    model.LinkedModelId = Guid.NewGuid().ToString();
                    model.AddedDate = DateTime.UtcNow;
                    fedModel.LinkedModels.Add(model);
                    fedModel.LastUpdated = DateTime.UtcNow;
                }
            }
        }

        /// <summary>
        /// Update linked model version
        /// </summary>
        public void UpdateLinkedModelVersion(string federatedModelId, string linkedModelId,
            string newVersion, string newPath)
        {
            lock (_lock)
            {
                if (_federatedModels.TryGetValue(federatedModelId, out var fedModel))
                {
                    var linkedModel = fedModel.LinkedModels.FirstOrDefault(m => m.LinkedModelId == linkedModelId);
                    if (linkedModel != null)
                    {
                        linkedModel.PreviousVersion = linkedModel.Version;
                        linkedModel.Version = newVersion;
                        linkedModel.FilePath = newPath;
                        linkedModel.LastUpdated = DateTime.UtcNow;
                        fedModel.LastUpdated = DateTime.UtcNow;

                        // Track change
                        TrackModelChange(federatedModelId, linkedModelId, "Version Update",
                            $"Updated from {linkedModel.PreviousVersion} to {newVersion}");
                    }
                }
            }
        }

        #endregion

        #region Coordination Sessions

        /// <summary>
        /// Start a coordination session
        /// </summary>
        public CoordinationSession StartCoordinationSession(CoordinationSessionRequest request)
        {
            var session = new CoordinationSession
            {
                SessionId = Guid.NewGuid().ToString(),
                FederatedModelId = request.FederatedModelId,
                Name = request.Name,
                SessionType = request.SessionType,
                StartTime = DateTime.UtcNow,
                Attendees = request.Attendees ?? new List<SessionAttendee>(),
                ClashTests = new List<ClashTest>(),
                Issues = new List<CoordinationIssue>(),
                Decisions = new List<CoordinationDecision>(),
                ActionItems = new List<CoordinationActionItem>(),
                Status = SessionStatus.InProgress
            };

            lock (_lock)
            {
                _sessions[session.SessionId] = session;
            }

            return session;
        }

        /// <summary>
        /// Run clash test in session
        /// </summary>
        public ClashTestResult RunClashTest(string sessionId, ClashTestRequest request)
        {
            var result = new ClashTestResult
            {
                TestId = Guid.NewGuid().ToString(),
                TestName = request.TestName,
                Selection1 = request.Selection1,
                Selection2 = request.Selection2,
                Tolerance = request.Tolerance,
                RunDate = DateTime.UtcNow,
                Clashes = new List<Clash>()
            };

            // Simulate clash detection based on request
            var clashCount = SimulateClashDetection(request);
            for (int i = 0; i < clashCount; i++)
            {
                result.Clashes.Add(GenerateSimulatedClash(i, request));
            }

            // Categorize clashes
            result.Summary = new ClashSummary
            {
                TotalClashes = result.Clashes.Count,
                NewClashes = result.Clashes.Count(c => c.Status == ClashStatus.New),
                ActiveClashes = result.Clashes.Count(c => c.Status == ClashStatus.Active),
                ResolvedClashes = result.Clashes.Count(c => c.Status == ClashStatus.Resolved),
                ByType = result.Clashes.GroupBy(c => c.ClashType).ToDictionary(g => g.Key, g => g.Count()),
                BySeverity = result.Clashes.GroupBy(c => c.Severity).ToDictionary(g => g.Key, g => g.Count())
            };

            // Add to session
            lock (_lock)
            {
                if (_sessions.TryGetValue(sessionId, out var session))
                {
                    session.ClashTests.Add(new ClashTest
                    {
                        TestId = result.TestId,
                        TestName = request.TestName,
                        ClashCount = result.Clashes.Count
                    });
                }
            }

            return result;
        }

        private int SimulateClashDetection(ClashTestRequest request)
        {
            // Simulate based on selections
            var baseCount = new Random().Next(10, 100);

            // More clashes for MEP vs MEP
            if (request.Selection1.Contains("MEP") && request.Selection2.Contains("MEP"))
                baseCount += 50;

            return baseCount;
        }

        private Clash GenerateSimulatedClash(int index, ClashTestRequest request)
        {
            var severities = new[] { ClashSeverity.Critical, ClashSeverity.Major, ClashSeverity.Minor };
            var types = new[] { ClashType.Hard, ClashType.Soft, ClashType.Clearance };

            return new Clash
            {
                ClashId = $"CLH-{index + 1:D5}",
                Name = $"Clash {index + 1}",
                ClashType = types[index % types.Length],
                Severity = severities[index % severities.Length],
                Element1 = new ClashElement
                {
                    ElementId = Guid.NewGuid().ToString(),
                    Category = request.Selection1,
                    ModelName = "Model A"
                },
                Element2 = new ClashElement
                {
                    ElementId = Guid.NewGuid().ToString(),
                    Category = request.Selection2,
                    ModelName = "Model B"
                },
                Location = new ClashLocation
                {
                    X = 100 + index * 10,
                    Y = 50 + index * 5,
                    Z = 3.0,
                    Level = $"Level {(index % 5) + 1}",
                    Zone = $"Zone {(char)('A' + (index % 4))}"
                },
                Status = ClashStatus.New,
                DetectedDate = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Log a coordination decision
        /// </summary>
        public void LogDecision(string sessionId, CoordinationDecision decision)
        {
            decision.DecisionId = Guid.NewGuid().ToString();
            decision.DecisionDate = DateTime.UtcNow;

            lock (_lock)
            {
                if (_sessions.TryGetValue(sessionId, out var session))
                {
                    session.Decisions.Add(decision);
                }
            }
        }

        /// <summary>
        /// Create action item from coordination
        /// </summary>
        public CoordinationActionItem CreateActionItem(string sessionId, ActionItemRequest request)
        {
            var actionItem = new CoordinationActionItem
            {
                ActionItemId = Guid.NewGuid().ToString(),
                SessionId = sessionId,
                Description = request.Description,
                AssignedTo = request.AssignedTo,
                AssignedDiscipline = request.AssignedDiscipline,
                DueDate = request.DueDate,
                Priority = request.Priority,
                RelatedClashIds = request.RelatedClashIds,
                Status = ActionItemStatus.Open,
                CreatedDate = DateTime.UtcNow
            };

            lock (_lock)
            {
                if (_sessions.TryGetValue(sessionId, out var session))
                {
                    session.ActionItems.Add(actionItem);
                }
            }

            return actionItem;
        }

        /// <summary>
        /// End coordination session with summary
        /// </summary>
        public CoordinationSessionSummary EndSession(string sessionId, string notes = null)
        {
            lock (_lock)
            {
                if (!_sessions.TryGetValue(sessionId, out var session))
                    throw new KeyNotFoundException($"Session {sessionId} not found");

                session.EndTime = DateTime.UtcNow;
                session.Status = SessionStatus.Completed;
                session.Notes = notes;

                return new CoordinationSessionSummary
                {
                    SessionId = sessionId,
                    Duration = session.EndTime.Value - session.StartTime,
                    ClashTestsRun = session.ClashTests.Count,
                    TotalClashesFound = session.ClashTests.Sum(t => t.ClashCount),
                    IssuesRaised = session.Issues.Count,
                    DecisionsMade = session.Decisions.Count,
                    ActionItemsCreated = session.ActionItems.Count,
                    AttendeeCount = session.Attendees.Count
                };
            }
        }

        #endregion

        #region 4D Schedule Integration

        /// <summary>
        /// Link schedule to model elements
        /// </summary>
        public ScheduleLink CreateScheduleLink(ScheduleLinkRequest request)
        {
            var link = new ScheduleLink
            {
                LinkId = Guid.NewGuid().ToString(),
                ProjectId = request.ProjectId,
                ScheduleName = request.ScheduleName,
                ScheduleSource = request.ScheduleSource,
                CreatedDate = DateTime.UtcNow,
                Activities = request.Activities ?? new List<ScheduleActivity>(),
                ElementMappings = new List<ElementScheduleMapping>()
            };

            lock (_lock)
            {
                _scheduleLinks[link.LinkId] = link;
            }

            return link;
        }

        /// <summary>
        /// Map model elements to schedule activities
        /// </summary>
        public void MapElementsToActivity(string linkId, ElementScheduleMapping mapping)
        {
            lock (_lock)
            {
                if (_scheduleLinks.TryGetValue(linkId, out var link))
                {
                    mapping.MappingId = Guid.NewGuid().ToString();
                    mapping.MappedDate = DateTime.UtcNow;
                    link.ElementMappings.Add(mapping);
                }
            }
        }

        /// <summary>
        /// Get 4D sequence for a date range
        /// </summary>
        public Sequence4D Get4DSequence(string linkId, DateTime startDate, DateTime endDate)
        {
            if (!_scheduleLinks.TryGetValue(linkId, out var link))
                throw new KeyNotFoundException($"Schedule link {linkId} not found");

            var sequence = new Sequence4D
            {
                LinkId = linkId,
                StartDate = startDate,
                EndDate = endDate,
                Frames = new List<SequenceFrame>()
            };

            // Generate frames for each day
            var currentDate = startDate;
            while (currentDate <= endDate)
            {
                var activeActivities = link.Activities
                    .Where(a => a.StartDate <= currentDate && a.EndDate >= currentDate)
                    .ToList();

                var frame = new SequenceFrame
                {
                    FrameDate = currentDate,
                    VisibleElements = new List<string>(),
                    InProgressElements = new List<string>(),
                    CompletedElements = new List<string>()
                };

                foreach (var activity in link.Activities)
                {
                    var mappings = link.ElementMappings
                        .Where(m => m.ActivityId == activity.ActivityId)
                        .SelectMany(m => m.ElementIds)
                        .ToList();

                    if (activity.EndDate < currentDate)
                    {
                        frame.CompletedElements.AddRange(mappings);
                    }
                    else if (activity.StartDate <= currentDate && activity.EndDate >= currentDate)
                    {
                        frame.InProgressElements.AddRange(mappings);
                    }

                    if (activity.StartDate <= currentDate)
                    {
                        frame.VisibleElements.AddRange(mappings);
                    }
                }

                sequence.Frames.Add(frame);
                currentDate = currentDate.AddDays(1);
            }

            return sequence;
        }

        /// <summary>
        /// Analyze schedule conflicts
        /// </summary>
        public List<ScheduleConflict> AnalyzeScheduleConflicts(string linkId)
        {
            if (!_scheduleLinks.TryGetValue(linkId, out var link))
                throw new KeyNotFoundException($"Schedule link {linkId} not found");

            var conflicts = new List<ScheduleConflict>();

            // Find spatial conflicts in concurrent activities
            var activitiesByDate = link.Activities
                .SelectMany(a => Enumerable.Range(0, (a.EndDate - a.StartDate).Days + 1)
                    .Select(d => new { Date = a.StartDate.AddDays(d), Activity = a }))
                .GroupBy(x => x.Date)
                .Where(g => g.Count() > 1);

            foreach (var group in activitiesByDate)
            {
                var activities = group.Select(g => g.Activity).ToList();

                // Check for same-zone conflicts
                for (int i = 0; i < activities.Count; i++)
                {
                    for (int j = i + 1; j < activities.Count; j++)
                    {
                        if (activities[i].Zone == activities[j].Zone)
                        {
                            conflicts.Add(new ScheduleConflict
                            {
                                ConflictId = Guid.NewGuid().ToString(),
                                Type = ScheduleConflictType.SpatialOverlap,
                                Date = group.Key,
                                Activity1 = activities[i].Name,
                                Activity2 = activities[j].Name,
                                Zone = activities[i].Zone,
                                Description = $"Activities '{activities[i].Name}' and '{activities[j].Name}' scheduled in same zone",
                                Severity = ConflictSeverity.Warning
                            });
                        }
                    }
                }
            }

            return conflicts;
        }

        #endregion

        #region 5D Cost Integration

        /// <summary>
        /// Create cost link for 5D
        /// </summary>
        public CostLink CreateCostLink(CostLinkRequest request)
        {
            var link = new CostLink
            {
                LinkId = Guid.NewGuid().ToString(),
                ProjectId = request.ProjectId,
                CostDatabaseName = request.CostDatabaseName,
                CreatedDate = DateTime.UtcNow,
                ElementCosts = new List<ElementCost>(),
                CostCategories = request.CostCategories ?? GetDefaultCostCategories()
            };

            lock (_lock)
            {
                _costLinks[link.LinkId] = link;
            }

            return link;
        }

        private List<CostCategory> GetDefaultCostCategories()
        {
            return new List<CostCategory>
            {
                new CostCategory { CategoryId = "01", Name = "General Requirements", Division = "01" },
                new CostCategory { CategoryId = "03", Name = "Concrete", Division = "03" },
                new CostCategory { CategoryId = "04", Name = "Masonry", Division = "04" },
                new CostCategory { CategoryId = "05", Name = "Metals", Division = "05" },
                new CostCategory { CategoryId = "06", Name = "Wood and Plastics", Division = "06" },
                new CostCategory { CategoryId = "07", Name = "Thermal and Moisture Protection", Division = "07" },
                new CostCategory { CategoryId = "08", Name = "Doors and Windows", Division = "08" },
                new CostCategory { CategoryId = "09", Name = "Finishes", Division = "09" },
                new CostCategory { CategoryId = "21", Name = "Fire Suppression", Division = "21" },
                new CostCategory { CategoryId = "22", Name = "Plumbing", Division = "22" },
                new CostCategory { CategoryId = "23", Name = "HVAC", Division = "23" },
                new CostCategory { CategoryId = "26", Name = "Electrical", Division = "26" }
            };
        }

        /// <summary>
        /// Assign costs to elements
        /// </summary>
        public void AssignElementCost(string linkId, ElementCost cost)
        {
            lock (_lock)
            {
                if (_costLinks.TryGetValue(linkId, out var link))
                {
                    cost.CostId = Guid.NewGuid().ToString();
                    cost.AssignedDate = DateTime.UtcNow;
                    link.ElementCosts.Add(cost);
                }
            }
        }

        /// <summary>
        /// Generate cost report from model
        /// </summary>
        public CostReport GenerateCostReport(string linkId, CostReportRequest request)
        {
            if (!_costLinks.TryGetValue(linkId, out var link))
                throw new KeyNotFoundException($"Cost link {linkId} not found");

            var report = new CostReport
            {
                ReportId = Guid.NewGuid().ToString(),
                LinkId = linkId,
                GeneratedDate = DateTime.UtcNow,
                ReportType = request.ReportType,
                LineItems = new List<CostLineItem>(),
                Summary = new CostSummary()
            };

            // Group costs based on report type
            var groupedCosts = request.ReportType switch
            {
                CostReportType.ByDivision => link.ElementCosts.GroupBy(c => c.Division),
                CostReportType.ByLevel => link.ElementCosts.GroupBy(c => c.Level),
                CostReportType.BySystem => link.ElementCosts.GroupBy(c => c.System),
                _ => link.ElementCosts.GroupBy(c => c.Category)
            };

            foreach (var group in groupedCosts)
            {
                report.LineItems.Add(new CostLineItem
                {
                    Category = group.Key,
                    ElementCount = group.Count(),
                    TotalQuantity = group.Sum(c => c.Quantity),
                    Unit = group.First().Unit,
                    UnitCost = group.Average(c => c.UnitCost),
                    TotalCost = group.Sum(c => c.TotalCost),
                    LaborCost = group.Sum(c => c.LaborCost),
                    MaterialCost = group.Sum(c => c.MaterialCost)
                });
            }

            // Calculate summary
            report.Summary = new CostSummary
            {
                TotalCost = report.LineItems.Sum(l => l.TotalCost),
                LaborTotal = report.LineItems.Sum(l => l.LaborCost),
                MaterialTotal = report.LineItems.Sum(l => l.MaterialCost),
                LineItemCount = report.LineItems.Count,
                ElementCount = link.ElementCosts.Count
            };

            return report;
        }

        /// <summary>
        /// Analyze cost impact of change
        /// </summary>
        public CostImpactAnalysis AnalyzeCostImpact(string linkId, ChangeImpactRequest request)
        {
            if (!_costLinks.TryGetValue(linkId, out var link))
                throw new KeyNotFoundException($"Cost link {linkId} not found");

            var analysis = new CostImpactAnalysis
            {
                AnalysisId = Guid.NewGuid().ToString(),
                ChangeDescription = request.ChangeDescription,
                AnalysisDate = DateTime.UtcNow,
                AffectedElements = new List<AffectedElement>(),
                Summary = new CostImpactSummary()
            };

            // Analyze affected elements
            foreach (var elementId in request.AffectedElementIds)
            {
                var existingCost = link.ElementCosts.FirstOrDefault(c => c.ElementId == elementId);
                if (existingCost != null)
                {
                    var affected = new AffectedElement
                    {
                        ElementId = elementId,
                        CurrentCost = existingCost.TotalCost,
                        NewCost = existingCost.TotalCost * (1m + (decimal)(request.CostChangePercent / 100)),
                        CostDifference = existingCost.TotalCost * (decimal)(request.CostChangePercent / 100),
                        ChangeType = request.CostChangePercent > 0 ? "Addition" : "Reduction"
                    };
                    analysis.AffectedElements.Add(affected);
                }
            }

            analysis.Summary = new CostImpactSummary
            {
                TotalCurrentCost = analysis.AffectedElements.Sum(a => a.CurrentCost),
                TotalNewCost = analysis.AffectedElements.Sum(a => a.NewCost),
                TotalDifference = analysis.AffectedElements.Sum(a => a.CostDifference),
                PercentChange = request.CostChangePercent,
                AffectedElementCount = analysis.AffectedElements.Count
            };

            return analysis;
        }

        #endregion

        #region Change Tracking

        /// <summary>
        /// Track a model change
        /// </summary>
        public ModelChange TrackModelChange(string federatedModelId, string linkedModelId,
            string changeType, string description)
        {
            var change = new ModelChange
            {
                ChangeId = Guid.NewGuid().ToString(),
                FederatedModelId = federatedModelId,
                LinkedModelId = linkedModelId,
                ChangeType = changeType,
                Description = description,
                ChangeDate = DateTime.UtcNow,
                Status = ChangeStatus.Pending
            };

            lock (_lock)
            {
                _changes[change.ChangeId] = change;
            }

            ModelChanged?.Invoke(this, new ChangeEventArgs
            {
                ChangeId = change.ChangeId,
                ChangeType = changeType,
                Description = description
            });

            return change;
        }

        /// <summary>
        /// Get change history
        /// </summary>
        public List<ModelChange> GetChangeHistory(string federatedModelId, DateTime? since = null)
        {
            lock (_lock)
            {
                var query = _changes.Values.Where(c => c.FederatedModelId == federatedModelId);

                if (since.HasValue)
                    query = query.Where(c => c.ChangeDate >= since.Value);

                return query.OrderByDescending(c => c.ChangeDate).ToList();
            }
        }

        /// <summary>
        /// Compare model versions
        /// </summary>
        public ModelComparison CompareVersions(string federatedModelId,
            string version1, string version2, ModelComparisonData data)
        {
            var comparison = new ModelComparison
            {
                ComparisonId = Guid.NewGuid().ToString(),
                FederatedModelId = federatedModelId,
                Version1 = version1,
                Version2 = version2,
                ComparisonDate = DateTime.UtcNow,
                AddedElements = data?.AddedElements ?? new List<string>(),
                DeletedElements = data?.DeletedElements ?? new List<string>(),
                ModifiedElements = data?.ModifiedElements ?? new List<ModifiedElement>()
            };

            comparison.Summary = new ComparisonSummary
            {
                TotalAdded = comparison.AddedElements.Count,
                TotalDeleted = comparison.DeletedElements.Count,
                TotalModified = comparison.ModifiedElements.Count,
                NetChange = comparison.AddedElements.Count - comparison.DeletedElements.Count
            };

            return comparison;
        }

        #endregion

        #region Coordination Rules

        /// <summary>
        /// Get applicable coordination rules
        /// </summary>
        public List<TradeCoordinationRule> GetCoordinationRules(string trade1 = null, string trade2 = null)
        {
            var rules = _coordinationRules.AsEnumerable();

            if (!string.IsNullOrEmpty(trade1))
                rules = rules.Where(r => r.Trade1 == trade1 || r.Trade2 == trade1 || r.Trade1 == "All" || r.Trade2 == "All");

            if (!string.IsNullOrEmpty(trade2))
                rules = rules.Where(r => r.Trade1 == trade2 || r.Trade2 == trade2 || r.Trade1 == "All" || r.Trade2 == "All");

            return rules.ToList();
        }

        /// <summary>
        /// Check coordination rules
        /// </summary>
        public List<RuleViolation> CheckCoordinationRules(RuleCheckRequest request)
        {
            var violations = new List<RuleViolation>();

            foreach (var rule in _coordinationRules)
            {
                if (IsRuleApplicable(rule, request.Trade1, request.Trade2))
                {
                    // Check clearance rules
                    if (rule.RuleType == CoordinationRuleType.Clearance &&
                        request.ActualClearance < rule.MinClearance)
                    {
                        violations.Add(new RuleViolation
                        {
                            RuleId = rule.RuleId,
                            RuleName = rule.Name,
                            Description = $"Clearance {request.ActualClearance}mm less than required {rule.MinClearance}mm",
                            Severity = ViolationSeverity.Error,
                            Location = request.Location
                        });
                    }
                }
            }

            return violations;
        }

        private bool IsRuleApplicable(TradeCoordinationRule rule, string trade1, string trade2)
        {
            if (rule.Trade1 == "All" || rule.Trade2 == "All")
                return true;

            return (rule.Trade1 == trade1 && rule.Trade2 == trade2) ||
                   (rule.Trade1 == trade2 && rule.Trade2 == trade1);
        }

        #endregion

        #region Queries

        public FederatedModel GetFederatedModel(string id)
        {
            lock (_lock)
            {
                return _federatedModels.TryGetValue(id, out var model) ? model : null;
            }
        }

        public CoordinationSession GetSession(string id)
        {
            lock (_lock)
            {
                return _sessions.TryGetValue(id, out var session) ? session : null;
            }
        }

        public ScheduleLink GetScheduleLink(string id)
        {
            lock (_lock)
            {
                return _scheduleLinks.TryGetValue(id, out var link) ? link : null;
            }
        }

        public CostLink GetCostLink(string id)
        {
            lock (_lock)
            {
                return _costLinks.TryGetValue(id, out var link) ? link : null;
            }
        }

        #endregion
    }

    #region Data Models

    public class FederatedModel
    {
        public string FederatedModelId { get; set; }
        public string ProjectId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime LastUpdated { get; set; }
        public List<LinkedModel> LinkedModels { get; set; }
        public List<CoordinationZone> CoordinationZones { get; set; }
        public FederatedModelStatus Status { get; set; }
    }

    public class FederatedModelRequest
    {
        public string ProjectId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public List<LinkedModel> LinkedModels { get; set; }
        public List<CoordinationZone> CoordinationZones { get; set; }
    }

    public class LinkedModel
    {
        public string LinkedModelId { get; set; }
        public string Name { get; set; }
        public string Discipline { get; set; }
        public string FilePath { get; set; }
        public string Version { get; set; }
        public string PreviousVersion { get; set; }
        public DateTime AddedDate { get; set; }
        public DateTime LastUpdated { get; set; }
        public string ResponsibleParty { get; set; }
    }

    public class CoordinationZone
    {
        public string ZoneId { get; set; }
        public string Name { get; set; }
        public string Level { get; set; }
        public BoundingBox Bounds { get; set; }
    }

    public class BoundingBox
    {
        public double MinX { get; set; }
        public double MinY { get; set; }
        public double MinZ { get; set; }
        public double MaxX { get; set; }
        public double MaxY { get; set; }
        public double MaxZ { get; set; }
    }

    public class CoordinationSession
    {
        public string SessionId { get; set; }
        public string FederatedModelId { get; set; }
        public string Name { get; set; }
        public CoordinationSessionType SessionType { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public List<SessionAttendee> Attendees { get; set; }
        public List<ClashTest> ClashTests { get; set; }
        public List<CoordinationIssue> Issues { get; set; }
        public List<CoordinationDecision> Decisions { get; set; }
        public List<CoordinationActionItem> ActionItems { get; set; }
        public SessionStatus Status { get; set; }
        public string Notes { get; set; }
    }

    public class CoordinationSessionRequest
    {
        public string FederatedModelId { get; set; }
        public string Name { get; set; }
        public CoordinationSessionType SessionType { get; set; }
        public List<SessionAttendee> Attendees { get; set; }
    }

    public class SessionAttendee
    {
        public string Name { get; set; }
        public string Organization { get; set; }
        public string Role { get; set; }
        public string Discipline { get; set; }
    }

    public class ClashTest
    {
        public string TestId { get; set; }
        public string TestName { get; set; }
        public int ClashCount { get; set; }
    }

    public class ClashTestRequest
    {
        public string TestName { get; set; }
        public string Selection1 { get; set; }
        public string Selection2 { get; set; }
        public double Tolerance { get; set; }
    }

    public class ClashTestResult
    {
        public string TestId { get; set; }
        public string TestName { get; set; }
        public string Selection1 { get; set; }
        public string Selection2 { get; set; }
        public double Tolerance { get; set; }
        public DateTime RunDate { get; set; }
        public List<Clash> Clashes { get; set; }
        public ClashSummary Summary { get; set; }
    }

    public class Clash
    {
        public string ClashId { get; set; }
        public string Name { get; set; }
        public ClashType ClashType { get; set; }
        public ClashSeverity Severity { get; set; }
        public ClashElement Element1 { get; set; }
        public ClashElement Element2 { get; set; }
        public ClashLocation Location { get; set; }
        public ClashStatus Status { get; set; }
        public DateTime DetectedDate { get; set; }
        public string AssignedTo { get; set; }
        public string Resolution { get; set; }
    }

    public class ClashElement
    {
        public string ElementId { get; set; }
        public string Category { get; set; }
        public string ModelName { get; set; }
    }

    public class ClashLocation
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public string Level { get; set; }
        public string Zone { get; set; }
    }

    public class ClashSummary
    {
        public int TotalClashes { get; set; }
        public int NewClashes { get; set; }
        public int ActiveClashes { get; set; }
        public int ResolvedClashes { get; set; }
        public Dictionary<ClashType, int> ByType { get; set; }
        public Dictionary<ClashSeverity, int> BySeverity { get; set; }
    }

    public class CoordinationIssue
    {
        public string IssueId { get; set; }
        public string Description { get; set; }
        public string RaisedBy { get; set; }
        public DateTime RaisedDate { get; set; }
        public IssueStatus Status { get; set; }
    }

    public class CoordinationDecision
    {
        public string DecisionId { get; set; }
        public string Description { get; set; }
        public string DecisionMaker { get; set; }
        public DateTime DecisionDate { get; set; }
        public string Rationale { get; set; }
        public List<string> AffectedDisciplines { get; set; }
    }

    public class CoordinationActionItem
    {
        public string ActionItemId { get; set; }
        public string SessionId { get; set; }
        public string Description { get; set; }
        public string AssignedTo { get; set; }
        public string AssignedDiscipline { get; set; }
        public DateTime DueDate { get; set; }
        public ActionItemPriority Priority { get; set; }
        public List<string> RelatedClashIds { get; set; }
        public ActionItemStatus Status { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class ActionItemRequest
    {
        public string Description { get; set; }
        public string AssignedTo { get; set; }
        public string AssignedDiscipline { get; set; }
        public DateTime DueDate { get; set; }
        public ActionItemPriority Priority { get; set; }
        public List<string> RelatedClashIds { get; set; }
    }

    public class CoordinationSessionSummary
    {
        public string SessionId { get; set; }
        public TimeSpan Duration { get; set; }
        public int ClashTestsRun { get; set; }
        public int TotalClashesFound { get; set; }
        public int IssuesRaised { get; set; }
        public int DecisionsMade { get; set; }
        public int ActionItemsCreated { get; set; }
        public int AttendeeCount { get; set; }
    }

    public class ScheduleLink
    {
        public string LinkId { get; set; }
        public string ProjectId { get; set; }
        public string ScheduleName { get; set; }
        public string ScheduleSource { get; set; }
        public DateTime CreatedDate { get; set; }
        public List<ScheduleActivity> Activities { get; set; }
        public List<ElementScheduleMapping> ElementMappings { get; set; }
    }

    public class ScheduleLinkRequest
    {
        public string ProjectId { get; set; }
        public string ScheduleName { get; set; }
        public string ScheduleSource { get; set; }
        public List<ScheduleActivity> Activities { get; set; }
    }

    public class ScheduleActivity
    {
        public string ActivityId { get; set; }
        public string Name { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Zone { get; set; }
        public string Level { get; set; }
        public string Trade { get; set; }
    }

    public class ElementScheduleMapping
    {
        public string MappingId { get; set; }
        public string ActivityId { get; set; }
        public List<string> ElementIds { get; set; }
        public DateTime MappedDate { get; set; }
    }

    public class Sequence4D
    {
        public string LinkId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public List<SequenceFrame> Frames { get; set; }
    }

    public class SequenceFrame
    {
        public DateTime FrameDate { get; set; }
        public List<string> VisibleElements { get; set; }
        public List<string> InProgressElements { get; set; }
        public List<string> CompletedElements { get; set; }
    }

    public class ScheduleConflict
    {
        public string ConflictId { get; set; }
        public ScheduleConflictType Type { get; set; }
        public DateTime Date { get; set; }
        public string Activity1 { get; set; }
        public string Activity2 { get; set; }
        public string Zone { get; set; }
        public string Description { get; set; }
        public ConflictSeverity Severity { get; set; }
    }

    public class CostLink
    {
        public string LinkId { get; set; }
        public string ProjectId { get; set; }
        public string CostDatabaseName { get; set; }
        public DateTime CreatedDate { get; set; }
        public List<ElementCost> ElementCosts { get; set; }
        public List<CostCategory> CostCategories { get; set; }
    }

    public class CostLinkRequest
    {
        public string ProjectId { get; set; }
        public string CostDatabaseName { get; set; }
        public List<CostCategory> CostCategories { get; set; }
    }

    public class CostCategory
    {
        public string CategoryId { get; set; }
        public string Name { get; set; }
        public string Division { get; set; }
    }

    public class ElementCost
    {
        public string CostId { get; set; }
        public string ElementId { get; set; }
        public string Category { get; set; }
        public string Division { get; set; }
        public string Level { get; set; }
        public string System { get; set; }
        public double Quantity { get; set; }
        public string Unit { get; set; }
        public decimal UnitCost { get; set; }
        public decimal TotalCost { get; set; }
        public decimal LaborCost { get; set; }
        public decimal MaterialCost { get; set; }
        public DateTime AssignedDate { get; set; }
    }

    public class CostReport
    {
        public string ReportId { get; set; }
        public string LinkId { get; set; }
        public DateTime GeneratedDate { get; set; }
        public CostReportType ReportType { get; set; }
        public List<CostLineItem> LineItems { get; set; }
        public CostSummary Summary { get; set; }
    }

    public class CostReportRequest
    {
        public CostReportType ReportType { get; set; }
    }

    public class CostLineItem
    {
        public string Category { get; set; }
        public int ElementCount { get; set; }
        public double TotalQuantity { get; set; }
        public string Unit { get; set; }
        public decimal UnitCost { get; set; }
        public decimal TotalCost { get; set; }
        public decimal LaborCost { get; set; }
        public decimal MaterialCost { get; set; }
    }

    public class CostSummary
    {
        public decimal TotalCost { get; set; }
        public decimal LaborTotal { get; set; }
        public decimal MaterialTotal { get; set; }
        public int LineItemCount { get; set; }
        public int ElementCount { get; set; }
    }

    public class ChangeImpactRequest
    {
        public string ChangeDescription { get; set; }
        public List<string> AffectedElementIds { get; set; }
        public double CostChangePercent { get; set; }
    }

    public class CostImpactAnalysis
    {
        public string AnalysisId { get; set; }
        public string ChangeDescription { get; set; }
        public DateTime AnalysisDate { get; set; }
        public List<AffectedElement> AffectedElements { get; set; }
        public CostImpactSummary Summary { get; set; }
    }

    public class AffectedElement
    {
        public string ElementId { get; set; }
        public decimal CurrentCost { get; set; }
        public decimal NewCost { get; set; }
        public decimal CostDifference { get; set; }
        public string ChangeType { get; set; }
    }

    public class CostImpactSummary
    {
        public decimal TotalCurrentCost { get; set; }
        public decimal TotalNewCost { get; set; }
        public decimal TotalDifference { get; set; }
        public double PercentChange { get; set; }
        public int AffectedElementCount { get; set; }
    }

    public class ModelChange
    {
        public string ChangeId { get; set; }
        public string FederatedModelId { get; set; }
        public string LinkedModelId { get; set; }
        public string ChangeType { get; set; }
        public string Description { get; set; }
        public DateTime ChangeDate { get; set; }
        public ChangeStatus Status { get; set; }
    }

    public class ModelComparisonData
    {
        public List<string> AddedElements { get; set; }
        public List<string> DeletedElements { get; set; }
        public List<ModifiedElement> ModifiedElements { get; set; }
    }

    public class ModelComparison
    {
        public string ComparisonId { get; set; }
        public string FederatedModelId { get; set; }
        public string Version1 { get; set; }
        public string Version2 { get; set; }
        public DateTime ComparisonDate { get; set; }
        public List<string> AddedElements { get; set; }
        public List<string> DeletedElements { get; set; }
        public List<ModifiedElement> ModifiedElements { get; set; }
        public ComparisonSummary Summary { get; set; }
    }

    public class ModifiedElement
    {
        public string ElementId { get; set; }
        public List<string> ChangedProperties { get; set; }
    }

    public class ComparisonSummary
    {
        public int TotalAdded { get; set; }
        public int TotalDeleted { get; set; }
        public int TotalModified { get; set; }
        public int NetChange { get; set; }
    }

    public class TradeCoordinationRule
    {
        public string RuleId { get; set; }
        public string Name { get; set; }
        public string Trade1 { get; set; }
        public string Trade2 { get; set; }
        public CoordinationRuleType RuleType { get; set; }
        public double MinClearance { get; set; }
        public string Description { get; set; }
    }

    public class RuleCheckRequest
    {
        public string Trade1 { get; set; }
        public string Trade2 { get; set; }
        public double ActualClearance { get; set; }
        public string Location { get; set; }
    }

    public class RuleViolation
    {
        public string RuleId { get; set; }
        public string RuleName { get; set; }
        public string Description { get; set; }
        public ViolationSeverity Severity { get; set; }
        public string Location { get; set; }
    }

    public class CoordinationEventArgs : EventArgs
    {
        public string Message { get; set; }
        public string SessionId { get; set; }
    }

    public class ChangeEventArgs : EventArgs
    {
        public string ChangeId { get; set; }
        public string ChangeType { get; set; }
        public string Description { get; set; }
    }

    #endregion

    #region Enums

    public enum FederatedModelStatus { Active, Archived, OnHold }
    public enum CoordinationSessionType { ClashReview, DesignReview, TradeCoordination, Milestone }
    public enum SessionStatus { Scheduled, InProgress, Completed, Cancelled }
    public enum ClashType { Hard, Soft, Clearance, Duplicate }
    public enum ClashSeverity { Critical, Major, Minor, Informational }
    public enum ClashStatus { New, Active, Reviewed, Resolved, Approved }
    public enum IssueStatus { Open, InProgress, Resolved, Closed }
    public enum ActionItemPriority { Low, Medium, High, Critical }
    public enum ActionItemStatus { Open, InProgress, Completed, Cancelled }
    public enum ScheduleConflictType { SpatialOverlap, SequenceError, ResourceConflict }
    public enum ConflictSeverity { Low, Warning, High, Critical }
    public enum CostReportType { ByCategory, ByDivision, ByLevel, BySystem }
    public enum ChangeStatus { Pending, Reviewed, Accepted, Rejected }
    public enum CoordinationRuleType { Clearance, Priority, Access, Sequence }
    public enum ViolationSeverity { Info, Warning, Error, Critical }

    #endregion
}
