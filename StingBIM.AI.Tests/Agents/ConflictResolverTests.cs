// ============================================================================
// StingBIM AI Tests - Conflict Resolver Tests
// Unit tests for the weighted conflict resolution system
// ============================================================================

using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using StingBIM.AI.Agents.Framework;

namespace StingBIM.AI.Tests.Agents
{
    [TestFixture]
    public class ConflictResolverTests
    {
        private ConflictResolver _resolver;

        [SetUp]
        public void SetUp()
        {
            _resolver = new ConflictResolver();
        }

        #region Safety Override Tests

        [Test]
        public void ResolveConflict_SafetyCriticalIssue_ShouldOverride()
        {
            // Arrange
            var opinions = new List<AgentOpinion>
            {
                CreateOpinion("ArchAgent", "Architectural", 0.9f, false),
                CreateOpinion("SafetyAgent", "Safety", 0.3f, true), // Critical issue
                CreateOpinion("CostAgent", "Cost", 0.85f, false)
            };

            var proposal = new DesignProposal { ProposalId = "test" };

            // Act
            var resolution = _resolver.ResolveConflict(opinions, "General", proposal);

            // Assert
            resolution.Method.Should().Be(ResolutionMethod.SafetyOverride);
            resolution.WinningOpinion.AgentId.Should().Be("SafetyAgent");
            resolution.Confidence.Should().Be(1.0f);
        }

        [Test]
        public void ResolveConflict_StructuralCriticalIssue_ShouldOverride()
        {
            // Arrange
            var opinions = new List<AgentOpinion>
            {
                CreateOpinion("ArchAgent", "Architectural", 0.9f, false),
                CreateOpinion("StructuralAgent", "Structural", 0.2f, true), // Critical issue
                CreateOpinion("CostAgent", "Cost", 0.95f, false)
            };

            var proposal = new DesignProposal { ProposalId = "test" };

            // Act
            var resolution = _resolver.ResolveConflict(opinions, "LoadBearing", proposal);

            // Assert
            resolution.Method.Should().Be(ResolutionMethod.SafetyOverride);
            resolution.WinningOpinion.AgentSpecialty.Should().Be("Structural");
        }

        #endregion

        #region Domain Expertise Tests

        [Test]
        public void ResolveConflict_StructuralDomain_ShouldDeferToStructuralExpert()
        {
            // Arrange
            var opinions = new List<AgentOpinion>
            {
                CreateOpinion("ArchAgent", "Architectural", 0.7f, false, 0.6f),
                CreateOpinion("StructuralAgent", "Structural", 0.85f, false, 0.95f), // High confidence
                CreateOpinion("CostAgent", "Cost", 0.9f, false, 0.8f)
            };

            var proposal = new DesignProposal { ProposalId = "test" };

            // Act
            var resolution = _resolver.ResolveConflict(opinions, "LoadBearing", proposal);

            // Assert
            resolution.Method.Should().Be(ResolutionMethod.DomainExpertise);
            resolution.WinningOpinion.AgentId.Should().Be("StructuralAgent");
        }

        [Test]
        public void ResolveConflict_HVACDomain_ShouldDeferToMechanicalExpert()
        {
            // Arrange
            var opinions = new List<AgentOpinion>
            {
                CreateOpinion("ArchAgent", "Architectural", 0.8f, false, 0.7f),
                CreateOpinion("MEPAgent", "Mechanical", 0.75f, false, 0.9f),
                CreateOpinion("CostAgent", "Cost", 0.85f, false, 0.8f)
            };

            var proposal = new DesignProposal { ProposalId = "test" };

            // Act
            var resolution = _resolver.ResolveConflict(opinions, "HVAC", proposal);

            // Assert
            resolution.Method.Should().Be(ResolutionMethod.DomainExpertise);
            resolution.WinningOpinion.AgentSpecialty.Should().Be("Mechanical");
        }

        #endregion

        #region Weighted Voting Tests

        [Test]
        public void ResolveConflict_NoDomainExpert_ShouldUseWeightedVoting()
        {
            // Arrange
            var opinions = new List<AgentOpinion>
            {
                CreateOpinion("ArchAgent", "Architectural", 0.8f, false, 0.7f),
                CreateOpinion("CostAgent", "Cost", 0.6f, false, 0.8f),
                CreateOpinion("SustainabilityAgent", "Sustainability", 0.75f, false, 0.75f)
            };

            var proposal = new DesignProposal { ProposalId = "test" };

            // Act
            var resolution = _resolver.ResolveConflict(opinions, "Aesthetic", proposal);

            // Assert
            resolution.Method.Should().Be(ResolutionMethod.WeightedVoting);
            resolution.VoteBreakdown.Should().NotBeEmpty();
        }

        [Test]
        public void ResolveConflict_LowConfidence_ShouldRequireHumanReview()
        {
            // Arrange
            var opinions = new List<AgentOpinion>
            {
                CreateOpinion("ArchAgent", "Architectural", 0.5f, false, 0.3f),
                CreateOpinion("CostAgent", "Cost", 0.4f, false, 0.3f),
                CreateOpinion("SustainabilityAgent", "Sustainability", 0.45f, false, 0.3f)
            };

            var proposal = new DesignProposal { ProposalId = "test" };

            // Act
            var resolution = _resolver.ResolveConflict(opinions, "General", proposal);

            // Assert
            resolution.RequiresHumanReview.Should().BeTrue();
        }

        #endregion

        #region Multiple Conflicts Tests

        [Test]
        public void ResolveAllConflicts_MultipleDomainsInConflict_ShouldResolveEach()
        {
            // Arrange
            var opinions = new List<AgentOpinion>
            {
                new AgentOpinion
                {
                    AgentId = "SafetyAgent",
                    AgentSpecialty = "Safety",
                    Score = 0.6f,
                    Confidence = 0.9f,
                    Issues = new List<DesignIssue>
                    {
                        new DesignIssue { Description = "Egress issue", Domain = "Egress" }
                    }
                },
                new AgentOpinion
                {
                    AgentId = "StructuralAgent",
                    AgentSpecialty = "Structural",
                    Score = 0.5f,
                    Confidence = 0.85f,
                    Issues = new List<DesignIssue>
                    {
                        new DesignIssue { Description = "Load concern", Domain = "LoadBearing" }
                    }
                }
            };

            var proposal = new DesignProposal { ProposalId = "test" };

            // Act
            var resolutions = _resolver.ResolveAllConflicts(opinions, proposal);

            // Assert
            resolutions.Should().HaveCount(2);
            resolutions.Should().Contain(r => r.Domain == "Egress");
            resolutions.Should().Contain(r => r.Domain == "LoadBearing");
        }

        #endregion

        #region Helper Methods

        private AgentOpinion CreateOpinion(
            string agentId,
            string specialty,
            float score,
            bool hasCritical,
            float confidence = 0.8f)
        {
            return new AgentOpinion
            {
                AgentId = agentId,
                AgentSpecialty = specialty,
                Score = score,
                Confidence = confidence,
                HasCriticalIssues = hasCritical,
                Issues = hasCritical
                    ? new List<DesignIssue> { new DesignIssue { Description = "Critical" } }
                    : new List<DesignIssue>()
            };
        }

        #endregion
    }
}
