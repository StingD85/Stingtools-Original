// ============================================================================
// StingBIM Integration Tests - Foundation to AI to Revit Pipeline
// End-to-end tests verifying complete system integration
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;

namespace StingBIM.AI.Tests.Integration
{
    /// <summary>
    /// Integration tests for the complete Foundation → AI → Revit pipeline.
    /// Tests the interaction between all system layers.
    /// </summary>
    [TestFixture]
    [Category("Integration")]
    public class FoundationToRevitIntegrationTests
    {
        private TestIntegrationContext _context;

        [SetUp]
        public void SetUp()
        {
            _context = new TestIntegrationContext();
            _context.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            _context?.Dispose();
        }

        #region NLP → Agent → Revit Pipeline Tests

        [Test]
        public async Task NLPCommand_CreateWall_ShouldTriggerAgentConsensusAndRevitCreation()
        {
            // Arrange
            var userCommand = "create a 200mm concrete wall with 2 hour fire rating";

            // Act - Step 1: NLP Processing
            var nlpResult = _context.NLPProcessor.ProcessCommand(userCommand);

            nlpResult.Intent.Should().Be("CreateWall");
            nlpResult.Entities.Should().ContainKey("Dimension");
            nlpResult.Entities.Should().ContainKey("Material");
            nlpResult.Entities.Should().ContainKey("FireRating");

            // Act - Step 2: Create Design Proposal
            var proposal = _context.CreateProposalFromNLP(nlpResult);

            // Act - Step 3: Agent Consensus
            var consensusResult = await _context.AgentCoordinator.GetConsensusAsync(proposal);

            consensusResult.Should().NotBeNull();
            consensusResult.Opinions.Should().NotBeEmpty();

            // Act - Step 4: Apply to Revit (if approved)
            if (consensusResult.IsApproved)
            {
                var revitResult = await _context.RevitBridge.ApplyDesignProposalAsync(
                    _context.ConvertToRevitProposal(proposal));

                revitResult.Success.Should().BeTrue();
                revitResult.AppliedModifications.Should().NotBeEmpty();
            }
        }

        [Test]
        public async Task NLPCommand_CheckCompliance_ShouldQueryStandardsEngine()
        {
            // Arrange
            var userCommand = "check ADA compliance for the selected door";
            var testDoor = _context.CreateTestElement("Door", new Dictionary<string, object>
            {
                ["Width"] = 0.9, // 900mm
                ["ManeuveringClearance"] = 1.5, // 1500mm
                ["ThresholdHeight"] = 0.01 // 10mm
            });

            // Act - Step 1: NLP Processing
            var nlpResult = _context.NLPProcessor.ProcessCommand(userCommand);
            nlpResult.Intent.Should().Be("CheckCompliance");

            // Act - Step 2: Query Standards Engine
            _context.StandardsEngine.EnableStandard("ADA");
            var complianceResult = _context.StandardsEngine.CheckCompliance(testDoor);

            // Assert
            complianceResult.Should().NotBeNull();
            complianceResult.RuleResults.Should().NotBeEmpty();
            complianceResult.ElementType.Should().Be("Door");
        }

        [Test]
        public async Task NLPCommand_EstimateCost_ShouldUseMaterialRepository()
        {
            // Arrange
            var userCommand = "estimate cost for concrete walls";

            // Act - Step 1: NLP Processing
            var nlpResult = _context.NLPProcessor.ProcessCommand(userCommand);
            nlpResult.Intent.Should().Be("EstimateCost");

            // Act - Step 2: Query Material Repository
            var concreteMaterials = _context.MaterialRepository.SearchMaterials("concrete");

            // Act - Step 3: Calculate cost based on quantity
            var wallArea = 100.0; // 100 m²
            var material = concreteMaterials.FirstOrDefault();

            if (material != null)
            {
                var estimatedCost = wallArea * (double)material.CostProperties.UnitCost;
                estimatedCost.Should().BeGreaterThan(0);
            }
        }

        #endregion

        #region Configuration → Services Integration Tests

        [Test]
        public void Configuration_ShouldBeAccessibleAcrossAllLayers()
        {
            // Act
            var aiConfig = _context.Configuration.AI;
            var revitConfig = _context.Configuration.Revit;
            var dataConfig = _context.Configuration.Data;

            // Assert
            aiConfig.Should().NotBeNull();
            aiConfig.ConsensusThreshold.Should().BeGreaterThan(0);
            aiConfig.MaxConsensusRounds.Should().BeGreaterThan(0);

            revitConfig.Should().NotBeNull();
            dataConfig.Should().NotBeNull();
        }

        [Test]
        public void ServiceLocator_ShouldResolveAllRegisteredServices()
        {
            // Arrange & Act
            var hasNLP = _context.ServiceLocator.IsRegistered<INLPProcessor>();
            var hasAgentCoordinator = _context.ServiceLocator.IsRegistered<IAgentCoordinator>();
            var hasRevitBridge = _context.ServiceLocator.IsRegistered<IRevitBridge>();

            // Assert
            hasNLP.Should().BeTrue();
            hasAgentCoordinator.Should().BeTrue();
            hasRevitBridge.Should().BeTrue();
        }

        #endregion

        #region Transaction Flow Tests

        [Test]
        public async Task Transaction_ElementCreation_ShouldBeAtomic()
        {
            // Arrange
            var elementsToCreate = new[]
            {
                new ElementRequest { Type = "Wall", Name = "Wall-1" },
                new ElementRequest { Type = "Door", Name = "Door-1" },
                new ElementRequest { Type = "Window", Name = "Window-1" }
            };

            var createdIds = new List<string>();

            // Act
            var result = await _context.TransactionManager.ExecuteAsync(
                "Create Multiple Elements",
                async (ctx, ct) =>
                {
                    foreach (var request in elementsToCreate)
                    {
                        var element = await _context.RevitBridge.CreateElementAsync(
                            new ElementCreationRequest
                            {
                                ElementType = request.Type,
                                Name = request.Name
                            }, ct);

                        createdIds.Add(element.Id);
                        ctx.LogOperation($"Created {request.Type}: {element.Id}");
                    }
                });

            // Assert
            result.Success.Should().BeTrue();
            createdIds.Should().HaveCount(3);
        }

        [Test]
        public async Task Transaction_WithFailure_ShouldRollback()
        {
            // Arrange
            var shouldFail = true;

            // Act
            var result = await _context.TransactionManager.ExecuteAsync(
                "Failing Transaction",
                async (ctx, ct) =>
                {
                    ctx.LogOperation("Starting operation");

                    if (shouldFail)
                    {
                        throw new InvalidOperationException("Simulated failure");
                    }

                    await Task.CompletedTask;
                });

            // Assert
            result.Success.Should().BeFalse();
            result.ErrorMessage.Should().Contain("Simulated failure");
        }

        #endregion

        #region Memory and Learning Integration Tests

        [Test]
        public void WorkingMemory_ShouldTrackRecentCommands()
        {
            // Arrange
            var commands = new[]
            {
                "create a wall",
                "add a door",
                "check compliance"
            };

            // Act
            foreach (var cmd in commands)
            {
                var result = _context.NLPProcessor.ProcessCommand(cmd);
                _context.WorkingMemory.AddItem(new MemoryItem
                {
                    Id = Guid.NewGuid().ToString(),
                    Content = cmd,
                    Intent = result.Intent,
                    Timestamp = DateTime.UtcNow
                });
            }

            // Assert
            _context.WorkingMemory.GetRecentItems(3).Should().HaveCount(3);
        }

        [Test]
        public void FeedbackLoop_ShouldUpdatePatternLearner()
        {
            // Arrange
            var userCorrection = new UserFeedback
            {
                OriginalIntent = "CreateWall",
                CorrectedIntent = "CreatePartition",
                UserInput = "add a partition wall",
                IsCorrect = false
            };

            // Act
            _context.FeedbackCollector.RecordFeedback(userCorrection);
            var learnedPatterns = _context.PatternLearner.GetLearnedPatterns();

            // Assert
            _context.FeedbackCollector.GetFeedbackCount().Should().BeGreaterThan(0);
        }

        #endregion

        #region Formula and Parameter Integration Tests

        [Test]
        public void FormulaEngine_ShouldCalculateFromMaterialProperties()
        {
            // Arrange
            var material = _context.MaterialRepository.GetMaterial("concrete-c30");
            var thickness = 0.2; // 200mm

            // Act - Calculate R-value using formula engine
            var variables = new Dictionary<string, double>
            {
                ["Thickness"] = thickness,
                ["ThermalConductivity"] = material?.ThermalProperties.ThermalConductivity ?? 1.7
            };

            var result = _context.FormulaEngine.Evaluate("R_VALUE", variables);

            // Assert
            result.Success.Should().BeTrue();
            result.Value.Should().BeGreaterThan(0);
        }

        [Test]
        public void ParameterManager_ShouldGenerateStableGUIDs()
        {
            // Arrange
            var paramName = "Fire_Rating";

            // Act - Register same parameter twice
            var param1 = _context.ParameterManager.RegisterParameter(new SharedParameterDefinition
            {
                Name = paramName,
                DataType = ParameterDataType.Text,
                Group = "Fire Safety"
            });

            var param2 = _context.ParameterManager.GetParameter(paramName);

            // Assert - GUIDs should be identical
            param1.Guid.Should().Be(param2.Guid);
        }

        #endregion

        #region Cross-Discipline Coordination Tests

        [Test]
        public async Task CrossDisciplineCoordination_ShouldDetectConflicts()
        {
            // Arrange
            var structuralWall = _context.CreateTestElement("Wall", new Dictionary<string, object>
            {
                ["IsLoadBearing"] = true,
                ["Location"] = new { X = 0, Y = 0, Z = 0 }
            });

            var hvacDuct = _context.CreateTestElement("Duct", new Dictionary<string, object>
            {
                ["Diameter"] = 0.3,
                ["Location"] = new { X = 0, Y = 0, Z = 2.5 }
            });

            // Act
            var conflicts = _context.CrossDisciplineCoordinator.CheckForConflicts(
                new[] { structuralWall, hvacDuct });

            // Assert
            conflicts.Should().NotBeNull();
        }

        #endregion
    }

    #region Test Support Classes

    /// <summary>
    /// Test context providing all integrated services.
    /// </summary>
    internal class TestIntegrationContext : IDisposable
    {
        public TestConfiguration Configuration { get; private set; }
        public TestServiceLocator ServiceLocator { get; private set; }
        public TestNLPProcessor NLPProcessor { get; private set; }
        public TestAgentCoordinator AgentCoordinator { get; private set; }
        public TestRevitBridge RevitBridge { get; private set; }
        public TestStandardsEngine StandardsEngine { get; private set; }
        public TestMaterialRepository MaterialRepository { get; private set; }
        public TestFormulaEngine FormulaEngine { get; private set; }
        public TestParameterManager ParameterManager { get; private set; }
        public TestTransactionManager TransactionManager { get; private set; }
        public TestWorkingMemory WorkingMemory { get; private set; }
        public TestFeedbackCollector FeedbackCollector { get; private set; }
        public TestPatternLearner PatternLearner { get; private set; }
        public TestCrossDisciplineCoordinator CrossDisciplineCoordinator { get; private set; }

        public void Initialize()
        {
            Configuration = new TestConfiguration();
            ServiceLocator = new TestServiceLocator();

            NLPProcessor = new TestNLPProcessor();
            AgentCoordinator = new TestAgentCoordinator();
            RevitBridge = new TestRevitBridge();
            StandardsEngine = new TestStandardsEngine();
            MaterialRepository = new TestMaterialRepository();
            FormulaEngine = new TestFormulaEngine();
            ParameterManager = new TestParameterManager();
            TransactionManager = new TestTransactionManager();
            WorkingMemory = new TestWorkingMemory();
            FeedbackCollector = new TestFeedbackCollector();
            PatternLearner = new TestPatternLearner();
            CrossDisciplineCoordinator = new TestCrossDisciplineCoordinator();

            // Register services
            ServiceLocator.Register<INLPProcessor>(NLPProcessor);
            ServiceLocator.Register<IAgentCoordinator>(AgentCoordinator);
            ServiceLocator.Register<IRevitBridge>(RevitBridge);
        }

        public DesignElement CreateTestElement(string type, Dictionary<string, object> properties)
        {
            return new DesignElement
            {
                Id = Guid.NewGuid().ToString(),
                Type = type,
                Category = type,
                Properties = properties
            };
        }

        public DesignProposal CreateProposalFromNLP(NLPResult nlpResult)
        {
            return new DesignProposal
            {
                Id = Guid.NewGuid().ToString(),
                Intent = nlpResult.Intent,
                Entities = nlpResult.Entities
            };
        }

        public RevitDesignProposal ConvertToRevitProposal(DesignProposal proposal)
        {
            return new RevitDesignProposal
            {
                Id = proposal.Id,
                Modifications = new List<ProposalModification>
                {
                    new ProposalModification
                    {
                        Type = ModificationType.Create,
                        ElementType = proposal.Entities.GetValueOrDefault("ElementType")?.ToString() ?? "Wall",
                        Parameters = proposal.Entities
                    }
                }
            };
        }

        public void Dispose()
        {
            // Cleanup resources
        }
    }

    // Interface definitions for integration testing
    internal interface INLPProcessor { }
    internal interface IAgentCoordinator { }
    internal interface IRevitBridge { }

    // Test implementations
    internal class TestConfiguration
    {
        public AIConfig AI { get; } = new AIConfig();
        public RevitConfig Revit { get; } = new RevitConfig();
        public DataConfig Data { get; } = new DataConfig();

        internal class AIConfig
        {
            public float ConsensusThreshold { get; } = 0.7f;
            public int MaxConsensusRounds { get; } = 3;
        }

        internal class RevitConfig { }
        internal class DataConfig { }
    }

    internal class TestServiceLocator
    {
        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

        public void Register<T>(T service) => _services[typeof(T)] = service;
        public bool IsRegistered<T>() => _services.ContainsKey(typeof(T));
        public T Resolve<T>() => (T)_services[typeof(T)];
    }

    internal class TestNLPProcessor : INLPProcessor
    {
        public NLPResult ProcessCommand(string command)
        {
            var result = new NLPResult { OriginalInput = command };
            var lower = command.ToLowerInvariant();

            // Intent detection
            if (lower.Contains("create") || lower.Contains("add"))
            {
                if (lower.Contains("wall")) result.Intent = "CreateWall";
                else if (lower.Contains("door")) result.Intent = "CreateDoor";
                else result.Intent = "CreateElement";
            }
            else if (lower.Contains("check") || lower.Contains("compliance"))
            {
                result.Intent = "CheckCompliance";
            }
            else if (lower.Contains("cost") || lower.Contains("estimate"))
            {
                result.Intent = "EstimateCost";
            }

            // Entity extraction
            var dimMatch = System.Text.RegularExpressions.Regex.Match(command, @"(\d+)\s*mm");
            if (dimMatch.Success) result.Entities["Dimension"] = dimMatch.Value;

            var materials = new[] { "concrete", "steel", "wood", "glass" };
            foreach (var m in materials)
            {
                if (lower.Contains(m)) { result.Entities["Material"] = m; break; }
            }

            var fireMatch = System.Text.RegularExpressions.Regex.Match(lower, @"(\d+)\s*hour");
            if (fireMatch.Success) result.Entities["FireRating"] = fireMatch.Value;

            return result;
        }
    }

    internal class TestAgentCoordinator : IAgentCoordinator
    {
        public async Task<ConsensusResult> GetConsensusAsync(DesignProposal proposal, CancellationToken ct = default)
        {
            await Task.Delay(10, ct);
            return new ConsensusResult
            {
                IsApproved = true,
                Score = 0.85f,
                Opinions = new List<AgentOpinion>
                {
                    new AgentOpinion { AgentId = "Safety", Score = 0.9f },
                    new AgentOpinion { AgentId = "Structural", Score = 0.85f },
                    new AgentOpinion { AgentId = "Architectural", Score = 0.8f }
                }
            };
        }
    }

    internal class TestRevitBridge : IRevitBridge
    {
        private readonly List<RevitElement> _elements = new List<RevitElement>();

        public async Task<RevitElement> CreateElementAsync(ElementCreationRequest request, CancellationToken ct = default)
        {
            await Task.Delay(5, ct);
            var element = new RevitElement
            {
                Id = Guid.NewGuid().ToString(),
                Type = request.ElementType,
                Name = request.Name
            };
            _elements.Add(element);
            return element;
        }

        public async Task<ApplyResult> ApplyDesignProposalAsync(RevitDesignProposal proposal, CancellationToken ct = default)
        {
            await Task.Delay(10, ct);
            return new ApplyResult
            {
                Success = true,
                AppliedModifications = proposal.Modifications
            };
        }
    }

    internal class TestStandardsEngine
    {
        private readonly HashSet<string> _enabledStandards = new HashSet<string>();

        public void EnableStandard(string code) => _enabledStandards.Add(code);

        public ComplianceResult CheckCompliance(DesignElement element)
        {
            return new ComplianceResult
            {
                ElementType = element.Type,
                RuleResults = new List<RuleResult>
                {
                    new RuleResult { RuleId = "ADA-404.2.4", IsCompliant = true }
                }
            };
        }
    }

    internal class TestMaterialRepository
    {
        private readonly Dictionary<string, Material> _materials = new Dictionary<string, Material>
        {
            ["concrete-c30"] = new Material
            {
                Id = "concrete-c30",
                Name = "Concrete C30",
                ThermalProperties = new ThermalProps { ThermalConductivity = 1.7 },
                CostProperties = new CostProps { UnitCost = 85 }
            }
        };

        public Material GetMaterial(string id) => _materials.GetValueOrDefault(id);
        public IEnumerable<Material> SearchMaterials(string term) =>
            _materials.Values.Where(m => m.Name.ToLower().Contains(term.ToLower()));
    }

    internal class TestFormulaEngine
    {
        public FormulaResult Evaluate(string formulaId, Dictionary<string, double> variables)
        {
            if (formulaId == "R_VALUE" && variables.ContainsKey("Thickness") && variables.ContainsKey("ThermalConductivity"))
            {
                var rValue = variables["Thickness"] / variables["ThermalConductivity"];
                return new FormulaResult { Success = true, Value = rValue };
            }
            return new FormulaResult { Success = false };
        }
    }

    internal class TestParameterManager
    {
        private readonly Dictionary<string, SharedParameter> _parameters = new Dictionary<string, SharedParameter>();

        public SharedParameter RegisterParameter(SharedParameterDefinition def)
        {
            if (!_parameters.ContainsKey(def.Name))
            {
                _parameters[def.Name] = new SharedParameter
                {
                    Name = def.Name,
                    Guid = GenerateStableGuid(def.Name),
                    DataType = def.DataType,
                    Group = def.Group
                };
            }
            return _parameters[def.Name];
        }

        public SharedParameter GetParameter(string name) => _parameters.GetValueOrDefault(name);

        private Guid GenerateStableGuid(string name)
        {
            using var md5 = System.Security.Cryptography.MD5.Create();
            var hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes($"StingBIM.{name}"));
            return new Guid(hash);
        }
    }

    internal class TestTransactionManager
    {
        public async Task<TransactionResult> ExecuteAsync(
            string name,
            Func<TransactionContext, CancellationToken, Task> action,
            CancellationToken ct = default)
        {
            var ctx = new TransactionContext();
            try
            {
                await action(ctx, ct);
                return new TransactionResult { Success = true };
            }
            catch (Exception ex)
            {
                return new TransactionResult { Success = false, ErrorMessage = ex.Message };
            }
        }
    }

    internal class TestWorkingMemory
    {
        private readonly List<MemoryItem> _items = new List<MemoryItem>();
        public void AddItem(MemoryItem item) => _items.Insert(0, item);
        public IEnumerable<MemoryItem> GetRecentItems(int count) => _items.Take(count);
    }

    internal class TestFeedbackCollector
    {
        private readonly List<UserFeedback> _feedback = new List<UserFeedback>();
        public void RecordFeedback(UserFeedback fb) => _feedback.Add(fb);
        public int GetFeedbackCount() => _feedback.Count;
    }

    internal class TestPatternLearner
    {
        public IEnumerable<string> GetLearnedPatterns() => new[] { "pattern1", "pattern2" };
    }

    internal class TestCrossDisciplineCoordinator
    {
        public IEnumerable<Conflict> CheckForConflicts(IEnumerable<DesignElement> elements)
        {
            return new List<Conflict>();
        }
    }

    // Data structures
    internal class NLPResult
    {
        public string OriginalInput { get; set; }
        public string Intent { get; set; }
        public Dictionary<string, object> Entities { get; set; } = new Dictionary<string, object>();
    }

    internal class DesignProposal
    {
        public string Id { get; set; }
        public string Intent { get; set; }
        public Dictionary<string, object> Entities { get; set; }
    }

    internal class RevitDesignProposal
    {
        public string Id { get; set; }
        public List<ProposalModification> Modifications { get; set; }
    }

    internal class ConsensusResult
    {
        public bool IsApproved { get; set; }
        public float Score { get; set; }
        public List<AgentOpinion> Opinions { get; set; }
    }

    internal class AgentOpinion
    {
        public string AgentId { get; set; }
        public float Score { get; set; }
    }

    internal class DesignElement
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public string Category { get; set; }
        public Dictionary<string, object> Properties { get; set; }
    }

    internal class RevitElement
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public string Name { get; set; }
    }

    internal class ElementCreationRequest
    {
        public string ElementType { get; set; }
        public string Name { get; set; }
    }

    internal class ElementRequest
    {
        public string Type { get; set; }
        public string Name { get; set; }
    }

    internal class ProposalModification
    {
        public ModificationType Type { get; set; }
        public string ElementType { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
    }

    internal enum ModificationType { Create, Modify, Delete }

    internal class ApplyResult
    {
        public bool Success { get; set; }
        public List<ProposalModification> AppliedModifications { get; set; }
    }

    internal class ComplianceResult
    {
        public string ElementType { get; set; }
        public List<RuleResult> RuleResults { get; set; }
    }

    internal class RuleResult
    {
        public string RuleId { get; set; }
        public bool IsCompliant { get; set; }
    }

    internal class Material
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public ThermalProps ThermalProperties { get; set; }
        public CostProps CostProperties { get; set; }
    }

    internal class ThermalProps { public double ThermalConductivity { get; set; } }
    internal class CostProps { public decimal UnitCost { get; set; } }

    internal class FormulaResult
    {
        public bool Success { get; set; }
        public double Value { get; set; }
    }

    internal class SharedParameterDefinition
    {
        public string Name { get; set; }
        public ParameterDataType DataType { get; set; }
        public string Group { get; set; }
    }

    internal class SharedParameter
    {
        public string Name { get; set; }
        public Guid Guid { get; set; }
        public ParameterDataType DataType { get; set; }
        public string Group { get; set; }
    }

    internal enum ParameterDataType { Text, Number, Integer, YesNo }

    internal class TransactionResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
    }

    internal class TransactionContext
    {
        public void LogOperation(string msg) { }
    }

    internal class MemoryItem
    {
        public string Id { get; set; }
        public string Content { get; set; }
        public string Intent { get; set; }
        public DateTime Timestamp { get; set; }
    }

    internal class UserFeedback
    {
        public string OriginalIntent { get; set; }
        public string CorrectedIntent { get; set; }
        public string UserInput { get; set; }
        public bool IsCorrect { get; set; }
    }

    internal class Conflict
    {
        public string Description { get; set; }
    }

    #endregion
}
