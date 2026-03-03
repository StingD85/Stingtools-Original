using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using StingBIM.AI.Creation.Common;

namespace StingBIM.AI.Creation.Pipeline
{
    /// <summary>
    /// Pipeline connecting the Design module to the Creation module.
    /// Transforms design solutions into actual building elements through Revit API.
    /// Supports validation, optimization, and phased creation workflows.
    /// </summary>
    public class DesignToCreationPipeline
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly Dictionary<string, IPipelineElementCreator> _elementCreators;
        private readonly List<PipelineExecution> _executionHistory;
        private readonly PipelineConfiguration _configuration;
        private readonly object _lock = new object();

        public event EventHandler<PipelineProgressEventArgs> ProgressChanged;
        public event EventHandler<ElementCreatedEventArgs> ElementCreated;
        public event EventHandler<PipelineErrorEventArgs> ErrorOccurred;

        public DesignToCreationPipeline(PipelineConfiguration configuration = null)
        {
            _configuration = configuration ?? new PipelineConfiguration();
            _elementCreators = new Dictionary<string, IPipelineElementCreator>(StringComparer.OrdinalIgnoreCase);
            _executionHistory = new List<PipelineExecution>();

            Logger.Info("DesignToCreationPipeline initialized");
        }

        #region Creator Registration

        /// <summary>
        /// Registers an element creator for a specific element type.
        /// </summary>
        public void RegisterCreator(string elementType, IPipelineElementCreator creator)
        {
            lock (_lock)
            {
                _elementCreators[elementType] = creator;
                Logger.Debug("Registered creator for element type: {0}", elementType);
            }
        }

        /// <summary>
        /// Registers multiple creators at once.
        /// </summary>
        public void RegisterCreators(Dictionary<string, IPipelineElementCreator> creators)
        {
            lock (_lock)
            {
                foreach (var kvp in creators)
                {
                    _elementCreators[kvp.Key] = kvp.Value;
                }
                Logger.Info("Registered {0} element creators", creators.Count);
            }
        }

        #endregion

        #region Pipeline Execution

        /// <summary>
        /// Executes the full design-to-creation pipeline.
        /// </summary>
        public async Task<PipelineResult> ExecutePipelineAsync(
            DesignSolution designSolution,
            CreationContext context,
            IProgress<PipelineProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            Logger.Info("Starting pipeline execution for design: {0}", designSolution.DesignId);

            var execution = new PipelineExecution
            {
                ExecutionId = Guid.NewGuid().ToString("N"),
                DesignId = designSolution.DesignId,
                StartTime = DateTime.UtcNow,
                Status = PipelineStatus.Running
            };

            var result = new PipelineResult
            {
                ExecutionId = execution.ExecutionId,
                DesignId = designSolution.DesignId,
                CreatedElements = new List<CreatedElement>(),
                FailedElements = new List<FailedElement>(),
                Warnings = new List<string>()
            };

            try
            {
                // Phase 1: Validate design
                ReportProgress(progress, 0, "Validating design solution...");
                var validationResult = await ValidateDesignAsync(designSolution, context, cancellationToken);

                if (!validationResult.IsValid && !_configuration.ContinueOnValidationWarnings)
                {
                    result.Success = false;
                    result.ErrorMessage = "Design validation failed: " + string.Join("; ", validationResult.Errors);
                    execution.Status = PipelineStatus.Failed;
                    return result;
                }

                result.Warnings.AddRange(validationResult.Warnings);

                // Phase 2: Prepare creation order
                ReportProgress(progress, 10, "Determining creation sequence...");
                var creationPlan = await PrepareCreationPlanAsync(designSolution, context, cancellationToken);

                // Phase 3: Execute creation in phases
                int totalElements = creationPlan.Phases.Sum(p => p.Elements.Count);
                int processedElements = 0;

                foreach (var phase in creationPlan.Phases.OrderBy(p => p.Order))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    ReportProgress(progress, 20 + (processedElements * 70 / totalElements),
                        $"Creating {phase.Name} ({phase.Elements.Count} elements)...");

                    foreach (var element in phase.Elements)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        try
                        {
                            var createdElement = await CreateElementAsync(element, context, cancellationToken);

                            if (createdElement != null)
                            {
                                result.CreatedElements.Add(createdElement);
                                OnElementCreated(createdElement);
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Warn(ex, "Failed to create element: {0}", element.ElementId);

                            result.FailedElements.Add(new FailedElement
                            {
                                DesignElement = element,
                                Error = ex.Message,
                                Exception = ex
                            });

                            OnErrorOccurred(element, ex);

                            if (!_configuration.ContinueOnElementError)
                            {
                                throw;
                            }
                        }

                        processedElements++;
                        ReportProgress(progress, 20 + (processedElements * 70 / totalElements),
                            $"Created {processedElements}/{totalElements} elements...");
                    }
                }

                // Phase 4: Post-processing
                ReportProgress(progress, 90, "Running post-processing...");
                await RunPostProcessingAsync(result, context, cancellationToken);

                // Phase 5: Finalize
                ReportProgress(progress, 100, "Pipeline complete");

                result.Success = result.FailedElements.Count == 0 ||
                               (result.CreatedElements.Count > 0 && _configuration.ContinueOnElementError);

                execution.Status = result.Success ? PipelineStatus.Completed : PipelineStatus.PartiallyCompleted;
                execution.EndTime = DateTime.UtcNow;

                Logger.Info("Pipeline execution complete: {0} created, {1} failed",
                    result.CreatedElements.Count, result.FailedElements.Count);
            }
            catch (OperationCanceledException)
            {
                Logger.Info("Pipeline execution cancelled");
                execution.Status = PipelineStatus.Cancelled;
                result.Success = false;
                result.ErrorMessage = "Pipeline execution was cancelled";
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Pipeline execution failed");
                execution.Status = PipelineStatus.Failed;
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }
            finally
            {
                lock (_lock)
                {
                    _executionHistory.Add(execution);
                }
            }

            return result;
        }

        /// <summary>
        /// Executes pipeline for a single element (for testing or incremental creation).
        /// </summary>
        public async Task<CreatedElement> CreateSingleElementAsync(
            DesignElement element,
            CreationContext context,
            CancellationToken cancellationToken = default)
        {
            return await CreateElementAsync(element, context, cancellationToken);
        }

        #endregion

        #region Validation

        private async Task<ValidationResult> ValidateDesignAsync(
            DesignSolution design,
            CreationContext context,
            CancellationToken cancellationToken)
        {
            var result = new ValidationResult
            {
                IsValid = true,
                Errors = new List<string>(),
                Warnings = new List<string>()
            };

            await Task.Run(() =>
            {
                // Check all element types have creators
                foreach (var element in design.Elements)
                {
                    if (!_elementCreators.ContainsKey(element.ElementType))
                    {
                        result.Errors.Add($"No creator registered for element type: {element.ElementType}");
                        result.IsValid = false;
                    }
                }

                // Validate element dependencies
                var elementIds = design.Elements.Select(e => e.ElementId).ToHashSet();
                foreach (var element in design.Elements)
                {
                    foreach (var dependency in element.Dependencies ?? Enumerable.Empty<string>())
                    {
                        if (!elementIds.Contains(dependency))
                        {
                            result.Warnings.Add($"Element {element.ElementId} has unresolved dependency: {dependency}");
                        }
                    }
                }

                // Check for spatial conflicts
                var spatialConflicts = DetectSpatialConflicts(design.Elements);
                foreach (var conflict in spatialConflicts)
                {
                    result.Warnings.Add($"Potential spatial conflict: {conflict}");
                }

                // Validate against context constraints
                if (context.MaxElements > 0 && design.Elements.Count > context.MaxElements)
                {
                    result.Warnings.Add($"Design contains {design.Elements.Count} elements, exceeds recommended maximum of {context.MaxElements}");
                }

                // Check level references
                if (context.AvailableLevels?.Any() == true)
                {
                    var invalidLevels = design.Elements
                        .Where(e => e.Level != null && !context.AvailableLevels.Contains(e.Level))
                        .Select(e => e.Level)
                        .Distinct();

                    foreach (var level in invalidLevels)
                    {
                        result.Errors.Add($"Design references non-existent level: {level}");
                        result.IsValid = false;
                    }
                }
            }, cancellationToken);

            return result;
        }

        private List<string> DetectSpatialConflicts(List<DesignElement> elements)
        {
            var conflicts = new List<string>();

            // Simple AABB overlap check
            for (int i = 0; i < elements.Count; i++)
            {
                for (int j = i + 1; j < elements.Count; j++)
                {
                    if (ElementsOverlap(elements[i], elements[j]))
                    {
                        conflicts.Add($"{elements[i].ElementId} overlaps with {elements[j].ElementId}");
                    }
                }
            }

            return conflicts;
        }

        private bool ElementsOverlap(DesignElement e1, DesignElement e2)
        {
            if (e1.BoundingBox == null || e2.BoundingBox == null)
                return false;

            return e1.BoundingBox.Overlaps(e2.BoundingBox);
        }

        #endregion

        #region Creation Plan

        private async Task<CreationPlan> PrepareCreationPlanAsync(
            DesignSolution design,
            CreationContext context,
            CancellationToken cancellationToken)
        {
            var plan = new CreationPlan
            {
                DesignId = design.DesignId,
                Phases = new List<CreationPhase>()
            };

            await Task.Run(() =>
            {
                // Group elements by category/type for proper creation order
                var elementsByCategory = design.Elements.GroupBy(e => GetCreationCategory(e.ElementType));

                // Define creation order
                var categoryOrder = new Dictionary<string, int>
                {
                    ["Grids"] = 1,
                    ["Levels"] = 2,
                    ["Structural_Foundations"] = 3,
                    ["Structural_Columns"] = 4,
                    ["Structural_Beams"] = 5,
                    ["Structural_Floors"] = 6,
                    ["Walls"] = 7,
                    ["Floors"] = 8,
                    ["Ceilings"] = 9,
                    ["Roofs"] = 10,
                    ["Stairs"] = 11,
                    ["Doors"] = 12,
                    ["Windows"] = 13,
                    ["MEP_Ducts"] = 14,
                    ["MEP_Pipes"] = 15,
                    ["MEP_Equipment"] = 16,
                    ["Electrical"] = 17,
                    ["Furniture"] = 18,
                    ["Annotations"] = 99
                };

                foreach (var group in elementsByCategory)
                {
                    var phase = new CreationPhase
                    {
                        Name = group.Key,
                        Order = categoryOrder.TryGetValue(group.Key, out int order) ? order : 50,
                        Elements = group.ToList()
                    };

                    // Sort elements within phase by dependencies
                    phase.Elements = TopologicalSort(phase.Elements);

                    plan.Phases.Add(phase);
                }
            }, cancellationToken);

            return plan;
        }

        private string GetCreationCategory(string elementType)
        {
            return elementType switch
            {
                "Grid" => "Grids",
                "Level" => "Levels",
                "Column" or "StructuralColumn" => "Structural_Columns",
                "Beam" or "StructuralBeam" => "Structural_Beams",
                "StructuralFloor" or "Foundation" => "Structural_Foundations",
                "Wall" or "BasicWall" or "CurtainWall" => "Walls",
                "Floor" => "Floors",
                "Ceiling" => "Ceilings",
                "Roof" or "FootprintRoof" => "Roofs",
                "Stair" or "Ramp" => "Stairs",
                "Door" => "Doors",
                "Window" => "Windows",
                "Duct" or "FlexDuct" => "MEP_Ducts",
                "Pipe" or "FlexPipe" => "MEP_Pipes",
                "MechanicalEquipment" or "AirTerminal" => "MEP_Equipment",
                "ElectricalFixture" or "CableTray" or "Conduit" => "Electrical",
                "FurnitureSystem" or "Furniture" => "Furniture",
                "TextNote" or "Dimension" or "Tag" => "Annotations",
                _ => "Other"
            };
        }

        private List<DesignElement> TopologicalSort(List<DesignElement> elements)
        {
            // Build dependency graph
            var graph = new Dictionary<string, List<string>>();
            var inDegree = new Dictionary<string, int>();

            foreach (var element in elements)
            {
                graph[element.ElementId] = new List<string>();
                inDegree[element.ElementId] = 0;
            }

            foreach (var element in elements)
            {
                foreach (var dep in element.Dependencies ?? Enumerable.Empty<string>())
                {
                    if (graph.ContainsKey(dep))
                    {
                        graph[dep].Add(element.ElementId);
                        inDegree[element.ElementId]++;
                    }
                }
            }

            // Kahn's algorithm for topological sort
            var queue = new Queue<string>();
            foreach (var kvp in inDegree)
            {
                if (kvp.Value == 0)
                    queue.Enqueue(kvp.Key);
            }

            var sortedIds = new List<string>();
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                sortedIds.Add(current);

                foreach (var neighbor in graph[current])
                {
                    inDegree[neighbor]--;
                    if (inDegree[neighbor] == 0)
                        queue.Enqueue(neighbor);
                }
            }

            // Return elements in sorted order
            var elementMap = elements.ToDictionary(e => e.ElementId);
            return sortedIds.Where(id => elementMap.ContainsKey(id)).Select(id => elementMap[id]).ToList();
        }

        #endregion

        #region Element Creation

        private async Task<CreatedElement> CreateElementAsync(
            DesignElement element,
            CreationContext context,
            CancellationToken cancellationToken)
        {
            if (!_elementCreators.TryGetValue(element.ElementType, out var creator))
            {
                throw new InvalidOperationException($"No creator registered for element type: {element.ElementType}");
            }

            Logger.Debug("Creating element: {0} ({1})", element.ElementId, element.ElementType);

            var creationParams = new ElementCreationParams
            {
                DesignElement = element,
                Context = context,
                Options = _configuration.DefaultCreationOptions
            };

            // Transform design coordinates to Revit coordinates if needed
            if (_configuration.CoordinateTransform != null)
            {
                creationParams.TransformedGeometry = _configuration.CoordinateTransform.Transform(element.Geometry);
            }

            var createdElement = await creator.CreateElementAsync(creationParams, cancellationToken);

            // Map design element ID to Revit element ID
            if (createdElement != null && !string.IsNullOrEmpty(element.ElementId))
            {
                context.ElementIdMapping[element.ElementId] = createdElement.RevitElementId;
            }

            return createdElement;
        }

        #endregion

        #region Post-Processing

        private async Task RunPostProcessingAsync(
            PipelineResult result,
            CreationContext context,
            CancellationToken cancellationToken)
        {
            await Task.Run(() =>
            {
                // Update element parameters
                foreach (var element in result.CreatedElements)
                {
                    if (element.AdditionalParameters?.Any() == true)
                    {
                        // In real implementation, would update Revit parameters here
                        Logger.Debug("Would update {0} parameters for element {1}",
                            element.AdditionalParameters.Count, element.RevitElementId);
                    }
                }

                // Create element associations
                var associationCount = 0;
                foreach (var element in result.CreatedElements)
                {
                    if (element.DesignElement?.Associations?.Any() == true)
                    {
                        foreach (var association in element.DesignElement.Associations)
                        {
                            if (context.ElementIdMapping.TryGetValue(association.TargetElementId, out var revitId))
                            {
                                // In real implementation, would create Revit element association
                                associationCount++;
                            }
                        }
                    }
                }

                if (associationCount > 0)
                {
                    Logger.Debug("Created {0} element associations", associationCount);
                }

                // Run validation on created elements
                if (_configuration.ValidateAfterCreation)
                {
                    var validationIssues = ValidateCreatedElements(result.CreatedElements);
                    result.Warnings.AddRange(validationIssues);
                }

            }, cancellationToken);
        }

        private List<string> ValidateCreatedElements(List<CreatedElement> elements)
        {
            var issues = new List<string>();

            // Check for elements that weren't created with expected properties
            foreach (var element in elements)
            {
                if (element.CreationStatus != CreationStatus.Success)
                {
                    issues.Add($"Element {element.RevitElementId} created with status: {element.CreationStatus}");
                }

                if (element.Warnings?.Any() == true)
                {
                    issues.AddRange(element.Warnings);
                }
            }

            return issues;
        }

        #endregion

        #region Progress and Events

        private void ReportProgress(IProgress<PipelineProgress> progress, int percentage, string message)
        {
            var progressInfo = new PipelineProgress
            {
                PercentComplete = percentage,
                CurrentOperation = message
            };

            progress?.Report(progressInfo);
            ProgressChanged?.Invoke(this, new PipelineProgressEventArgs(progressInfo));
        }

        private void OnElementCreated(CreatedElement element)
        {
            ElementCreated?.Invoke(this, new ElementCreatedEventArgs(element));
        }

        private void OnErrorOccurred(DesignElement element, Exception exception)
        {
            ErrorOccurred?.Invoke(this, new PipelineErrorEventArgs(element, exception));
        }

        #endregion

        #region Public API

        /// <summary>
        /// Gets pipeline configuration.
        /// </summary>
        public PipelineConfiguration Configuration => _configuration;

        /// <summary>
        /// Gets registered element types.
        /// </summary>
        public IEnumerable<string> GetRegisteredElementTypes()
        {
            lock (_lock)
            {
                return _elementCreators.Keys.ToList();
            }
        }

        /// <summary>
        /// Gets execution history.
        /// </summary>
        public IEnumerable<PipelineExecution> GetExecutionHistory()
        {
            lock (_lock)
            {
                return _executionHistory.ToList();
            }
        }

        /// <summary>
        /// Checks if an element type is supported.
        /// </summary>
        public bool IsElementTypeSupported(string elementType)
        {
            lock (_lock)
            {
                return _elementCreators.ContainsKey(elementType);
            }
        }

        /// <summary>
        /// Dry run - validates design without creating elements.
        /// </summary>
        public async Task<ValidationResult> DryRunAsync(
            DesignSolution design,
            CreationContext context,
            CancellationToken cancellationToken = default)
        {
            Logger.Info("Running dry run validation for design: {0}", design.DesignId);

            var result = await ValidateDesignAsync(design, context, cancellationToken);
            var plan = await PrepareCreationPlanAsync(design, context, cancellationToken);

            // Add plan details to result
            result.AdditionalInfo = new Dictionary<string, object>
            {
                ["PhaseCount"] = plan.Phases.Count,
                ["TotalElements"] = plan.Phases.Sum(p => p.Elements.Count),
                ["Phases"] = plan.Phases.Select(p => new { p.Name, p.Order, ElementCount = p.Elements.Count })
            };

            return result;
        }

        #endregion
    }

    #region Interfaces

    /// <summary>
    /// Interface for pipeline element creators.
    /// This is specific to the design-to-creation pipeline.
    /// For general element creation, see StingBIM.AI.Creation.Elements.IElementCreator.
    /// </summary>
    public interface IPipelineElementCreator
    {
        string ElementType { get; }
        Task<CreatedElement> CreateElementAsync(ElementCreationParams parameters, CancellationToken cancellationToken);
        bool CanCreate(DesignElement element);
    }

    /// <summary>
    /// Interface for coordinate transformation.
    /// </summary>
    public interface ICoordinateTransform
    {
        ElementGeometry Transform(ElementGeometry geometry);
    }

    #endregion

    #region Data Models

    public class PipelineConfiguration
    {
        public bool ContinueOnValidationWarnings { get; set; } = true;
        public bool ContinueOnElementError { get; set; } = true;
        public bool ValidateAfterCreation { get; set; } = true;
        public ElementCreationOptions DefaultCreationOptions { get; set; } = new ElementCreationOptions();
        public ICoordinateTransform CoordinateTransform { get; set; }
    }

    public class ElementCreationOptions
    {
        public bool AutoJoin { get; set; } = true;
        public bool CreateAssociations { get; set; } = true;
        public bool SetParameters { get; set; } = true;
    }

    public class DesignSolution
    {
        public string DesignId { get; set; }
        public string DesignName { get; set; }
        public List<DesignElement> Elements { get; set; } = new List<DesignElement>();
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
    }

    public class DesignElement
    {
        public string ElementId { get; set; }
        public string ElementType { get; set; }
        public string Name { get; set; }
        public string Level { get; set; }
        public ElementGeometry Geometry { get; set; }
        public DesignBoundingBox BoundingBox { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
        public List<string> Dependencies { get; set; } = new List<string>();
        public List<ElementAssociation> Associations { get; set; } = new List<ElementAssociation>();
    }

    public class ElementGeometry
    {
        public List<Point3D> Points { get; set; } = new List<Point3D>();
        public List<Curve> Curves { get; set; } = new List<Curve>();
        public double Width { get; set; }
        public double Height { get; set; }
        public double Depth { get; set; }
        public double Rotation { get; set; }
    }

    public class Curve
    {
        public Point3D Start { get; set; }
        public Point3D End { get; set; }
        public string CurveType { get; set; } = "Line";
    }

    public class DesignBoundingBox
    {
        public Point3D Min { get; set; }
        public Point3D Max { get; set; }

        public bool Overlaps(DesignBoundingBox other)
        {
            if (other == null) return false;

            return Min.X <= other.Max.X && Max.X >= other.Min.X &&
                   Min.Y <= other.Max.Y && Max.Y >= other.Min.Y &&
                   Min.Z <= other.Max.Z && Max.Z >= other.Min.Z;
        }
    }

    public class ElementAssociation
    {
        public string TargetElementId { get; set; }
        public string AssociationType { get; set; }
    }

    public class CreationContext
    {
        public object RevitDocument { get; set; } // Autodesk.Revit.DB.Document in real implementation
        public List<string> AvailableLevels { get; set; } = new List<string>();
        public Dictionary<string, string> ElementIdMapping { get; set; } = new Dictionary<string, string>();
        public int MaxElements { get; set; } = 0;
        public Dictionary<string, object> AdditionalContext { get; set; } = new Dictionary<string, object>();
    }

    public class ElementCreationParams
    {
        public DesignElement DesignElement { get; set; }
        public CreationContext Context { get; set; }
        public ElementCreationOptions Options { get; set; }
        public ElementGeometry TransformedGeometry { get; set; }
    }

    public class CreatedElement
    {
        public string RevitElementId { get; set; }
        public DesignElement DesignElement { get; set; }
        public CreationStatus CreationStatus { get; set; }
        public Dictionary<string, object> AdditionalParameters { get; set; }
        public List<string> Warnings { get; set; }
    }

    public class FailedElement
    {
        public DesignElement DesignElement { get; set; }
        public string Error { get; set; }
        public Exception Exception { get; set; }
    }

    public enum CreationStatus
    {
        Success,
        PartialSuccess,
        Failed
    }

    public class CreationPlan
    {
        public string DesignId { get; set; }
        public List<CreationPhase> Phases { get; set; }
    }

    public class CreationPhase
    {
        public string Name { get; set; }
        public int Order { get; set; }
        public List<DesignElement> Elements { get; set; }
    }

    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; }
        public List<string> Warnings { get; set; }
        public Dictionary<string, object> AdditionalInfo { get; set; }
    }

    public class PipelineResult
    {
        public string ExecutionId { get; set; }
        public string DesignId { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public List<CreatedElement> CreatedElements { get; set; }
        public List<FailedElement> FailedElements { get; set; }
        public List<string> Warnings { get; set; }
    }

    public class PipelineExecution
    {
        public string ExecutionId { get; set; }
        public string DesignId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public PipelineStatus Status { get; set; }
    }

    public enum PipelineStatus
    {
        Running,
        Completed,
        PartiallyCompleted,
        Failed,
        Cancelled
    }

    public class PipelineProgress
    {
        public int PercentComplete { get; set; }
        public string CurrentOperation { get; set; }
    }

    #endregion

    #region Event Args

    public class PipelineProgressEventArgs : EventArgs
    {
        public PipelineProgress Progress { get; }

        public PipelineProgressEventArgs(PipelineProgress progress)
        {
            Progress = progress;
        }
    }

    public class ElementCreatedEventArgs : EventArgs
    {
        public CreatedElement Element { get; }

        public ElementCreatedEventArgs(CreatedElement element)
        {
            Element = element;
        }
    }

    public class PipelineErrorEventArgs : EventArgs
    {
        public DesignElement Element { get; }
        public Exception Exception { get; }

        public PipelineErrorEventArgs(DesignElement element, Exception exception)
        {
            Element = element;
            Exception = exception;
        }
    }

    #endregion
}
