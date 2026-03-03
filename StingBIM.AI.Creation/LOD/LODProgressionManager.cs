using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace StingBIM.AI.Creation.LOD
{
    /// <summary>
    /// Manages Level of Development (LOD) progression from LOD 100 to LOD 500.
    /// Tracks element maturity, validates LOD requirements, and guides progression.
    /// </summary>
    public class LODProgressionManager
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly LODSettings _settings;
        private readonly LODRequirementsDatabase _requirementsDb;
        private readonly LODValidator _validator;

        public LODProgressionManager(LODSettings settings = null)
        {
            _settings = settings ?? new LODSettings();
            _requirementsDb = new LODRequirementsDatabase();
            _validator = new LODValidator(_requirementsDb);
        }

        /// <summary>
        /// Analyze current LOD status of all elements in the model.
        /// </summary>
        public async Task<LODAnalysisResult> AnalyzeModelLODAsync(
            BIMModel model,
            IProgress<LODProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            Logger.Info("Analyzing model LOD status");
            var result = new LODAnalysisResult { ModelId = model.Id };

            var elements = model.GetAllElements();
            int processed = 0;

            foreach (var element in elements)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var elementLOD = await AnalyzeElementLODAsync(element, cancellationToken);
                result.ElementResults[element.Id] = elementLOD;

                processed++;
                progress?.Report(new LODProgress
                {
                    Phase = "Analyzing elements",
                    PercentComplete = (processed * 100) / elements.Count,
                    CurrentElement = element.Name
                });
            }

            result.Summary = CalculateLODSummary(result.ElementResults.Values.ToList());
            Logger.Info($"LOD analysis complete: {result.Summary.AverageLOD:F0} average LOD");
            return result;
        }

        /// <summary>
        /// Analyze LOD of a specific element.
        /// </summary>
        public async Task<ElementLODResult> AnalyzeElementLODAsync(
            BIMElement element,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                var requirements = _requirementsDb.GetRequirements(element.Category);
                var result = new ElementLODResult
                {
                    ElementId = element.Id,
                    ElementName = element.Name,
                    Category = element.Category
                };

                // Check each LOD level
                foreach (var level in Enum.GetValues<LODLevel>())
                {
                    var levelReqs = requirements.GetRequirementsForLevel(level);
                    var validation = _validator.ValidateElement(element, levelReqs);
                    result.LevelValidations[level] = validation;

                    if (validation.IsMet)
                        result.CurrentLOD = level;
                }

                result.MissingForNextLevel = GetMissingRequirements(element, result.CurrentLOD);
                return result;
            }, cancellationToken);
        }

        /// <summary>
        /// Get roadmap to progress element to target LOD.
        /// </summary>
        public LODProgressionRoadmap GetProgressionRoadmap(
            BIMElement element,
            LODLevel targetLOD)
        {
            var currentResult = AnalyzeElementLODAsync(element, CancellationToken.None).Result;
            var roadmap = new LODProgressionRoadmap
            {
                ElementId = element.Id,
                CurrentLOD = currentResult.CurrentLOD,
                TargetLOD = targetLOD
            };

            if (currentResult.CurrentLOD >= targetLOD)
            {
                roadmap.IsComplete = true;
                return roadmap;
            }

            // Build steps for each LOD level between current and target
            var currentLevel = currentResult.CurrentLOD;
            while (currentLevel < targetLOD)
            {
                var nextLevel = (LODLevel)((int)currentLevel + 100);
                var requirements = _requirementsDb.GetRequirements(element.Category);
                var nextReqs = requirements.GetRequirementsForLevel(nextLevel);

                var step = new LODProgressionStep
                {
                    FromLOD = currentLevel,
                    ToLOD = nextLevel,
                    RequiredActions = GetActionsForProgression(element, nextReqs)
                };

                roadmap.Steps.Add(step);
                currentLevel = nextLevel;
            }

            return roadmap;
        }

        /// <summary>
        /// Batch upgrade elements to target LOD where possible.
        /// </summary>
        public async Task<LODUpgradeResult> UpgradeElementsAsync(
            List<BIMElement> elements,
            LODLevel targetLOD,
            LODUpgradeOptions options,
            IProgress<LODProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            Logger.Info($"Upgrading {elements.Count} elements to LOD {(int)targetLOD}");
            var result = new LODUpgradeResult { TargetLOD = targetLOD };
            int processed = 0;

            foreach (var element in elements)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var elementResult = await UpgradeElementAsync(element, targetLOD, options, cancellationToken);
                result.ElementResults.Add(elementResult);

                processed++;
                progress?.Report(new LODProgress
                {
                    Phase = "Upgrading elements",
                    PercentComplete = (processed * 100) / elements.Count,
                    CurrentElement = element.Name
                });
            }

            result.Summary = new LODUpgradeSummary
            {
                TotalElements = elements.Count,
                SuccessfulUpgrades = result.ElementResults.Count(r => r.Success),
                PartialUpgrades = result.ElementResults.Count(r => r.PartialSuccess),
                FailedUpgrades = result.ElementResults.Count(r => !r.Success && !r.PartialSuccess)
            };

            return result;
        }

        /// <summary>
        /// Upgrade single element to target LOD.
        /// </summary>
        public async Task<ElementUpgradeResult> UpgradeElementAsync(
            BIMElement element,
            LODLevel targetLOD,
            LODUpgradeOptions options,
            CancellationToken cancellationToken = default)
        {
            var result = new ElementUpgradeResult
            {
                ElementId = element.Id,
                OriginalLOD = (await AnalyzeElementLODAsync(element, cancellationToken)).CurrentLOD,
                TargetLOD = targetLOD
            };

            try
            {
                var roadmap = GetProgressionRoadmap(element, targetLOD);

                foreach (var step in roadmap.Steps)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    foreach (var action in step.RequiredActions)
                    {
                        var actionResult = await ExecuteUpgradeActionAsync(element, action, options, cancellationToken);
                        result.ActionsPerformed.Add(actionResult);

                        if (!actionResult.Success && !options.ContinueOnFailure)
                        {
                            result.Success = false;
                            result.FailureReason = actionResult.Error;
                            return result;
                        }
                    }
                }

                var finalAnalysis = await AnalyzeElementLODAsync(element, cancellationToken);
                result.AchievedLOD = finalAnalysis.CurrentLOD;
                result.Success = finalAnalysis.CurrentLOD >= targetLOD;
                result.PartialSuccess = finalAnalysis.CurrentLOD > result.OriginalLOD && !result.Success;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to upgrade element {element.Id}");
                result.Success = false;
                result.FailureReason = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Generate LOD compliance report for a project milestone.
        /// </summary>
        public async Task<LODComplianceReport> GenerateComplianceReportAsync(
            BIMModel model,
            LODMilestone milestone,
            CancellationToken cancellationToken = default)
        {
            Logger.Info($"Generating LOD compliance report for milestone: {milestone.Name}");
            var analysis = await AnalyzeModelLODAsync(model, null, cancellationToken);

            var report = new LODComplianceReport
            {
                ModelId = model.Id,
                Milestone = milestone,
                GeneratedDate = DateTime.UtcNow
            };

            foreach (var (elementId, elementResult) in analysis.ElementResults)
            {
                var requiredLOD = milestone.GetRequiredLOD(elementResult.Category);
                var compliance = new ElementComplianceStatus
                {
                    ElementId = elementId,
                    Category = elementResult.Category,
                    RequiredLOD = requiredLOD,
                    ActualLOD = elementResult.CurrentLOD,
                    IsCompliant = elementResult.CurrentLOD >= requiredLOD
                };

                if (!compliance.IsCompliant)
                {
                    compliance.GapAnalysis = GetMissingRequirements(
                        model.GetElement(elementId), requiredLOD);
                }

                report.ElementCompliance.Add(compliance);
            }

            report.Summary = new ComplianceSummary
            {
                TotalElements = report.ElementCompliance.Count,
                CompliantElements = report.ElementCompliance.Count(c => c.IsCompliant),
                NonCompliantElements = report.ElementCompliance.Count(c => !c.IsCompliant),
                CompliancePercentage = report.ElementCompliance.Count > 0
                    ? (report.ElementCompliance.Count(c => c.IsCompliant) * 100.0) / report.ElementCompliance.Count
                    : 100
            };

            return report;
        }

        /// <summary>
        /// Set LOD targets for project phases.
        /// </summary>
        public LODPlan CreateLODPlan(ProjectPhases phases, LODPlanOptions options)
        {
            var plan = new LODPlan { ProjectId = phases.ProjectId };

            // Schematic Design - LOD 100-200
            plan.PhaseTargets.Add(new PhaseTarget
            {
                Phase = ProjectPhase.SchematicDesign,
                DefaultLOD = LODLevel.LOD200,
                CategoryOverrides = new Dictionary<ElementCategory, LODLevel>
                {
                    { ElementCategory.Walls, LODLevel.LOD200 },
                    { ElementCategory.Floors, LODLevel.LOD200 },
                    { ElementCategory.Roofs, LODLevel.LOD100 },
                    { ElementCategory.Columns, LODLevel.LOD200 },
                    { ElementCategory.MEP, LODLevel.LOD100 }
                }
            });

            // Design Development - LOD 300
            plan.PhaseTargets.Add(new PhaseTarget
            {
                Phase = ProjectPhase.DesignDevelopment,
                DefaultLOD = LODLevel.LOD300,
                CategoryOverrides = new Dictionary<ElementCategory, LODLevel>
                {
                    { ElementCategory.Structural, LODLevel.LOD350 },
                    { ElementCategory.MEP, LODLevel.LOD300 }
                }
            });

            // Construction Documents - LOD 350-400
            plan.PhaseTargets.Add(new PhaseTarget
            {
                Phase = ProjectPhase.ConstructionDocuments,
                DefaultLOD = LODLevel.LOD350,
                CategoryOverrides = new Dictionary<ElementCategory, LODLevel>
                {
                    { ElementCategory.Structural, LODLevel.LOD400 },
                    { ElementCategory.MEP, LODLevel.LOD350 },
                    { ElementCategory.Specialty, LODLevel.LOD400 }
                }
            });

            // Construction - LOD 400
            plan.PhaseTargets.Add(new PhaseTarget
            {
                Phase = ProjectPhase.Construction,
                DefaultLOD = LODLevel.LOD400
            });

            // As-Built/FM - LOD 500
            plan.PhaseTargets.Add(new PhaseTarget
            {
                Phase = ProjectPhase.AsBuilt,
                DefaultLOD = LODLevel.LOD500
            });

            return plan;
        }

        #region Private Methods

        private List<LODRequirement> GetMissingRequirements(BIMElement element, LODLevel currentLOD)
        {
            if (currentLOD >= LODLevel.LOD500) return new List<LODRequirement>();

            var nextLevel = (LODLevel)((int)currentLOD + 100);
            var requirements = _requirementsDb.GetRequirements(element.Category);
            var nextReqs = requirements.GetRequirementsForLevel(nextLevel);

            return nextReqs.Where(r => !_validator.IsRequirementMet(element, r)).ToList();
        }

        private List<LODUpgradeAction> GetActionsForProgression(BIMElement element, List<LODRequirement> requirements)
        {
            var actions = new List<LODUpgradeAction>();

            foreach (var req in requirements)
            {
                if (_validator.IsRequirementMet(element, req)) continue;

                actions.Add(new LODUpgradeAction
                {
                    Requirement = req,
                    ActionType = DetermineActionType(req),
                    Description = $"Add {req.Name}: {req.Description}"
                });
            }

            return actions;
        }

        private LODActionType DetermineActionType(LODRequirement requirement)
        {
            return requirement.Type switch
            {
                RequirementType.Geometry => LODActionType.AddGeometry,
                RequirementType.Parameter => LODActionType.AddParameter,
                RequirementType.Material => LODActionType.AssignMaterial,
                RequirementType.Connection => LODActionType.DefineConnection,
                RequirementType.Documentation => LODActionType.AddDocumentation,
                _ => LODActionType.Manual
            };
        }

        private async Task<ActionResult> ExecuteUpgradeActionAsync(
            BIMElement element,
            LODUpgradeAction action,
            LODUpgradeOptions options,
            CancellationToken cancellationToken)
        {
            var result = new ActionResult { Action = action };

            try
            {
                switch (action.ActionType)
                {
                    case LODActionType.AddParameter:
                        await AddParameterAsync(element, action.Requirement, options, cancellationToken);
                        break;
                    case LODActionType.AddGeometry:
                        await AddGeometryDetailAsync(element, action.Requirement, options, cancellationToken);
                        break;
                    case LODActionType.AssignMaterial:
                        await AssignMaterialAsync(element, action.Requirement, options, cancellationToken);
                        break;
                    case LODActionType.DefineConnection:
                        await DefineConnectionAsync(element, action.Requirement, options, cancellationToken);
                        break;
                    case LODActionType.AddDocumentation:
                        await AddDocumentationAsync(element, action.Requirement, options, cancellationToken);
                        break;
                    default:
                        result.RequiresManualAction = true;
                        break;
                }

                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
            }

            return result;
        }

        private Task AddParameterAsync(BIMElement element, LODRequirement req, LODUpgradeOptions options, CancellationToken ct)
        {
            // Add required parameter with default or inferred value
            if (options.AutoPopulateParameters)
            {
                var value = InferParameterValue(element, req);
                element.SetParameter(req.Name, value);
            }
            else
            {
                element.AddParameter(req.Name, req.DataType, null);
            }
            return Task.CompletedTask;
        }

        private Task AddGeometryDetailAsync(BIMElement element, LODRequirement req, LODUpgradeOptions options, CancellationToken ct)
        {
            // This would integrate with Revit API to add geometry detail
            Logger.Debug($"Adding geometry detail: {req.Name} to element {element.Id}");
            return Task.CompletedTask;
        }

        private Task AssignMaterialAsync(BIMElement element, LODRequirement req, LODUpgradeOptions options, CancellationToken ct)
        {
            if (options.AutoAssignMaterials)
            {
                var material = InferMaterial(element);
                element.SetMaterial(material);
            }
            return Task.CompletedTask;
        }

        private Task DefineConnectionAsync(BIMElement element, LODRequirement req, LODUpgradeOptions options, CancellationToken ct)
        {
            // Define structural/MEP connections
            Logger.Debug($"Defining connection: {req.Name} for element {element.Id}");
            return Task.CompletedTask;
        }

        private Task AddDocumentationAsync(BIMElement element, LODRequirement req, LODUpgradeOptions options, CancellationToken ct)
        {
            // Add documentation reference
            element.AddDocumentationLink(req.Name, req.Description);
            return Task.CompletedTask;
        }

        private object InferParameterValue(BIMElement element, LODRequirement req)
        {
            // Infer value based on element properties and requirement
            return req.DefaultValue;
        }

        private string InferMaterial(BIMElement element)
        {
            return element.Category switch
            {
                ElementCategory.Walls => "Concrete",
                ElementCategory.Floors => "Concrete",
                ElementCategory.Columns => "Steel",
                ElementCategory.Beams => "Steel",
                ElementCategory.Roofs => "Metal Deck",
                _ => "Generic"
            };
        }

        private LODSummary CalculateLODSummary(List<ElementLODResult> results)
        {
            return new LODSummary
            {
                TotalElements = results.Count,
                LOD100Count = results.Count(r => r.CurrentLOD == LODLevel.LOD100),
                LOD200Count = results.Count(r => r.CurrentLOD == LODLevel.LOD200),
                LOD300Count = results.Count(r => r.CurrentLOD == LODLevel.LOD300),
                LOD350Count = results.Count(r => r.CurrentLOD == LODLevel.LOD350),
                LOD400Count = results.Count(r => r.CurrentLOD == LODLevel.LOD400),
                LOD500Count = results.Count(r => r.CurrentLOD == LODLevel.LOD500),
                AverageLOD = results.Any() ? results.Average(r => (int)r.CurrentLOD) : 0
            };
        }

        #endregion
    }

    #region LOD Requirements Database

    internal class LODRequirementsDatabase
    {
        private readonly Dictionary<ElementCategory, CategoryLODRequirements> _requirements;

        public LODRequirementsDatabase()
        {
            _requirements = InitializeRequirements();
        }

        public CategoryLODRequirements GetRequirements(ElementCategory category)
        {
            return _requirements.TryGetValue(category, out var reqs)
                ? reqs
                : _requirements[ElementCategory.Generic];
        }

        private Dictionary<ElementCategory, CategoryLODRequirements> InitializeRequirements()
        {
            return new Dictionary<ElementCategory, CategoryLODRequirements>
            {
                [ElementCategory.Walls] = new CategoryLODRequirements
                {
                    Category = ElementCategory.Walls,
                    Levels = new Dictionary<LODLevel, List<LODRequirement>>
                    {
                        [LODLevel.LOD100] = new List<LODRequirement>
                        {
                            new() { Name = "Overall Mass", Type = RequirementType.Geometry, Description = "Conceptual volume/mass" }
                        },
                        [LODLevel.LOD200] = new List<LODRequirement>
                        {
                            new() { Name = "Approximate Geometry", Type = RequirementType.Geometry, Description = "Approximate size, shape, location" },
                            new() { Name = "Wall Type", Type = RequirementType.Parameter, DataType = "string" }
                        },
                        [LODLevel.LOD300] = new List<LODRequirement>
                        {
                            new() { Name = "Accurate Geometry", Type = RequirementType.Geometry, Description = "Accurate size, shape, location" },
                            new() { Name = "Wall Assembly", Type = RequirementType.Parameter, DataType = "string" },
                            new() { Name = "Fire Rating", Type = RequirementType.Parameter, DataType = "string" },
                            new() { Name = "Acoustic Rating", Type = RequirementType.Parameter, DataType = "double" }
                        },
                        [LODLevel.LOD350] = new List<LODRequirement>
                        {
                            new() { Name = "Layer Definition", Type = RequirementType.Geometry, Description = "Individual layers modeled" },
                            new() { Name = "Material Specification", Type = RequirementType.Material },
                            new() { Name = "Connection Details", Type = RequirementType.Connection }
                        },
                        [LODLevel.LOD400] = new List<LODRequirement>
                        {
                            new() { Name = "Fabrication Detail", Type = RequirementType.Geometry, Description = "Shop detail level" },
                            new() { Name = "Installation Sequence", Type = RequirementType.Parameter, DataType = "int" },
                            new() { Name = "Manufacturer", Type = RequirementType.Parameter, DataType = "string" }
                        },
                        [LODLevel.LOD500] = new List<LODRequirement>
                        {
                            new() { Name = "As-Built Verification", Type = RequirementType.Documentation },
                            new() { Name = "Maintenance Info", Type = RequirementType.Documentation },
                            new() { Name = "Warranty Info", Type = RequirementType.Parameter, DataType = "string" }
                        }
                    }
                },
                [ElementCategory.Structural] = new CategoryLODRequirements
                {
                    Category = ElementCategory.Structural,
                    Levels = new Dictionary<LODLevel, List<LODRequirement>>
                    {
                        [LODLevel.LOD100] = new List<LODRequirement>
                        {
                            new() { Name = "Structural Concept", Type = RequirementType.Geometry }
                        },
                        [LODLevel.LOD200] = new List<LODRequirement>
                        {
                            new() { Name = "Approximate Size/Location", Type = RequirementType.Geometry },
                            new() { Name = "Member Type", Type = RequirementType.Parameter, DataType = "string" }
                        },
                        [LODLevel.LOD300] = new List<LODRequirement>
                        {
                            new() { Name = "Accurate Geometry", Type = RequirementType.Geometry },
                            new() { Name = "Section Profile", Type = RequirementType.Parameter, DataType = "string" },
                            new() { Name = "Material Grade", Type = RequirementType.Material },
                            new() { Name = "Load Capacity", Type = RequirementType.Parameter, DataType = "double" }
                        },
                        [LODLevel.LOD350] = new List<LODRequirement>
                        {
                            new() { Name = "Connection Design", Type = RequirementType.Connection },
                            new() { Name = "Reinforcement Layout", Type = RequirementType.Geometry },
                            new() { Name = "Camber", Type = RequirementType.Parameter, DataType = "double" }
                        },
                        [LODLevel.LOD400] = new List<LODRequirement>
                        {
                            new() { Name = "Shop Detail", Type = RequirementType.Geometry },
                            new() { Name = "Piece Mark", Type = RequirementType.Parameter, DataType = "string" },
                            new() { Name = "Erection Sequence", Type = RequirementType.Parameter, DataType = "int" }
                        },
                        [LODLevel.LOD500] = new List<LODRequirement>
                        {
                            new() { Name = "As-Built Survey", Type = RequirementType.Documentation },
                            new() { Name = "Mill Certificates", Type = RequirementType.Documentation },
                            new() { Name = "Inspection Records", Type = RequirementType.Documentation }
                        }
                    }
                },
                [ElementCategory.MEP] = new CategoryLODRequirements
                {
                    Category = ElementCategory.MEP,
                    Levels = new Dictionary<LODLevel, List<LODRequirement>>
                    {
                        [LODLevel.LOD100] = new List<LODRequirement>
                        {
                            new() { Name = "System Concept", Type = RequirementType.Geometry }
                        },
                        [LODLevel.LOD200] = new List<LODRequirement>
                        {
                            new() { Name = "Approximate Routing", Type = RequirementType.Geometry },
                            new() { Name = "System Type", Type = RequirementType.Parameter, DataType = "string" }
                        },
                        [LODLevel.LOD300] = new List<LODRequirement>
                        {
                            new() { Name = "Accurate Routing", Type = RequirementType.Geometry },
                            new() { Name = "Size", Type = RequirementType.Parameter, DataType = "double" },
                            new() { Name = "Flow/Load", Type = RequirementType.Parameter, DataType = "double" }
                        },
                        [LODLevel.LOD350] = new List<LODRequirement>
                        {
                            new() { Name = "Connections", Type = RequirementType.Connection },
                            new() { Name = "Supports", Type = RequirementType.Geometry },
                            new() { Name = "Access Points", Type = RequirementType.Geometry }
                        },
                        [LODLevel.LOD400] = new List<LODRequirement>
                        {
                            new() { Name = "Fabrication Detail", Type = RequirementType.Geometry },
                            new() { Name = "Part Numbers", Type = RequirementType.Parameter, DataType = "string" }
                        },
                        [LODLevel.LOD500] = new List<LODRequirement>
                        {
                            new() { Name = "Commissioning Data", Type = RequirementType.Documentation },
                            new() { Name = "O&M Manuals", Type = RequirementType.Documentation }
                        }
                    }
                },
                [ElementCategory.Generic] = new CategoryLODRequirements
                {
                    Category = ElementCategory.Generic,
                    Levels = new Dictionary<LODLevel, List<LODRequirement>>
                    {
                        [LODLevel.LOD100] = new List<LODRequirement>
                        {
                            new() { Name = "Conceptual Mass", Type = RequirementType.Geometry }
                        },
                        [LODLevel.LOD200] = new List<LODRequirement>
                        {
                            new() { Name = "Approximate Geometry", Type = RequirementType.Geometry }
                        },
                        [LODLevel.LOD300] = new List<LODRequirement>
                        {
                            new() { Name = "Accurate Geometry", Type = RequirementType.Geometry },
                            new() { Name = "Type Parameters", Type = RequirementType.Parameter, DataType = "string" }
                        },
                        [LODLevel.LOD350] = new List<LODRequirement>
                        {
                            new() { Name = "Detailed Geometry", Type = RequirementType.Geometry }
                        },
                        [LODLevel.LOD400] = new List<LODRequirement>
                        {
                            new() { Name = "Fabrication Detail", Type = RequirementType.Geometry }
                        },
                        [LODLevel.LOD500] = new List<LODRequirement>
                        {
                            new() { Name = "As-Built Data", Type = RequirementType.Documentation }
                        }
                    }
                }
            };
        }
    }

    #endregion

    #region LOD Validator

    internal class LODValidator
    {
        private readonly LODRequirementsDatabase _requirementsDb;

        public LODValidator(LODRequirementsDatabase requirementsDb)
        {
            _requirementsDb = requirementsDb;
        }

        public LODLevelValidation ValidateElement(BIMElement element, List<LODRequirement> requirements)
        {
            var validation = new LODLevelValidation { IsMet = true };

            foreach (var req in requirements)
            {
                var isMet = IsRequirementMet(element, req);
                validation.RequirementResults[req.Name] = isMet;
                if (!isMet) validation.IsMet = false;
            }

            return validation;
        }

        public bool IsRequirementMet(BIMElement element, LODRequirement requirement)
        {
            return requirement.Type switch
            {
                RequirementType.Geometry => ValidateGeometry(element, requirement),
                RequirementType.Parameter => ValidateParameter(element, requirement),
                RequirementType.Material => ValidateMaterial(element, requirement),
                RequirementType.Connection => ValidateConnection(element, requirement),
                RequirementType.Documentation => ValidateDocumentation(element, requirement),
                _ => false
            };
        }

        private bool ValidateGeometry(BIMElement element, LODRequirement req)
        {
            return element.HasGeometry && element.GeometryDetail >= GetRequiredDetailLevel(req);
        }

        private bool ValidateParameter(BIMElement element, LODRequirement req)
        {
            return element.HasParameter(req.Name) && element.GetParameter(req.Name) != null;
        }

        private bool ValidateMaterial(BIMElement element, LODRequirement req)
        {
            return !string.IsNullOrEmpty(element.Material);
        }

        private bool ValidateConnection(BIMElement element, LODRequirement req)
        {
            return element.Connections != null && element.Connections.Any();
        }

        private bool ValidateDocumentation(BIMElement element, LODRequirement req)
        {
            return element.DocumentationLinks != null &&
                   element.DocumentationLinks.ContainsKey(req.Name);
        }

        private int GetRequiredDetailLevel(LODRequirement req)
        {
            return req.Name switch
            {
                "Overall Mass" => 1,
                "Approximate Geometry" => 2,
                "Accurate Geometry" => 3,
                "Layer Definition" or "Detailed Geometry" => 4,
                "Fabrication Detail" or "Shop Detail" => 5,
                _ => 1
            };
        }
    }

    #endregion

    #region Data Models

    public class LODSettings
    {
        public bool StrictValidation { get; set; } = false;
        public bool IncludeSubElements { get; set; } = true;
    }

    public class LODProgress
    {
        public string Phase { get; set; }
        public int PercentComplete { get; set; }
        public string CurrentElement { get; set; }
    }

    public class LODAnalysisResult
    {
        public string ModelId { get; set; }
        public Dictionary<string, ElementLODResult> ElementResults { get; } = new();
        public LODSummary Summary { get; set; }
    }

    public class ElementLODResult
    {
        public string ElementId { get; set; }
        public string ElementName { get; set; }
        public ElementCategory Category { get; set; }
        public LODLevel CurrentLOD { get; set; } = LODLevel.LOD100;
        public Dictionary<LODLevel, LODLevelValidation> LevelValidations { get; } = new();
        public List<LODRequirement> MissingForNextLevel { get; set; } = new();
    }

    public class LODLevelValidation
    {
        public bool IsMet { get; set; }
        public Dictionary<string, bool> RequirementResults { get; } = new();
    }

    public class LODSummary
    {
        public int TotalElements { get; set; }
        public int LOD100Count { get; set; }
        public int LOD200Count { get; set; }
        public int LOD300Count { get; set; }
        public int LOD350Count { get; set; }
        public int LOD400Count { get; set; }
        public int LOD500Count { get; set; }
        public double AverageLOD { get; set; }
    }

    public class LODProgressionRoadmap
    {
        public string ElementId { get; set; }
        public LODLevel CurrentLOD { get; set; }
        public LODLevel TargetLOD { get; set; }
        public bool IsComplete { get; set; }
        public List<LODProgressionStep> Steps { get; } = new();
    }

    public class LODProgressionStep
    {
        public LODLevel FromLOD { get; set; }
        public LODLevel ToLOD { get; set; }
        public List<LODUpgradeAction> RequiredActions { get; set; } = new();
    }

    public class LODUpgradeAction
    {
        public LODRequirement Requirement { get; set; }
        public LODActionType ActionType { get; set; }
        public string Description { get; set; }
    }

    public class LODUpgradeOptions
    {
        public bool AutoPopulateParameters { get; set; } = true;
        public bool AutoAssignMaterials { get; set; } = true;
        public bool ContinueOnFailure { get; set; } = true;
    }

    public class LODUpgradeResult
    {
        public LODLevel TargetLOD { get; set; }
        public List<ElementUpgradeResult> ElementResults { get; } = new();
        public LODUpgradeSummary Summary { get; set; }
    }

    public class ElementUpgradeResult
    {
        public string ElementId { get; set; }
        public LODLevel OriginalLOD { get; set; }
        public LODLevel TargetLOD { get; set; }
        public LODLevel AchievedLOD { get; set; }
        public bool Success { get; set; }
        public bool PartialSuccess { get; set; }
        public string FailureReason { get; set; }
        public List<ActionResult> ActionsPerformed { get; } = new();
    }

    public class ActionResult
    {
        public LODUpgradeAction Action { get; set; }
        public bool Success { get; set; }
        public bool RequiresManualAction { get; set; }
        public string Error { get; set; }
    }

    public class LODUpgradeSummary
    {
        public int TotalElements { get; set; }
        public int SuccessfulUpgrades { get; set; }
        public int PartialUpgrades { get; set; }
        public int FailedUpgrades { get; set; }
    }

    public class LODComplianceReport
    {
        public string ModelId { get; set; }
        public LODMilestone Milestone { get; set; }
        public DateTime GeneratedDate { get; set; }
        public List<ElementComplianceStatus> ElementCompliance { get; } = new();
        public ComplianceSummary Summary { get; set; }
    }

    public class LODMilestone
    {
        public string Name { get; set; }
        public ProjectPhase Phase { get; set; }
        public Dictionary<ElementCategory, LODLevel> CategoryRequirements { get; } = new();
        public LODLevel DefaultLOD { get; set; }

        public LODLevel GetRequiredLOD(ElementCategory category)
        {
            return CategoryRequirements.TryGetValue(category, out var lod) ? lod : DefaultLOD;
        }
    }

    public class ElementComplianceStatus
    {
        public string ElementId { get; set; }
        public ElementCategory Category { get; set; }
        public LODLevel RequiredLOD { get; set; }
        public LODLevel ActualLOD { get; set; }
        public bool IsCompliant { get; set; }
        public List<LODRequirement> GapAnalysis { get; set; }
    }

    public class ComplianceSummary
    {
        public int TotalElements { get; set; }
        public int CompliantElements { get; set; }
        public int NonCompliantElements { get; set; }
        public double CompliancePercentage { get; set; }
    }

    public class LODPlan
    {
        public string ProjectId { get; set; }
        public List<PhaseTarget> PhaseTargets { get; } = new();
    }

    public class PhaseTarget
    {
        public ProjectPhase Phase { get; set; }
        public LODLevel DefaultLOD { get; set; }
        public Dictionary<ElementCategory, LODLevel> CategoryOverrides { get; set; } = new();
    }

    public class LODPlanOptions
    {
        public bool UseIndustryDefaults { get; set; } = true;
    }

    public class ProjectPhases
    {
        public string ProjectId { get; set; }
        public List<ProjectPhase> Phases { get; set; } = new();
    }

    public class CategoryLODRequirements
    {
        public ElementCategory Category { get; set; }
        public Dictionary<LODLevel, List<LODRequirement>> Levels { get; set; } = new();

        public List<LODRequirement> GetRequirementsForLevel(LODLevel level)
        {
            var reqs = new List<LODRequirement>();
            foreach (var (lvl, levelReqs) in Levels.OrderBy(kv => kv.Key))
            {
                if (lvl <= level)
                    reqs.AddRange(levelReqs);
            }
            return reqs;
        }
    }

    public class LODRequirement
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public RequirementType Type { get; set; }
        public string DataType { get; set; }
        public object DefaultValue { get; set; }
    }

    // BIM Model Classes
    public class BIMModel
    {
        public string Id { get; set; }
        private readonly List<BIMElement> _elements = new();

        public List<BIMElement> GetAllElements() => _elements;
        public BIMElement GetElement(string id) => _elements.FirstOrDefault(e => e.Id == id);
    }

    public class BIMElement
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public ElementCategory Category { get; set; }
        public bool HasGeometry { get; set; } = true;
        public int GeometryDetail { get; set; } = 1;
        public string Material { get; set; }
        public List<Connection> Connections { get; set; }
        public Dictionary<string, string> DocumentationLinks { get; set; } = new();
        private readonly Dictionary<string, object> _parameters = new();

        public bool HasParameter(string name) => _parameters.ContainsKey(name);
        public object GetParameter(string name) => _parameters.TryGetValue(name, out var v) ? v : null;
        public void SetParameter(string name, object value) => _parameters[name] = value;
        public void AddParameter(string name, string dataType, object value) => _parameters[name] = value;
        public void SetMaterial(string material) => Material = material;
        public void AddDocumentationLink(string name, string link) => DocumentationLinks[name] = link;
    }

    public class Connection
    {
        public string Id { get; set; }
        public string ConnectedElementId { get; set; }
        public string ConnectionType { get; set; }
    }

    // Enums
    public enum LODLevel
    {
        LOD100 = 100,
        LOD200 = 200,
        LOD300 = 300,
        LOD350 = 350,
        LOD400 = 400,
        LOD500 = 500
    }

    public enum RequirementType { Geometry, Parameter, Material, Connection, Documentation }
    public enum LODActionType { AddGeometry, AddParameter, AssignMaterial, DefineConnection, AddDocumentation, Manual }
    public enum ElementCategory { Walls, Floors, Roofs, Columns, Beams, Structural, MEP, Doors, Windows, Specialty, Generic }
    public enum ProjectPhase { Concept, SchematicDesign, DesignDevelopment, ConstructionDocuments, Construction, AsBuilt }

    #endregion
}
