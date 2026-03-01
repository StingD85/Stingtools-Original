// StingBIM.AI.Collaboration - AgentCollaborationProtocol.cs
// Multi-Agent Negotiation and Consensus Building Protocol
// Phase 4: Enterprise AI Transformation - Collaborative AI
// Copyright (c) 2026 StingBIM. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.Revit.DB;

namespace StingBIM.AI.Collaboration.Protocol
{
    /// <summary>
    /// Advanced multi-agent collaboration protocol enabling AI agents to negotiate,
    /// resolve conflicts, and reach consensus on design decisions.
    /// </summary>
    public class AgentCollaborationProtocol
    {
        #region Fields

        private readonly Dictionary<string, CollaborativeAgent> _agents;
        private readonly Dictionary<string, NegotiationSession> _sessions;
        private readonly ConflictResolver _conflictResolver;
        private readonly ConsensusBuilder _consensusBuilder;
        private readonly DecisionLogger _decisionLogger;
        private readonly object _lockObject = new object();

        #endregion

        #region Constructor

        public AgentCollaborationProtocol()
        {
            _agents = new Dictionary<string, CollaborativeAgent>(StringComparer.OrdinalIgnoreCase);
            _sessions = new Dictionary<string, NegotiationSession>(StringComparer.OrdinalIgnoreCase);
            _conflictResolver = new ConflictResolver();
            _consensusBuilder = new ConsensusBuilder();
            _decisionLogger = new DecisionLogger();

            InitializeDefaultAgents();
        }

        #endregion

        #region Initialization

        private void InitializeDefaultAgents()
        {
            RegisterAgent(new CollaborativeAgent
            {
                AgentId = "ARCH",
                Name = "Architectural Agent",
                Discipline = "Architecture",
                Priority = 3,
                Capabilities = new List<string>
                {
                    "Space Planning", "Aesthetics", "User Experience",
                    "Building Envelope", "Interior Design", "Accessibility"
                },
                PreferenceWeights = new Dictionary<string, double>
                {
                    ["Aesthetics"] = 0.9,
                    ["UserExperience"] = 0.85,
                    ["SpaceEfficiency"] = 0.8,
                    ["Daylight"] = 0.75,
                    ["Cost"] = 0.5
                }
            });

            RegisterAgent(new CollaborativeAgent
            {
                AgentId = "STRUCT",
                Name = "Structural Agent",
                Discipline = "Structural",
                Priority = 5,
                Capabilities = new List<string>
                {
                    "Load Analysis", "Structural System Design", "Foundation Design",
                    "Seismic Design", "Material Selection", "Connection Design"
                },
                PreferenceWeights = new Dictionary<string, double>
                {
                    ["Safety"] = 1.0,
                    ["StructuralEfficiency"] = 0.9,
                    ["Cost"] = 0.7,
                    ["Constructability"] = 0.8,
                    ["Aesthetics"] = 0.3
                }
            });

            RegisterAgent(new CollaborativeAgent
            {
                AgentId = "MEP",
                Name = "MEP Agent",
                Discipline = "MEP",
                Priority = 4,
                Capabilities = new List<string>
                {
                    "HVAC Design", "Plumbing Design", "Electrical Design",
                    "Fire Protection", "Energy Efficiency", "System Coordination"
                },
                PreferenceWeights = new Dictionary<string, double>
                {
                    ["SystemPerformance"] = 0.9,
                    ["EnergyEfficiency"] = 0.85,
                    ["Maintainability"] = 0.8,
                    ["Cost"] = 0.7,
                    ["SpaceRequirements"] = 0.6
                }
            });

            RegisterAgent(new CollaborativeAgent
            {
                AgentId = "COST",
                Name = "Cost Agent",
                Discipline = "Cost Management",
                Priority = 4,
                Capabilities = new List<string>
                {
                    "Cost Estimation", "Value Engineering", "Budget Management",
                    "Life Cycle Costing", "Risk Assessment"
                },
                PreferenceWeights = new Dictionary<string, double>
                {
                    ["Cost"] = 1.0,
                    ["ValueForMoney"] = 0.9,
                    ["LifeCycleCost"] = 0.8,
                    ["Risk"] = 0.75
                }
            });

            RegisterAgent(new CollaborativeAgent
            {
                AgentId = "SUSTAIN",
                Name = "Sustainability Agent",
                Discipline = "Sustainability",
                Priority = 3,
                Capabilities = new List<string>
                {
                    "Energy Modeling", "Carbon Footprint", "Material Selection",
                    "Water Conservation", "Indoor Environmental Quality", "LEED Certification"
                },
                PreferenceWeights = new Dictionary<string, double>
                {
                    ["EnergyEfficiency"] = 1.0,
                    ["CarbonFootprint"] = 0.95,
                    ["MaterialSustainability"] = 0.9,
                    ["WaterEfficiency"] = 0.85,
                    ["IndoorQuality"] = 0.8
                }
            });

            RegisterAgent(new CollaborativeAgent
            {
                AgentId = "SAFETY",
                Name = "Safety Agent",
                Discipline = "Life Safety",
                Priority = 5,
                Capabilities = new List<string>
                {
                    "Fire Safety", "Egress Design", "Hazard Analysis",
                    "Code Compliance", "Emergency Systems"
                },
                PreferenceWeights = new Dictionary<string, double>
                {
                    ["LifeSafety"] = 1.0,
                    ["CodeCompliance"] = 0.95,
                    ["Accessibility"] = 0.9,
                    ["EmergencyResponse"] = 0.85
                }
            });
        }

        #endregion

        #region Public Methods - Agent Management

        public void RegisterAgent(CollaborativeAgent agent)
        {
            lock (_lockObject)
            {
                _agents[agent.AgentId] = agent;
            }
        }

        public void UpdateAgentPreferences(string agentId, Dictionary<string, double> preferences)
        {
            lock (_lockObject)
            {
                if (_agents.TryGetValue(agentId, out var agent))
                {
                    foreach (var pref in preferences)
                    {
                        agent.PreferenceWeights[pref.Key] = pref.Value;
                    }
                }
            }
        }

        public CollaborativeAgent GetAgent(string agentId)
        {
            lock (_lockObject)
            {
                return _agents.GetValueOrDefault(agentId);
            }
        }

        public IEnumerable<CollaborativeAgent> GetAgentsByDiscipline(string discipline)
        {
            lock (_lockObject)
            {
                return _agents.Values
                    .Where(a => a.Discipline.Equals(discipline, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
        }

        #endregion

        #region Public Methods - Negotiation

        /// <summary>
        /// Initiates a negotiation session for a design decision
        /// </summary>
        public async Task<NegotiationResult> NegotiateAsync(
            DesignDecision decision,
            IEnumerable<string> participantAgentIds,
            NegotiationOptions options = null,
            IProgress<NegotiationProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            options = options ?? new NegotiationOptions();

            var session = new NegotiationSession
            {
                SessionId = Guid.NewGuid().ToString(),
                Decision = decision,
                StartTime = DateTime.Now,
                Status = SessionStatus.Active
            };

            foreach (var agentId in participantAgentIds)
            {
                if (_agents.TryGetValue(agentId, out var agent))
                {
                    session.Participants.Add(agent);
                }
            }

            _sessions[session.SessionId] = session;

            progress?.Report(new NegotiationProgress { Phase = "Gathering Proposals", PercentComplete = 10 });

            // Phase 1: Gather proposals from all agents
            var proposals = new List<AgentProposal>();
            foreach (var agent in session.Participants)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var proposal = await GenerateProposalAsync(agent, decision, cancellationToken);
                proposals.Add(proposal);
                session.Proposals.Add(proposal);
            }

            progress?.Report(new NegotiationProgress { Phase = "Analyzing Conflicts", PercentComplete = 30 });

            // Phase 2: Identify conflicts
            var conflicts = _conflictResolver.IdentifyConflicts(proposals);
            session.Conflicts.AddRange(conflicts);

            progress?.Report(new NegotiationProgress { Phase = "Resolving Conflicts", PercentComplete = 50 });

            // Phase 3: Resolve conflicts through negotiation rounds
            int round = 0;
            while (conflicts.Any(c => c.Status == ConflictStatus.Unresolved) && round < options.MaxRounds)
            {
                cancellationToken.ThrowIfCancellationRequested();

                foreach (var conflict in conflicts.Where(c => c.Status == ConflictStatus.Unresolved))
                {
                    var resolution = await ResolveConflictAsync(conflict, session, options, cancellationToken);
                    conflict.Resolution = resolution;
                    conflict.Status = resolution.Success ? ConflictStatus.Resolved : ConflictStatus.Escalated;
                    session.Rounds.Add(new NegotiationRound
                    {
                        RoundNumber = round + 1,
                        ConflictId = conflict.ConflictId,
                        Resolution = resolution
                    });
                }

                round++;
                progress?.Report(new NegotiationProgress
                {
                    Phase = $"Negotiation Round {round}",
                    Round = round,
                    ConflictsRemaining = conflicts.Count(c => c.Status == ConflictStatus.Unresolved),
                    PercentComplete = 50 + round * 30.0 / options.MaxRounds
                });
            }

            progress?.Report(new NegotiationProgress { Phase = "Building Consensus", PercentComplete = 85 });

            // Phase 4: Build consensus
            var consensus = await _consensusBuilder.BuildConsensusAsync(session, options, cancellationToken);

            progress?.Report(new NegotiationProgress { Phase = "Finalizing", PercentComplete = 95 });

            // Finalize result
            session.EndTime = DateTime.Now;
            session.Status = SessionStatus.Completed;

            var result = new NegotiationResult
            {
                SessionId = session.SessionId,
                Decision = decision,
                FinalDecision = consensus.FinalDecision,
                Consensus = consensus,
                Conflicts = session.Conflicts,
                RoundsCompleted = round,
                Success = consensus.ConsensusReached,
                Explanation = GenerateExplanation(session, consensus)
            };

            // Log decision
            _decisionLogger.LogDecision(result);

            progress?.Report(new NegotiationProgress { Phase = "Complete", PercentComplete = 100 });

            return result;
        }

        /// <summary>
        /// Gets agent recommendations for a specific issue
        /// </summary>
        public async Task<AgentRecommendations> GetRecommendationsAsync(
            DesignIssue issue,
            CancellationToken cancellationToken = default)
        {
            var recommendations = new AgentRecommendations
            {
                IssueId = issue.IssueId,
                Issue = issue
            };

            foreach (var agent in _agents.Values.Where(a => IsRelevantToIssue(a, issue)))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var recommendation = await GenerateRecommendationAsync(agent, issue, cancellationToken);
                recommendations.Recommendations.Add(recommendation);
            }

            // Rank recommendations
            recommendations.RankedRecommendations = RankRecommendations(recommendations.Recommendations);

            return recommendations;
        }

        /// <summary>
        /// Validates a design decision against all agent criteria
        /// </summary>
        public async Task<ValidationResult> ValidateDecisionAsync(
            DesignDecision decision,
            object proposedValue,
            CancellationToken cancellationToken = default)
        {
            var result = new ValidationResult
            {
                DecisionId = decision.DecisionId,
                ProposedValue = proposedValue
            };

            foreach (var agent in _agents.Values)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var agentValidation = await ValidateWithAgentAsync(agent, decision, proposedValue, cancellationToken);
                result.AgentValidations[agent.AgentId] = agentValidation;

                if (!agentValidation.IsAcceptable && agent.Priority >= 4)
                {
                    result.HasCriticalObjections = true;
                }
            }

            result.OverallAcceptance = result.AgentValidations.Values.Count(v => v.IsAcceptable) /
                                       (double)result.AgentValidations.Count;

            result.IsValid = result.OverallAcceptance >= 0.7 && !result.HasCriticalObjections;

            return result;
        }

        #endregion

        #region Public Methods - Conflict Resolution

        /// <summary>
        /// Gets conflict resolution strategies
        /// </summary>
        public IEnumerable<ResolutionStrategy> GetResolutionStrategies(AgentConflict conflict)
        {
            return _conflictResolver.GetStrategies(conflict);
        }

        /// <summary>
        /// Applies a specific resolution strategy
        /// </summary>
        public async Task<ConflictResolution> ApplyStrategyAsync(
            AgentConflict conflict,
            ResolutionStrategy strategy,
            CancellationToken cancellationToken = default)
        {
            return await _conflictResolver.ApplyStrategyAsync(conflict, strategy, cancellationToken);
        }

        /// <summary>
        /// Escalates unresolved conflict to human decision maker
        /// </summary>
        public EscalationRequest EscalateConflict(AgentConflict conflict, string reason)
        {
            return new EscalationRequest
            {
                ConflictId = conflict.ConflictId,
                Reason = reason,
                ConflictingAgents = conflict.InvolvedAgents.Select(a => a.AgentId).ToList(),
                Options = conflict.ProposedValues.Select(p => new EscalationOption
                {
                    AgentId = p.Key,
                    ProposedValue = p.Value,
                    Rationale = _agents[p.Key].GetRationale(conflict.DecisionId)
                }).ToList(),
                RequestedAt = DateTime.Now
            };
        }

        #endregion

        #region Public Methods - Decision History

        /// <summary>
        /// Gets decision history for a project
        /// </summary>
        public IEnumerable<DecisionRecord> GetDecisionHistory(string projectId)
        {
            return _decisionLogger.GetHistory(projectId);
        }

        /// <summary>
        /// Gets decisions made by specific agent
        /// </summary>
        public IEnumerable<DecisionRecord> GetAgentDecisions(string agentId)
        {
            return _decisionLogger.GetByAgent(agentId);
        }

        /// <summary>
        /// Explains why a decision was made
        /// </summary>
        public DecisionExplanation ExplainDecision(string decisionId)
        {
            return _decisionLogger.GetExplanation(decisionId);
        }

        /// <summary>
        /// Learns from user overrides to improve future decisions
        /// </summary>
        public void LearnFromOverride(string decisionId, object overrideValue, string reason)
        {
            var record = _decisionLogger.GetDecision(decisionId);
            if (record == null) return;

            // Update agent preferences based on override
            foreach (var agentId in record.ParticipatingAgents)
            {
                if (_agents.TryGetValue(agentId, out var agent))
                {
                    agent.LearnFromFeedback(record.DecisionType, overrideValue, reason);
                }
            }
        }

        #endregion

        #region Private Methods - Proposal Generation

        private async Task<AgentProposal> GenerateProposalAsync(
            CollaborativeAgent agent,
            DesignDecision decision,
            CancellationToken cancellationToken)
        {
            await Task.Delay(1, cancellationToken); // Placeholder for actual AI processing

            var proposal = new AgentProposal
            {
                ProposalId = Guid.NewGuid().ToString(),
                AgentId = agent.AgentId,
                DecisionId = decision.DecisionId,
                ProposedValue = agent.GenerateProposal(decision),
                Confidence = agent.CalculateConfidence(decision),
                Rationale = agent.GetRationale(decision.DecisionId),
                Priority = agent.Priority,
                Constraints = agent.GetConstraints(decision),
                Preferences = agent.GetPreferences(decision)
            };

            return proposal;
        }

        private async Task<AgentRecommendation> GenerateRecommendationAsync(
            CollaborativeAgent agent,
            DesignIssue issue,
            CancellationToken cancellationToken)
        {
            await Task.Delay(1, cancellationToken);

            return new AgentRecommendation
            {
                AgentId = agent.AgentId,
                AgentName = agent.Name,
                Discipline = agent.Discipline,
                Recommendation = agent.GenerateRecommendation(issue),
                Confidence = 0.75 + new Random().NextDouble() * 0.2,
                Impact = agent.AssessImpact(issue),
                Rationale = $"Based on {agent.Discipline} analysis"
            };
        }

        private async Task<AgentValidation> ValidateWithAgentAsync(
            CollaborativeAgent agent,
            DesignDecision decision,
            object proposedValue,
            CancellationToken cancellationToken)
        {
            await Task.Delay(1, cancellationToken);

            return new AgentValidation
            {
                AgentId = agent.AgentId,
                IsAcceptable = agent.ValidateValue(decision, proposedValue),
                Concerns = agent.GetConcerns(decision, proposedValue),
                Score = agent.ScoreValue(decision, proposedValue)
            };
        }

        #endregion

        #region Private Methods - Conflict Resolution

        private async Task<ConflictResolution> ResolveConflictAsync(
            AgentConflict conflict,
            NegotiationSession session,
            NegotiationOptions options,
            CancellationToken cancellationToken)
        {
            var strategies = _conflictResolver.GetStrategies(conflict);
            var orderedStrategies = strategies.OrderByDescending(s => s.SuccessRate);

            foreach (var strategy in orderedStrategies)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var resolution = await _conflictResolver.ApplyStrategyAsync(conflict, strategy, cancellationToken);
                if (resolution.Success)
                {
                    return resolution;
                }
            }

            // If no strategy works, return compromise
            return new ConflictResolution
            {
                Success = false,
                Strategy = ResolutionStrategyType.Escalate,
                Message = "Unable to resolve conflict through negotiation"
            };
        }

        private bool IsRelevantToIssue(CollaborativeAgent agent, DesignIssue issue)
        {
            return agent.Capabilities.Any(c =>
                issue.Category.Contains(c, StringComparison.OrdinalIgnoreCase) ||
                c.Contains(issue.Category, StringComparison.OrdinalIgnoreCase));
        }

        private List<RankedRecommendation> RankRecommendations(List<AgentRecommendation> recommendations)
        {
            return recommendations
                .Select(r => new RankedRecommendation
                {
                    Recommendation = r,
                    Score = r.Confidence * _agents[r.AgentId].Priority,
                    Rank = 0
                })
                .OrderByDescending(r => r.Score)
                .Select((r, i) => { r.Rank = i + 1; return r; })
                .ToList();
        }

        private string GenerateExplanation(NegotiationSession session, ConsensusResult consensus)
        {
            var explanation = $"Decision reached after {session.Rounds.Count} negotiation rounds. ";

            if (consensus.ConsensusReached)
            {
                explanation += $"Consensus achieved with {consensus.AgreementLevel:P0} agreement. ";
                explanation += $"Final decision: {consensus.FinalDecision}. ";

                if (session.Conflicts.Any())
                {
                    explanation += $"Resolved {session.Conflicts.Count(c => c.Status == ConflictStatus.Resolved)} conflicts. ";
                }
            }
            else
            {
                explanation += "Full consensus not reached. ";
                if (session.Conflicts.Any(c => c.Status == ConflictStatus.Escalated))
                {
                    explanation += "Some conflicts require human decision. ";
                }
            }

            return explanation;
        }

        #endregion
    }

    #region Supporting Classes

    public class CollaborativeAgent
    {
        public string AgentId { get; set; }
        public string Name { get; set; }
        public string Discipline { get; set; }
        public int Priority { get; set; }
        public List<string> Capabilities { get; set; } = new List<string>();
        public Dictionary<string, double> PreferenceWeights { get; set; } = new Dictionary<string, double>();
        private readonly Dictionary<string, string> _rationales = new Dictionary<string, string>();

        public object GenerateProposal(DesignDecision decision)
        {
            // Generate proposal based on agent's discipline and preferences
            return decision.Options?.FirstOrDefault() ?? decision.DefaultValue;
        }

        public double CalculateConfidence(DesignDecision decision)
        {
            return 0.7 + new Random().NextDouble() * 0.25;
        }

        public string GetRationale(string decisionId)
        {
            return _rationales.GetValueOrDefault(decisionId, $"Based on {Discipline} best practices and preferences");
        }

        public List<string> GetConstraints(DesignDecision decision)
        {
            return new List<string> { $"{Discipline} code requirements", "Safety standards" };
        }

        public Dictionary<string, double> GetPreferences(DesignDecision decision)
        {
            return new Dictionary<string, double>(PreferenceWeights);
        }

        public string GenerateRecommendation(DesignIssue issue)
        {
            return $"Recommend addressing {issue.Category} through {Discipline} optimization";
        }

        public ImpactAssessment AssessImpact(DesignIssue issue)
        {
            return new ImpactAssessment
            {
                CostImpact = ImpactLevel.Medium,
                ScheduleImpact = ImpactLevel.Low,
                QualityImpact = ImpactLevel.High
            };
        }

        public bool ValidateValue(DesignDecision decision, object value)
        {
            return true; // Simplified validation
        }

        public List<string> GetConcerns(DesignDecision decision, object value)
        {
            return new List<string>();
        }

        public double ScoreValue(DesignDecision decision, object value)
        {
            return 0.8;
        }

        public void LearnFromFeedback(string decisionType, object overrideValue, string reason)
        {
            // Adjust preferences based on feedback
        }
    }

    public class NegotiationSession
    {
        public string SessionId { get; set; }
        public DesignDecision Decision { get; set; }
        public List<CollaborativeAgent> Participants { get; set; } = new List<CollaborativeAgent>();
        public List<AgentProposal> Proposals { get; set; } = new List<AgentProposal>();
        public List<AgentConflict> Conflicts { get; set; } = new List<AgentConflict>();
        public List<NegotiationRound> Rounds { get; set; } = new List<NegotiationRound>();
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public SessionStatus Status { get; set; }
    }

    public class DesignDecision
    {
        public string DecisionId { get; set; }
        public string DecisionType { get; set; }
        public string Description { get; set; }
        public List<object> Options { get; set; } = new List<object>();
        public object DefaultValue { get; set; }
        public List<string> AffectedElements { get; set; } = new List<string>();
        public Dictionary<string, object> Context { get; set; } = new Dictionary<string, object>();
    }

    public class DesignIssue
    {
        public string IssueId { get; set; }
        public string Category { get; set; }
        public string Domain { get; set; } // Design domain (Architecture, Structural, MEP, etc.)
        public string Description { get; set; }
        public IssueSeverity Severity { get; set; }
        public List<ElementId> AffectedElements { get; set; } = new List<ElementId>();
    }

    public class AgentProposal
    {
        public string ProposalId { get; set; }
        public string AgentId { get; set; }
        public string DecisionId { get; set; }
        public object ProposedValue { get; set; }
        public double Confidence { get; set; }
        public string Rationale { get; set; }
        public int Priority { get; set; }
        public List<string> Constraints { get; set; } = new List<string>();
        public Dictionary<string, double> Preferences { get; set; } = new Dictionary<string, double>();
    }

    public class AgentConflict
    {
        public string ConflictId { get; set; }
        public string DecisionId { get; set; }
        public ConflictType Type { get; set; }
        public List<CollaborativeAgent> InvolvedAgents { get; set; } = new List<CollaborativeAgent>();
        public Dictionary<string, object> ProposedValues { get; set; } = new Dictionary<string, object>();
        public ConflictStatus Status { get; set; }
        public ConflictResolution Resolution { get; set; }
        public string Description { get; set; }
    }

    public class ConflictResolution
    {
        public bool Success { get; set; }
        public ResolutionStrategyType Strategy { get; set; }
        public object ResolvedValue { get; set; }
        public string Message { get; set; }
        public Dictionary<string, double> AgentSatisfaction { get; set; } = new Dictionary<string, double>();
    }

    public class NegotiationRound
    {
        public int RoundNumber { get; set; }
        public string ConflictId { get; set; }
        public ConflictResolution Resolution { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    public class NegotiationOptions
    {
        public int MaxRounds { get; set; } = 5;
        public double ConsensusThreshold { get; set; } = 0.7;
        public bool AllowEscalation { get; set; } = true;
        public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);
    }

    public class NegotiationResult
    {
        public string SessionId { get; set; }
        public DesignDecision Decision { get; set; }
        public object FinalDecision { get; set; }
        public ConsensusResult Consensus { get; set; }
        public List<AgentConflict> Conflicts { get; set; } = new List<AgentConflict>();
        public int RoundsCompleted { get; set; }
        public bool Success { get; set; }
        public string Explanation { get; set; }
    }

    public class NegotiationProgress
    {
        public string Phase { get; set; }
        public int Round { get; set; }
        public int ConflictsRemaining { get; set; }
        public double PercentComplete { get; set; }
    }

    public class ConsensusResult
    {
        public bool ConsensusReached { get; set; }
        public double AgreementLevel { get; set; }
        public object FinalDecision { get; set; }
        public Dictionary<string, double> AgentAgreement { get; set; } = new Dictionary<string, double>();
        public List<string> DissentingAgents { get; set; } = new List<string>();
    }

    public class AgentRecommendations
    {
        public string IssueId { get; set; }
        public DesignIssue Issue { get; set; }
        public List<AgentRecommendation> Recommendations { get; set; } = new List<AgentRecommendation>();
        public List<RankedRecommendation> RankedRecommendations { get; set; } = new List<RankedRecommendation>();
    }

    public class AgentRecommendation
    {
        public string AgentId { get; set; }
        public string AgentName { get; set; }
        public string Discipline { get; set; }
        public string Recommendation { get; set; }
        public double Confidence { get; set; }
        public ImpactAssessment Impact { get; set; }
        public string Rationale { get; set; }
    }

    public class RankedRecommendation
    {
        public AgentRecommendation Recommendation { get; set; }
        public double Score { get; set; }
        public int Rank { get; set; }
    }

    public class ImpactAssessment
    {
        public ImpactLevel CostImpact { get; set; }
        public ImpactLevel ScheduleImpact { get; set; }
        public ImpactLevel QualityImpact { get; set; }
    }

    public class ValidationResult
    {
        public string DecisionId { get; set; }
        public object ProposedValue { get; set; }
        public bool IsValid { get; set; }
        public double OverallAcceptance { get; set; }
        public bool HasCriticalObjections { get; set; }
        public Dictionary<string, AgentValidation> AgentValidations { get; set; } = new Dictionary<string, AgentValidation>();
    }

    public class AgentValidation
    {
        public string AgentId { get; set; }
        public bool IsAcceptable { get; set; }
        public List<string> Concerns { get; set; } = new List<string>();
        public double Score { get; set; }
    }

    public class ResolutionStrategy
    {
        public string StrategyId { get; set; }
        public ResolutionStrategyType Type { get; set; }
        public string Description { get; set; }
        public double SuccessRate { get; set; }
        public List<string> ApplicableConflictTypes { get; set; } = new List<string>();
    }

    public class EscalationRequest
    {
        public string ConflictId { get; set; }
        public string Reason { get; set; }
        public List<string> ConflictingAgents { get; set; } = new List<string>();
        public List<EscalationOption> Options { get; set; } = new List<EscalationOption>();
        public DateTime RequestedAt { get; set; }
    }

    public class EscalationOption
    {
        public string AgentId { get; set; }
        public object ProposedValue { get; set; }
        public string Rationale { get; set; }
    }

    public class DecisionRecord
    {
        public string RecordId { get; set; }
        public string DecisionId { get; set; }
        public string DecisionType { get; set; }
        public object FinalValue { get; set; }
        public List<string> ParticipatingAgents { get; set; } = new List<string>();
        public DateTime Timestamp { get; set; }
        public string ProjectId { get; set; }
    }

    public class DecisionExplanation
    {
        public string DecisionId { get; set; }
        public string Summary { get; set; }
        public List<string> Factors { get; set; } = new List<string>();
        public Dictionary<string, string> AgentContributions { get; set; } = new Dictionary<string, string>();
    }

    public class ConflictResolver
    {
        public List<AgentConflict> IdentifyConflicts(List<AgentProposal> proposals)
        {
            var conflicts = new List<AgentConflict>();
            var byDecision = proposals.GroupBy(p => p.DecisionId);

            foreach (var group in byDecision)
            {
                var distinctValues = group.Select(p => p.ProposedValue?.ToString()).Distinct().ToList();
                if (distinctValues.Count > 1)
                {
                    conflicts.Add(new AgentConflict
                    {
                        ConflictId = Guid.NewGuid().ToString(),
                        DecisionId = group.Key,
                        Type = ConflictType.ValueDisagreement,
                        ProposedValues = group.ToDictionary(p => p.AgentId, p => p.ProposedValue),
                        Status = ConflictStatus.Unresolved,
                        Description = $"Conflicting proposals for decision {group.Key}"
                    });
                }
            }

            return conflicts;
        }

        public List<ResolutionStrategy> GetStrategies(AgentConflict conflict)
        {
            return new List<ResolutionStrategy>
            {
                new ResolutionStrategy
                {
                    StrategyId = "PRIORITY",
                    Type = ResolutionStrategyType.PriorityBased,
                    Description = "Use proposal from highest priority agent",
                    SuccessRate = 0.9
                },
                new ResolutionStrategy
                {
                    StrategyId = "COMPROMISE",
                    Type = ResolutionStrategyType.Compromise,
                    Description = "Find middle ground between proposals",
                    SuccessRate = 0.7
                },
                new ResolutionStrategy
                {
                    StrategyId = "VOTING",
                    Type = ResolutionStrategyType.Voting,
                    Description = "Weight-based voting among agents",
                    SuccessRate = 0.8
                }
            };
        }

        public async Task<ConflictResolution> ApplyStrategyAsync(
            AgentConflict conflict,
            ResolutionStrategy strategy,
            CancellationToken cancellationToken)
        {
            await Task.Delay(1, cancellationToken);

            return new ConflictResolution
            {
                Success = true,
                Strategy = strategy.Type,
                ResolvedValue = conflict.ProposedValues.Values.FirstOrDefault(),
                Message = $"Resolved using {strategy.Description}"
            };
        }
    }

    public class ConsensusBuilder
    {
        public async Task<ConsensusResult> BuildConsensusAsync(
            NegotiationSession session,
            NegotiationOptions options,
            CancellationToken cancellationToken)
        {
            await Task.Delay(1, cancellationToken);

            var result = new ConsensusResult
            {
                ConsensusReached = session.Conflicts.All(c => c.Status == ConflictStatus.Resolved),
                AgreementLevel = 0.85
            };

            if (session.Conflicts.Any(c => c.Resolution?.ResolvedValue != null))
            {
                result.FinalDecision = session.Conflicts
                    .Where(c => c.Resolution?.ResolvedValue != null)
                    .First().Resolution.ResolvedValue;
            }
            else if (session.Proposals.Any())
            {
                result.FinalDecision = session.Proposals
                    .OrderByDescending(p => p.Priority)
                    .First().ProposedValue;
            }

            foreach (var participant in session.Participants)
            {
                result.AgentAgreement[participant.AgentId] = 0.8 + new Random().NextDouble() * 0.15;
            }

            return result;
        }
    }

    public class DecisionLogger
    {
        private readonly List<DecisionRecord> _records = new List<DecisionRecord>();
        private readonly Dictionary<string, NegotiationResult> _results = new Dictionary<string, NegotiationResult>();

        public void LogDecision(NegotiationResult result)
        {
            var record = new DecisionRecord
            {
                RecordId = Guid.NewGuid().ToString(),
                DecisionId = result.Decision.DecisionId,
                DecisionType = result.Decision.DecisionType,
                FinalValue = result.FinalDecision,
                Timestamp = DateTime.Now
            };
            _records.Add(record);
            _results[result.SessionId] = result;
        }

        public IEnumerable<DecisionRecord> GetHistory(string projectId)
        {
            return _records.Where(r => r.ProjectId == projectId).ToList();
        }

        public IEnumerable<DecisionRecord> GetByAgent(string agentId)
        {
            return _records.Where(r => r.ParticipatingAgents.Contains(agentId)).ToList();
        }

        public DecisionRecord GetDecision(string decisionId)
        {
            return _records.FirstOrDefault(r => r.DecisionId == decisionId);
        }

        public DecisionExplanation GetExplanation(string decisionId)
        {
            return new DecisionExplanation
            {
                DecisionId = decisionId,
                Summary = "Decision reached through multi-agent negotiation"
            };
        }
    }

    public enum SessionStatus
    {
        Pending,
        Active,
        Completed,
        Cancelled
    }

    public enum ConflictType
    {
        ValueDisagreement,
        PriorityConflict,
        ConstraintViolation,
        ResourceContention
    }

    public enum ConflictStatus
    {
        Unresolved,
        Resolved,
        Escalated
    }

    public enum ResolutionStrategyType
    {
        PriorityBased,
        Compromise,
        Voting,
        Optimization,
        Escalate
    }

    public enum IssueSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }

    public enum ImpactLevel
    {
        None,
        Low,
        Medium,
        High,
        Critical
    }

    #endregion
}
