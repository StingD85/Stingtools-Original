// StingBIM.AI.Reasoning.Temporal.TemporalReasoner
// Temporal reasoning for project phases and design sequences
// Master Proposal Reference: Part 2.1 Pillar 3 - Temporal Intelligence

using System;
using System.Collections.Generic;
using System.Linq;
using NLog;

namespace StingBIM.AI.Reasoning.Temporal
{
    /// <summary>
    /// Temporal reasoning engine for understanding project phases,
    /// construction sequences, and time-based design dependencies.
    /// </summary>
    public class TemporalReasoner
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly List<ProjectPhase> _phases;
        private readonly List<TemporalConstraint> _constraints;
        private readonly List<SequencePattern> _patterns;
        private readonly Dictionary<string, TaskDefinition> _tasks;

        public TemporalReasoner()
        {
            _phases = new List<ProjectPhase>();
            _constraints = new List<TemporalConstraint>();
            _patterns = new List<SequencePattern>();
            _tasks = new Dictionary<string, TaskDefinition>();

            InitializeStandardPhases();
            InitializeConstructionSequences();
        }

        #region Public API

        /// <summary>
        /// Determines the current project phase based on design state.
        /// </summary>
        public PhaseAnalysis AnalyzeProjectPhase(ProjectState state)
        {
            var analysis = new PhaseAnalysis
            {
                AnalyzedAt = DateTime.Now
            };

            // Determine current phase based on indicators
            foreach (var phase in _phases.OrderBy(p => p.Order))
            {
                var completion = CalculatePhaseCompletion(phase, state);
                analysis.PhaseCompletions[phase.Id] = completion;

                if (completion > 0 && completion < 1.0)
                {
                    analysis.CurrentPhase = phase;
                }
                else if (completion >= 1.0 && analysis.CurrentPhase == null)
                {
                    // This phase is complete, check next
                    continue;
                }
            }

            // If no current phase found, we're in the first incomplete phase
            if (analysis.CurrentPhase == null)
            {
                analysis.CurrentPhase = _phases.FirstOrDefault(p =>
                    analysis.PhaseCompletions.GetValueOrDefault(p.Id, 0) < 1.0);
            }

            // Calculate overall progress
            analysis.OverallProgress = analysis.PhaseCompletions.Values.Average();

            // Determine next actions
            analysis.NextActions = DetermineNextActions(analysis.CurrentPhase, state);

            // Identify blockers
            analysis.Blockers = IdentifyBlockers(state);

            return analysis;
        }

        /// <summary>
        /// Validates a proposed sequence of actions.
        /// </summary>
        public SequenceValidation ValidateSequence(List<DesignAction> actions)
        {
            var validation = new SequenceValidation { IsValid = true };

            for (int i = 0; i < actions.Count; i++)
            {
                var action = actions[i];
                var previousActions = actions.Take(i).ToList();

                // Check prerequisites
                var prereqCheck = CheckPrerequisites(action, previousActions);
                if (!prereqCheck.Satisfied)
                {
                    validation.IsValid = false;
                    validation.Violations.Add(new SequenceViolation
                    {
                        ActionIndex = i,
                        Action = action,
                        Reason = prereqCheck.Reason,
                        MissingPrerequisites = prereqCheck.MissingPrerequisites
                    });
                }

                // Check temporal constraints
                foreach (var constraint in _constraints.Where(c => c.AppliesTo(action.ActionType)))
                {
                    if (!constraint.IsSatisfied(action, previousActions))
                    {
                        validation.IsValid = false;
                        validation.Violations.Add(new SequenceViolation
                        {
                            ActionIndex = i,
                            Action = action,
                            Reason = constraint.ViolationMessage,
                            ConstraintId = constraint.Id
                        });
                    }
                }
            }

            // Calculate efficiency score
            validation.EfficiencyScore = CalculateSequenceEfficiency(actions);

            // Suggest optimizations
            validation.Suggestions = SuggestSequenceOptimizations(actions);

            return validation;
        }

        /// <summary>
        /// Suggests the optimal sequence for a set of tasks.
        /// </summary>
        public SequenceSuggestion SuggestOptimalSequence(List<string> taskIds, ProjectState state)
        {
            var suggestion = new SequenceSuggestion();

            // Build dependency graph
            var taskGraph = BuildTaskGraph(taskIds);

            // Topological sort with optimization
            var sortedTasks = TopologicalSort(taskGraph);

            // Apply construction sequence patterns
            sortedTasks = ApplySequencePatterns(sortedTasks);

            suggestion.RecommendedSequence = sortedTasks
                .Select(t => _tasks.GetValueOrDefault(t))
                .Where(t => t != null)
                .ToList();

            // Estimate timeline
            suggestion.EstimatedDuration = EstimateSequenceDuration(suggestion.RecommendedSequence);

            // Identify critical path
            suggestion.CriticalPath = IdentifyCriticalPath(taskGraph, suggestion.RecommendedSequence);

            // Add parallel opportunities
            suggestion.ParallelOpportunities = FindParallelOpportunities(taskGraph);

            return suggestion;
        }

        /// <summary>
        /// Predicts potential issues based on temporal patterns.
        /// </summary>
        public List<TemporalPrediction> PredictIssues(ProjectState state, TimeSpan lookAhead)
        {
            var predictions = new List<TemporalPrediction>();
            var currentPhase = AnalyzeProjectPhase(state).CurrentPhase;

            // Check for delayed dependencies
            foreach (var task in state.PendingTasks)
            {
                var definition = _tasks.GetValueOrDefault(task.TaskId);
                if (definition == null) continue;

                foreach (var prereq in definition.Prerequisites)
                {
                    var prereqTask = state.CompletedTasks.FirstOrDefault(t => t.TaskId == prereq);
                    if (prereqTask == null)
                    {
                        predictions.Add(new TemporalPrediction
                        {
                            Type = PredictionType.DependencyRisk,
                            Severity = PredictionSeverity.High,
                            Description = $"Task '{task.Name}' waiting on incomplete prerequisite '{prereq}'",
                            AffectedTask = task.TaskId,
                            RecommendedAction = $"Complete {prereq} first"
                        });
                    }
                }
            }

            // Check for phase transition issues
            if (currentPhase != null)
            {
                var phaseCompletion = CalculatePhaseCompletion(currentPhase, state);
                if (phaseCompletion > 0.8 && phaseCompletion < 1.0)
                {
                    var incompleteItems = GetIncompletePhaseItems(currentPhase, state);
                    predictions.Add(new TemporalPrediction
                    {
                        Type = PredictionType.PhaseTransition,
                        Severity = PredictionSeverity.Medium,
                        Description = $"Phase '{currentPhase.Name}' nearly complete. {incompleteItems.Count} items remaining.",
                        Details = incompleteItems
                    });
                }
            }

            // Check construction sequence issues
            foreach (var pattern in _patterns)
            {
                var violations = CheckPatternViolations(pattern, state);
                predictions.AddRange(violations);
            }

            return predictions.OrderByDescending(p => (int)p.Severity).ToList();
        }

        /// <summary>
        /// Gets the construction sequence for a building element.
        /// </summary>
        public ConstructionSequence GetConstructionSequence(string elementType)
        {
            var sequence = new ConstructionSequence { ElementType = elementType };

            switch (elementType.ToLowerInvariant())
            {
                case "wall":
                    sequence.Steps = new List<ConstructionStep>
                    {
                        new() { Order = 1, Name = "Set out", Description = "Mark wall positions", Duration = TimeSpan.FromHours(1) },
                        new() { Order = 2, Name = "Foundation check", Description = "Verify foundation level", Duration = TimeSpan.FromHours(0.5) },
                        new() { Order = 3, Name = "First course", Description = "Lay first course with DPC", Duration = TimeSpan.FromHours(2) },
                        new() { Order = 4, Name = "Build up", Description = "Continue wall construction", Duration = TimeSpan.FromDays(1) },
                        new() { Order = 5, Name = "Openings", Description = "Install lintels and frames", Duration = TimeSpan.FromHours(4) },
                        new() { Order = 6, Name = "Finish", Description = "Point and clean", Duration = TimeSpan.FromHours(2) }
                    };
                    sequence.Prerequisites = new[] { "Foundation", "Materials delivery" };
                    break;

                case "floor":
                    sequence.Steps = new List<ConstructionStep>
                    {
                        new() { Order = 1, Name = "Formwork", Description = "Install slab formwork", Duration = TimeSpan.FromDays(1) },
                        new() { Order = 2, Name = "Reinforcement", Description = "Place rebar", Duration = TimeSpan.FromDays(1) },
                        new() { Order = 3, Name = "Services", Description = "Install embedded services", Duration = TimeSpan.FromHours(4) },
                        new() { Order = 4, Name = "Pour", Description = "Concrete placement", Duration = TimeSpan.FromHours(4) },
                        new() { Order = 5, Name = "Cure", Description = "Curing period", Duration = TimeSpan.FromDays(7) },
                        new() { Order = 6, Name = "Strip", Description = "Remove formwork", Duration = TimeSpan.FromDays(1) }
                    };
                    sequence.Prerequisites = new[] { "Walls to slab level", "Structural engineer approval" };
                    break;

                case "roof":
                    sequence.Steps = new List<ConstructionStep>
                    {
                        new() { Order = 1, Name = "Wall plate", Description = "Install wall plate", Duration = TimeSpan.FromHours(4) },
                        new() { Order = 2, Name = "Trusses", Description = "Install roof trusses", Duration = TimeSpan.FromDays(1) },
                        new() { Order = 3, Name = "Bracing", Description = "Install bracing", Duration = TimeSpan.FromHours(4) },
                        new() { Order = 4, Name = "Battens", Description = "Fix battens", Duration = TimeSpan.FromDays(1) },
                        new() { Order = 5, Name = "Membrane", Description = "Install underlay", Duration = TimeSpan.FromHours(4) },
                        new() { Order = 6, Name = "Covering", Description = "Install roof covering", Duration = TimeSpan.FromDays(2) }
                    };
                    sequence.Prerequisites = new[] { "Walls complete", "Ring beam if applicable" };
                    break;
            }

            sequence.TotalDuration = sequence.Steps.Aggregate(
                TimeSpan.Zero,
                (sum, step) => sum + step.Duration);

            return sequence;
        }

        /// <summary>
        /// Performs full Critical Path Method (CPM) analysis on a set of tasks.
        /// Computes earliest/latest start and finish times, total float, and the critical path.
        /// </summary>
        public CriticalPathAnalysis PerformCriticalPathAnalysis(List<string> taskIds)
        {
            var analysis = new CriticalPathAnalysis();

            var taskGraph = BuildTaskGraph(taskIds);
            var sortedIds = TopologicalSort(taskGraph);

            var sequence = sortedIds
                .Select(t => _tasks.GetValueOrDefault(t))
                .Where(t => t != null)
                .ToList();

            if (sequence.Count == 0)
            {
                analysis.ProjectDuration = TimeSpan.Zero;
                return analysis;
            }

            // Build successor map
            var successors = new Dictionary<string, List<string>>();
            foreach (var task in sequence)
            {
                successors[task.Id] = new List<string>();
            }
            foreach (var kvp in taskGraph)
            {
                foreach (var prereq in kvp.Value)
                {
                    if (successors.ContainsKey(prereq))
                    {
                        successors[prereq].Add(kvp.Key);
                    }
                }
            }

            // Forward pass: Earliest Start / Earliest Finish
            var es = new Dictionary<string, double>();
            var ef = new Dictionary<string, double>();

            foreach (var task in sequence)
            {
                var prereqs = taskGraph.ContainsKey(task.Id) ? taskGraph[task.Id] : new List<string>();
                es[task.Id] = prereqs.Count > 0
                    ? prereqs.Where(p => ef.ContainsKey(p)).Select(p => ef[p]).DefaultIfEmpty(0).Max()
                    : 0;
                ef[task.Id] = es[task.Id] + task.Duration.TotalDays;
            }

            double projectDuration = ef.Values.Any() ? ef.Values.Max() : 0;

            // Backward pass: Latest Finish / Latest Start
            var lf = new Dictionary<string, double>();
            var ls = new Dictionary<string, double>();

            foreach (var task in sequence.AsEnumerable().Reverse())
            {
                var succs = successors.ContainsKey(task.Id) ? successors[task.Id] : new List<string>();
                lf[task.Id] = succs.Count > 0
                    ? succs.Where(s => ls.ContainsKey(s)).Select(s => ls[s]).DefaultIfEmpty(projectDuration).Min()
                    : projectDuration;
                ls[task.Id] = lf[task.Id] - task.Duration.TotalDays;
            }

            // Build scheduled tasks with float computation
            const double FloatTolerance = 0.001;

            foreach (var task in sequence)
            {
                var totalFloat = ls[task.Id] - es[task.Id];
                analysis.ScheduledTasks.Add(new ScheduledTask
                {
                    TaskId = task.Id,
                    TaskName = task.Name,
                    Duration = task.Duration,
                    EarliestStart = TimeSpan.FromDays(es[task.Id]),
                    EarliestFinish = TimeSpan.FromDays(ef[task.Id]),
                    LatestStart = TimeSpan.FromDays(ls[task.Id]),
                    LatestFinish = TimeSpan.FromDays(lf[task.Id]),
                    TotalFloat = TimeSpan.FromDays(totalFloat),
                    IsCritical = Math.Abs(totalFloat) < FloatTolerance
                });
            }

            analysis.CriticalPath = analysis.ScheduledTasks
                .Where(t => t.IsCritical)
                .Select(t => t.TaskId)
                .ToList();

            analysis.ProjectDuration = TimeSpan.FromDays(projectDuration);

            Logger.Info($"CPM analysis: {analysis.CriticalPath.Count} critical tasks, project duration: {projectDuration:F1} days");

            return analysis;
        }

        #endregion

        #region Initialization

        private void InitializeStandardPhases()
        {
            _phases.AddRange(new[]
            {
                new ProjectPhase
                {
                    Id = "concept",
                    Name = "Concept Design",
                    Order = 1,
                    Description = "Initial design concepts and massing",
                    Indicators = new[] { "HasMassing", "HasSiteAnalysis", "HasBrief" },
                    Deliverables = new[] { "Concept sketches", "Site analysis", "Initial budget" }
                },
                new ProjectPhase
                {
                    Id = "schematic",
                    Name = "Schematic Design",
                    Order = 2,
                    Description = "Develop design with room layouts",
                    Indicators = new[] { "HasRoomLayouts", "HasElevations", "HasPreliminaryStructure" },
                    Deliverables = new[] { "Floor plans", "Elevations", "Sections", "Preliminary specs" }
                },
                new ProjectPhase
                {
                    Id = "developed",
                    Name = "Developed Design",
                    Order = 3,
                    Description = "Detailed coordination of all systems",
                    Indicators = new[] { "HasDetailedPlans", "HasMEPCoordination", "HasStructuralDesign" },
                    Deliverables = new[] { "Coordinated drawings", "Structural design", "MEP design" }
                },
                new ProjectPhase
                {
                    Id = "construction",
                    Name = "Construction Documents",
                    Order = 4,
                    Description = "Complete documentation for construction",
                    Indicators = new[] { "HasConstructionDrawings", "HasSpecifications", "HasSchedules" },
                    Deliverables = new[] { "Construction drawings", "Specifications", "Bill of quantities" }
                },
                new ProjectPhase
                {
                    Id = "tender",
                    Name = "Tender",
                    Order = 5,
                    Description = "Contractor selection",
                    Indicators = new[] { "HasTenderDocuments", "HasBidAnalysis" },
                    Deliverables = new[] { "Tender documents", "Contractor selection" }
                },
                new ProjectPhase
                {
                    Id = "construction_admin",
                    Name = "Construction Administration",
                    Order = 6,
                    Description = "Site supervision and administration",
                    Indicators = new[] { "HasSiteInspections", "HasRFIResponses" },
                    Deliverables = new[] { "Site reports", "As-built drawings" }
                }
            });

            Logger.Info($"Initialized {_phases.Count} project phases");
        }

        private void InitializeConstructionSequences()
        {
            // Standard construction sequence constraints
            _constraints.AddRange(new[]
            {
                new TemporalConstraint
                {
                    Id = "foundation-first",
                    Name = "Foundation Before Superstructure",
                    Antecedent = "Foundation",
                    Consequent = "Wall",
                    Relation = TemporalRelation.Before,
                    ViolationMessage = "Walls cannot be built before foundation is complete"
                },
                new TemporalConstraint
                {
                    Id = "walls-before-roof",
                    Name = "Walls Before Roof",
                    Antecedent = "Wall",
                    Consequent = "Roof",
                    Relation = TemporalRelation.Before,
                    ViolationMessage = "Roof cannot be installed before walls are complete"
                },
                new TemporalConstraint
                {
                    Id = "structure-before-services",
                    Name = "Structure Before Services",
                    Antecedent = "Structure",
                    Consequent = "MEP_FirstFix",
                    Relation = TemporalRelation.Before,
                    ViolationMessage = "MEP first fix requires structure to be complete"
                },
                new TemporalConstraint
                {
                    Id = "firstfix-before-finishes",
                    Name = "First Fix Before Finishes",
                    Antecedent = "MEP_FirstFix",
                    Consequent = "Finishes",
                    Relation = TemporalRelation.Before,
                    ViolationMessage = "Finishes cannot proceed before first fix is complete"
                }
            });

            // Standard task definitions
            _tasks["foundation"] = new TaskDefinition
            {
                Id = "foundation",
                Name = "Foundation",
                Category = "Substructure",
                Prerequisites = new List<string> { "site_clearing", "setting_out" },
                Duration = TimeSpan.FromDays(14)
            };

            _tasks["walls"] = new TaskDefinition
            {
                Id = "walls",
                Name = "Wall Construction",
                Category = "Superstructure",
                Prerequisites = new List<string> { "foundation" },
                Duration = TimeSpan.FromDays(21)
            };

            _tasks["roof"] = new TaskDefinition
            {
                Id = "roof",
                Name = "Roof Construction",
                Category = "Superstructure",
                Prerequisites = new List<string> { "walls" },
                Duration = TimeSpan.FromDays(7)
            };

            _tasks["mep_firstfix"] = new TaskDefinition
            {
                Id = "mep_firstfix",
                Name = "MEP First Fix",
                Category = "Services",
                Prerequisites = new List<string> { "walls", "roof" },
                Duration = TimeSpan.FromDays(14)
            };

            _tasks["plastering"] = new TaskDefinition
            {
                Id = "plastering",
                Name = "Plastering",
                Category = "Finishes",
                Prerequisites = new List<string> { "mep_firstfix" },
                Duration = TimeSpan.FromDays(10)
            };

            _tasks["mep_secondfix"] = new TaskDefinition
            {
                Id = "mep_secondfix",
                Name = "MEP Second Fix",
                Category = "Services",
                Prerequisites = new List<string> { "plastering" },
                Duration = TimeSpan.FromDays(7)
            };

            _tasks["painting"] = new TaskDefinition
            {
                Id = "painting",
                Name = "Painting",
                Category = "Finishes",
                Prerequisites = new List<string> { "plastering" },
                Duration = TimeSpan.FromDays(7)
            };

            _tasks["flooring"] = new TaskDefinition
            {
                Id = "flooring",
                Name = "Floor Finishes",
                Category = "Finishes",
                Prerequisites = new List<string> { "mep_secondfix" },
                Duration = TimeSpan.FromDays(7)
            };

            // Standard patterns
            _patterns.Add(new SequencePattern
            {
                Id = "wet-before-dry",
                Name = "Wet Trades Before Dry",
                Description = "Wet works (plastering, screeding) must dry before dry finishes",
                RequiredSequence = new[] { "plastering", "drying_period", "painting" }
            });

            _patterns.Add(new SequencePattern
            {
                Id = "top-down-services",
                Name = "Top-Down Services Installation",
                Description = "Services installed from top floors down for gravity drainage",
                RequiredSequence = new[] { "roof_drainage", "upper_floor_services", "ground_floor_services" }
            });

            Logger.Info($"Initialized {_constraints.Count} constraints, {_tasks.Count} tasks, {_patterns.Count} patterns");
        }

        #endregion

        #region Private Methods

        private double CalculatePhaseCompletion(ProjectPhase phase, ProjectState state)
        {
            if (phase.Indicators == null || phase.Indicators.Length == 0)
                return 0;

            var completed = phase.Indicators.Count(indicator =>
                state.CompletedIndicators.Contains(indicator));

            return (double)completed / phase.Indicators.Length;
        }

        private List<SuggestedAction> DetermineNextActions(ProjectPhase currentPhase, ProjectState state)
        {
            var actions = new List<SuggestedAction>();

            if (currentPhase == null) return actions;

            // Find incomplete indicators for current phase
            foreach (var indicator in currentPhase.Indicators)
            {
                if (!state.CompletedIndicators.Contains(indicator))
                {
                    actions.Add(new SuggestedAction
                    {
                        ActionId = indicator,
                        Description = $"Complete: {indicator}",
                        Priority = ActionPriority.High,
                        Phase = currentPhase.Id
                    });
                }
            }

            // Add deliverables
            foreach (var deliverable in currentPhase.Deliverables)
            {
                if (!state.CompletedDeliverables.Contains(deliverable))
                {
                    actions.Add(new SuggestedAction
                    {
                        ActionId = deliverable,
                        Description = $"Produce: {deliverable}",
                        Priority = ActionPriority.Medium,
                        Phase = currentPhase.Id
                    });
                }
            }

            return actions.OrderByDescending(a => (int)a.Priority).ToList();
        }

        private List<string> IdentifyBlockers(ProjectState state)
        {
            var blockers = new List<string>();

            foreach (var task in state.PendingTasks)
            {
                var definition = _tasks.GetValueOrDefault(task.TaskId);
                if (definition == null) continue;

                foreach (var prereq in definition.Prerequisites)
                {
                    if (!state.CompletedTasks.Any(t => t.TaskId == prereq))
                    {
                        blockers.Add($"{task.Name} blocked by incomplete {prereq}");
                    }
                }
            }

            return blockers;
        }

        private PrerequisiteCheck CheckPrerequisites(DesignAction action, List<DesignAction> previousActions)
        {
            var check = new PrerequisiteCheck { Satisfied = true };

            if (_tasks.TryGetValue(action.ActionType, out var definition))
            {
                foreach (var prereq in definition.Prerequisites)
                {
                    var found = previousActions.Any(a => a.ActionType == prereq);
                    if (!found)
                    {
                        check.Satisfied = false;
                        check.MissingPrerequisites.Add(prereq);
                    }
                }

                if (!check.Satisfied)
                {
                    check.Reason = $"Missing prerequisites: {string.Join(", ", check.MissingPrerequisites)}";
                }
            }

            return check;
        }

        private double CalculateSequenceEfficiency(List<DesignAction> actions)
        {
            if (actions.Count < 2) return 1.0;

            var efficiencyScore = 1.0;

            // Penalize for out-of-order actions
            for (int i = 1; i < actions.Count; i++)
            {
                var prev = actions[i - 1];
                var curr = actions[i];

                if (_tasks.TryGetValue(curr.ActionType, out var currDef))
                {
                    // If current action's prerequisite is not the previous action, slight penalty
                    if (currDef.Prerequisites.Any() && !currDef.Prerequisites.Contains(prev.ActionType))
                    {
                        efficiencyScore -= 0.05;
                    }
                }
            }

            return Math.Max(0, efficiencyScore);
        }

        private List<string> SuggestSequenceOptimizations(List<DesignAction> actions)
        {
            var suggestions = new List<string>();

            // Find parallel opportunities
            var independentActions = FindIndependentActions(actions);
            if (independentActions.Count > 1)
            {
                suggestions.Add($"Actions {string.Join(", ", independentActions)} could be performed in parallel");
            }

            return suggestions;
        }

        private List<string> FindIndependentActions(List<DesignAction> actions)
        {
            var independent = new List<string>();

            foreach (var action in actions)
            {
                if (_tasks.TryGetValue(action.ActionType, out var def))
                {
                    if (!def.Prerequisites.Any())
                    {
                        independent.Add(action.ActionType);
                    }
                }
            }

            return independent;
        }

        private Dictionary<string, List<string>> BuildTaskGraph(List<string> taskIds)
        {
            var graph = new Dictionary<string, List<string>>();

            foreach (var taskId in taskIds)
            {
                graph[taskId] = new List<string>();

                if (_tasks.TryGetValue(taskId, out var def))
                {
                    foreach (var prereq in def.Prerequisites)
                    {
                        if (taskIds.Contains(prereq))
                        {
                            graph[taskId].Add(prereq);
                        }
                    }
                }
            }

            return graph;
        }

        private List<string> TopologicalSort(Dictionary<string, List<string>> graph)
        {
            var sorted = new List<string>();
            var visited = new HashSet<string>();
            var inProgress = new HashSet<string>();

            void Visit(string node)
            {
                if (visited.Contains(node)) return;
                if (inProgress.Contains(node))
                {
                    Logger.Warn($"Circular dependency detected at {node}");
                    return;
                }

                inProgress.Add(node);

                if (graph.ContainsKey(node))
                {
                    foreach (var dep in graph[node])
                    {
                        Visit(dep);
                    }
                }

                inProgress.Remove(node);
                visited.Add(node);
                sorted.Add(node);
            }

            foreach (var node in graph.Keys)
            {
                Visit(node);
            }

            return sorted;
        }

        private List<string> ApplySequencePatterns(List<string> tasks)
        {
            // Apply standard construction sequence if applicable
            var constructionOrder = new[]
            {
                "foundation", "walls", "floor", "roof",
                "mep_firstfix", "plastering", "mep_secondfix",
                "painting", "flooring"
            };

            return tasks.OrderBy(t =>
            {
                var index = Array.IndexOf(constructionOrder, t);
                return index >= 0 ? index : 100;
            }).ToList();
        }

        private TimeSpan EstimateSequenceDuration(List<TaskDefinition> tasks)
        {
            return tasks.Aggregate(TimeSpan.Zero, (sum, task) => sum + task.Duration);
        }

        private List<string> IdentifyCriticalPath(Dictionary<string, List<string>> graph, List<TaskDefinition> sequence)
        {
            if (sequence == null || sequence.Count == 0)
                return new List<string>();

            // Build successor map (inverse of prerequisite graph)
            var successors = new Dictionary<string, List<string>>();
            foreach (var task in sequence)
            {
                successors[task.Id] = new List<string>();
            }
            foreach (var kvp in graph)
            {
                foreach (var prereq in kvp.Value)
                {
                    if (successors.ContainsKey(prereq))
                    {
                        successors[prereq].Add(kvp.Key);
                    }
                }
            }

            // Forward pass: compute Earliest Start (ES) and Earliest Finish (EF)
            var es = new Dictionary<string, double>();
            var ef = new Dictionary<string, double>();

            foreach (var task in sequence)
            {
                var prereqs = graph.ContainsKey(task.Id) ? graph[task.Id] : new List<string>();
                es[task.Id] = prereqs.Count > 0
                    ? prereqs.Where(p => ef.ContainsKey(p)).Select(p => ef[p]).DefaultIfEmpty(0).Max()
                    : 0;
                ef[task.Id] = es[task.Id] + task.Duration.TotalDays;
            }

            double projectDuration = ef.Values.Any() ? ef.Values.Max() : 0;

            // Backward pass: compute Latest Finish (LF) and Latest Start (LS)
            var lf = new Dictionary<string, double>();
            var ls = new Dictionary<string, double>();

            foreach (var task in sequence.AsEnumerable().Reverse())
            {
                var succs = successors.ContainsKey(task.Id) ? successors[task.Id] : new List<string>();
                lf[task.Id] = succs.Count > 0
                    ? succs.Where(s => ls.ContainsKey(s)).Select(s => ls[s]).DefaultIfEmpty(projectDuration).Min()
                    : projectDuration;
                ls[task.Id] = lf[task.Id] - task.Duration.TotalDays;
            }

            // Critical path = tasks with zero total float (LS - ES ≈ 0)
            const double FloatTolerance = 0.001;
            var criticalPath = sequence
                .Where(t => Math.Abs(ls.GetValueOrDefault(t.Id) - es.GetValueOrDefault(t.Id)) < FloatTolerance)
                .Select(t => t.Id)
                .ToList();

            Logger.Debug($"Critical path: {criticalPath.Count} critical tasks of {sequence.Count}, duration: {projectDuration:F1} days");

            return criticalPath;
        }

        private List<ParallelOpportunity> FindParallelOpportunities(Dictionary<string, List<string>> graph)
        {
            var opportunities = new List<ParallelOpportunity>();

            // Find tasks with same dependencies that could run in parallel
            var byDependencies = graph
                .GroupBy(kvp => string.Join(",", kvp.Value.OrderBy(x => x)))
                .Where(g => g.Count() > 1);

            foreach (var group in byDependencies)
            {
                opportunities.Add(new ParallelOpportunity
                {
                    Tasks = group.Select(g => g.Key).ToList(),
                    SharedDependencies = group.First().Value
                });
            }

            return opportunities;
        }

        private List<string> GetIncompletePhaseItems(ProjectPhase phase, ProjectState state)
        {
            return phase.Indicators
                .Where(i => !state.CompletedIndicators.Contains(i))
                .ToList();
        }

        private List<TemporalPrediction> CheckPatternViolations(SequencePattern pattern, ProjectState state)
        {
            var predictions = new List<TemporalPrediction>();

            // Check if pattern sequence is being followed
            var completedInSequence = pattern.RequiredSequence
                .TakeWhile(s => state.CompletedTasks.Any(t => t.TaskId == s))
                .ToList();

            if (completedInSequence.Count > 0 && completedInSequence.Count < pattern.RequiredSequence.Length)
            {
                var nextRequired = pattern.RequiredSequence[completedInSequence.Count];
                var pending = state.PendingTasks.FirstOrDefault(t => t.TaskId == nextRequired);

                if (pending == null)
                {
                    predictions.Add(new TemporalPrediction
                    {
                        Type = PredictionType.SequenceViolation,
                        Severity = PredictionSeverity.Medium,
                        Description = $"Pattern '{pattern.Name}' expects '{nextRequired}' next",
                        RecommendedAction = $"Consider scheduling {nextRequired}"
                    });
                }
            }

            return predictions;
        }

        #endregion
    }

    #region Supporting Types

    public class ProjectPhase
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int Order { get; set; }
        public string Description { get; set; }
        public string[] Indicators { get; set; }
        public string[] Deliverables { get; set; }
    }

    public class ProjectState
    {
        public HashSet<string> CompletedIndicators { get; set; } = new();
        public HashSet<string> CompletedDeliverables { get; set; } = new();
        public List<TaskInstance> CompletedTasks { get; set; } = new();
        public List<TaskInstance> PendingTasks { get; set; } = new();
    }

    public class TaskInstance
    {
        public string TaskId { get; set; }
        public string Name { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    public class TaskDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public List<string> Prerequisites { get; set; } = new();
        public TimeSpan Duration { get; set; }
    }

    public class PhaseAnalysis
    {
        public DateTime AnalyzedAt { get; set; }
        public ProjectPhase CurrentPhase { get; set; }
        public Dictionary<string, double> PhaseCompletions { get; set; } = new();
        public double OverallProgress { get; set; }
        public List<SuggestedAction> NextActions { get; set; } = new();
        public List<string> Blockers { get; set; } = new();
    }

    public class SuggestedAction
    {
        public string ActionId { get; set; }
        public string Description { get; set; }
        public ActionPriority Priority { get; set; }
        public string Phase { get; set; }
    }

    public enum ActionPriority { Low, Medium, High, Critical }

    public class TemporalConstraint
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Antecedent { get; set; }
        public string Consequent { get; set; }
        public TemporalRelation Relation { get; set; }
        public string ViolationMessage { get; set; }

        public bool AppliesTo(string actionType) =>
            actionType == Consequent;

        public bool IsSatisfied(DesignAction action, List<DesignAction> previous) =>
            Relation == TemporalRelation.Before &&
            previous.Any(p => p.ActionType == Antecedent);
    }

    public enum TemporalRelation { Before, After, During, Overlaps, Meets }

    public class SequencePattern
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string[] RequiredSequence { get; set; }
    }

    public class DesignAction
    {
        public string ActionType { get; set; }
        public string Description { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class SequenceValidation
    {
        public bool IsValid { get; set; }
        public List<SequenceViolation> Violations { get; set; } = new();
        public double EfficiencyScore { get; set; }
        public List<string> Suggestions { get; set; } = new();
    }

    public class SequenceViolation
    {
        public int ActionIndex { get; set; }
        public DesignAction Action { get; set; }
        public string Reason { get; set; }
        public string ConstraintId { get; set; }
        public List<string> MissingPrerequisites { get; set; } = new();
    }

    public class PrerequisiteCheck
    {
        public bool Satisfied { get; set; }
        public string Reason { get; set; }
        public List<string> MissingPrerequisites { get; set; } = new();
    }

    public class SequenceSuggestion
    {
        public List<TaskDefinition> RecommendedSequence { get; set; }
        public TimeSpan EstimatedDuration { get; set; }
        public List<string> CriticalPath { get; set; }
        public List<ParallelOpportunity> ParallelOpportunities { get; set; }
    }

    public class ParallelOpportunity
    {
        public List<string> Tasks { get; set; }
        public List<string> SharedDependencies { get; set; }
    }

    public class TemporalPrediction
    {
        public PredictionType Type { get; set; }
        public PredictionSeverity Severity { get; set; }
        public string Description { get; set; }
        public string AffectedTask { get; set; }
        public string RecommendedAction { get; set; }
        public List<string> Details { get; set; }
    }

    public enum PredictionType { DependencyRisk, PhaseTransition, SequenceViolation, ResourceConflict }
    public enum PredictionSeverity { Low, Medium, High, Critical }

    public class ConstructionSequence
    {
        public string ElementType { get; set; }
        public List<ConstructionStep> Steps { get; set; } = new();
        public string[] Prerequisites { get; set; }
        public TimeSpan TotalDuration { get; set; }
    }

    public class ConstructionStep
    {
        public int Order { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public TimeSpan Duration { get; set; }
    }

    public class CriticalPathAnalysis
    {
        public List<ScheduledTask> ScheduledTasks { get; set; } = new();
        public List<string> CriticalPath { get; set; } = new();
        public TimeSpan ProjectDuration { get; set; }
    }

    public class ScheduledTask
    {
        public string TaskId { get; set; }
        public string TaskName { get; set; }
        public TimeSpan Duration { get; set; }
        public TimeSpan EarliestStart { get; set; }
        public TimeSpan EarliestFinish { get; set; }
        public TimeSpan LatestStart { get; set; }
        public TimeSpan LatestFinish { get; set; }
        public TimeSpan TotalFloat { get; set; }
        public bool IsCritical { get; set; }
    }

    #endregion
}
