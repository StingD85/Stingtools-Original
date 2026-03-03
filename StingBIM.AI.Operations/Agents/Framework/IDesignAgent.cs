// StingBIM.AI.Agents.Framework.IDesignAgent
// Interface for specialist design agents
// Master Proposal Reference: Part 2.2 Strategy 3 - Swarm Intelligence (Multi-Agent Architecture)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StingBIM.AI.Agents.Framework
{
    /// <summary>
    /// Interface for specialist design agents in the swarm system.
    /// Each agent specializes in one domain but communicates with others.
    /// </summary>
    public interface IDesignAgent
    {
        string AgentId { get; }
        string Specialty { get; }
        float ExpertiseLevel { get; }
        bool IsActive { get; }

        Task<AgentOpinion> EvaluateAsync(
            DesignProposal proposal,
            EvaluationContext context = null,
            CancellationToken cancellationToken = default);

        Task<IEnumerable<AgentSuggestion>> SuggestAsync(
            DesignContext context,
            CancellationToken cancellationToken = default);

        void ReceiveFeedback(AgentOpinion otherOpinion);
        ValidationResult ValidateAction(DesignAction action);
    }

    public class AgentOpinion
    {
        public string AgentId { get; set; }
        public string Specialty { get; set; }
        public string AgentSpecialty { get => Specialty; set => Specialty = value; } // Alias for Specialty
        public float Score { get; set; } // 0-1 overall score
        public float Confidence { get; set; } = 0.7f; // Agent's confidence in its evaluation
        public List<DesignIssue> Issues { get; set; } = new List<DesignIssue>();
        public List<string> Strengths { get; set; } = new List<string>();
        public Dictionary<string, float> AspectScores { get; set; } = new Dictionary<string, float>();
        public string Summary { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public bool IsRevised { get; set; }
        public bool IsPositive { get; set; }
        public bool HasCriticalIssues { get; set; }

        public void UpdateComputedProperties()
        {
            IsPositive = Score >= 0.7f;
            HasCriticalIssues = Issues.Any(i => i.Severity == IssueSeverity.Critical || i.Severity == IssueSeverity.Major);
        }
    }

    public class DesignIssue
    {
        public string Code { get; set; }
        public string Description { get; set; }
        public IssueSeverity Severity { get; set; }
        public string Location { get; set; }
        public string ElementId { get; set; }
        public string Domain { get; set; }
        public string Standard { get; set; }
        public string SuggestedFix { get; set; }
        public string Recommendation { get; set; } // Recommended action
        public Dictionary<string, object> Details { get; set; } = new Dictionary<string, object>();
    }

    public enum IssueSeverity
    {
        Info,
        Minor,
        Warning,
        Major,
        Error,
        Critical
    }

    public class AgentSuggestion
    {
        public string AgentId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public SuggestionType Type { get; set; }
        public SuggestionPriority SuggestionPriority { get; set; }
        public float Confidence { get; set; }
        public float Impact { get; set; } // Expected improvement
        public SuggestionPriority Priority { get; set; } // Suggestion priority enum
        public int PriorityLevel { get => (int)Priority; set => Priority = (SuggestionPriority)value; }
        public float PriorityOrder { get; set; } // Ordering priority for suggestion application
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
        public List<ProposalModification> Modifications { get; set; } = new List<ProposalModification>();
        public List<string> Prerequisites { get; set; } = new List<string>();
    }

    public enum SuggestionType
    {
        Improvement,
        Alternative,
        Warning,
        BestPractice,
        CodeCompliance,
        CostSaving,
        Sustainability
    }

    public enum SuggestionPriority
    {
        Low,
        Medium,
        High,
        Critical
    }

    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<DesignIssue> Issues { get; set; } = new List<DesignIssue>();
        public List<string> Warnings { get; set; } = new List<string>();

        public static ValidationResult Valid() => new ValidationResult { IsValid = true };

        public static ValidationResult Invalid(string reason, IssueSeverity severity = IssueSeverity.Error)
        {
            return new ValidationResult
            {
                IsValid = false,
                Issues = new List<DesignIssue>
                {
                    new DesignIssue { Description = reason, Severity = severity }
                }
            };
        }
    }

    public class DesignProposal
    {
        public string ProposalId { get; set; }
        public string Description { get; set; }
        public List<ProposalElement> Elements { get; set; } = new List<ProposalElement>();
        public List<ProposalModification> Modifications { get; set; } = new List<ProposalModification>();
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Applies a modification to this proposal.
        /// </summary>
        public void ApplyModification(ProposalModification modification)
        {
            if (modification == null) return;
            Modifications.Add(modification);
        }

        /// <summary>
        /// Applies a proposed modification to this proposal.
        /// </summary>
        public void ApplyModification(ProposedModification modification)
        {
            if (modification != null)
            {
                Modifications.Add(new ProposalModification
                {
                    ElementId = modification.ElementId,
                    ModificationType = modification.ModificationType,
                    Type = modification.Type,
                    OldValues = modification.OldValues,
                    NewValues = modification.NewValues,
                    Parameters = modification.Parameters
                });
            }
        }

        /// <summary>
        /// Applies multiple modifications to this proposal.
        /// </summary>
        public void ApplyModifications(IEnumerable<ProposalModification> modifications)
        {
            if (modifications == null) return;
            Modifications.AddRange(modifications);
        }
    }

    public class ProposedElement
    {
        public string ElementType { get; set; }
        public string Type { get => ElementType; set => ElementType = value; }
        public string ElementId { get; set; }
        public string FamilyName { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
        public GeometryInfo Geometry { get; set; }
    }

    public class ProposalElement : ProposedElement
    {
        public new string Type { get => ElementType; set => ElementType = value; }
        public string Id { get => ElementId; set => ElementId = value; }
        public string Name { get; set; }
    }

    public class ProposedModification
    {
        public string ElementId { get; set; }
        public string ModificationType { get; set; }
        public ModificationType Type { get; set; }
        public Dictionary<string, object> OldValues { get; set; } = new Dictionary<string, object>();
        public Dictionary<string, object> NewValues { get; set; } = new Dictionary<string, object>();
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
    }

    public class ProposalModification : ProposedModification { }

    public enum ModificationType
    {
        Create,
        Modify,
        Delete,
        Move,
        Resize,
        Rotate,
        ChangeType,
        ChangeParameter
    }

    public class GeometryInfo
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double Length { get; set; }
        public double Rotation { get; set; }
    }

    public class EvaluationContext
    {
        public string ProjectType { get; set; }
        public string BuildingCode { get; set; }
        public string ClimateZone { get; set; }
        public Dictionary<string, object> ProjectParameters { get; set; } = new Dictionary<string, object>();
        public List<string> PreviousIssues { get; set; } = new List<string>();
    }

    public class DesignContext
    {
        public string CurrentTask { get; set; }
        public List<string> SelectedElementIds { get; set; } = new List<string>();
        public string UserIntent { get; set; }
        public Dictionary<string, object> CurrentState { get; set; } = new Dictionary<string, object>();
        public DesignProposal CurrentProposal { get; set; }
        public ConsensusResult ConsensusResult { get; set; }
        public SessionState SessionState { get; set; }
        public IDictionary<string, object> SessionData { get; set; }
    }

    public class SessionState
    {
        public string SessionId { get; set; }
        public DateTime StartTime { get; set; } = DateTime.Now;
        public int TurnCount { get; set; }
        public List<DesignProposal> ProposalHistory { get; set; } = new List<DesignProposal>();
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
    }

    public class DesignAction
    {
        public string ActionType { get; set; }
        public string ElementType { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
    }
}
