// ============================================================================
// StingBIM Revit - Integration Bridge
// Connects AI layer to Revit API for element manipulation
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using StingBIM.Core.Transactions;
using StingBIM.Data.Parameters;

namespace StingBIM.Revit.Integration
{
    /// <summary>
    /// Integration bridge connecting StingBIM AI to Revit API.
    /// Provides abstracted interface for element operations.
    /// </summary>
    public class RevitIntegrationBridge : IRevitBridge
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly TransactionManager _transactionManager;
        private readonly SharedParameterManager _parameterManager;
        private readonly object _lock = new object();

        // Event handlers for Revit events
        public event EventHandler<ElementChangedEventArgs> ElementChanged;
        public event EventHandler<SelectionChangedEventArgs> SelectionChanged;
        public event EventHandler<DocumentChangedEventArgs> DocumentChanged;

        // Current document context
        private DocumentContext _currentDocument;
        public DocumentContext CurrentDocument => _currentDocument;

        public RevitIntegrationBridge(
            TransactionManager transactionManager,
            SharedParameterManager parameterManager)
        {
            _transactionManager = transactionManager;
            _parameterManager = parameterManager;
        }

        #region Document Operations

        /// <summary>
        /// Sets the current document context.
        /// </summary>
        public void SetDocumentContext(DocumentContext context)
        {
            lock (_lock)
            {
                _currentDocument = context;
                DocumentChanged?.Invoke(this, new DocumentChangedEventArgs(context));
                Logger.Info($"Document context set: {context?.DocumentPath ?? "None"}");
            }
        }

        /// <summary>
        /// Gets document information.
        /// </summary>
        public DocumentInfo GetDocumentInfo()
        {
            if (_currentDocument == null)
            {
                throw new InvalidOperationException("No document context set");
            }

            return new DocumentInfo
            {
                DocumentPath = _currentDocument.DocumentPath,
                Title = _currentDocument.Title,
                IsWorkshared = _currentDocument.IsWorkshared,
                ActiveView = _currentDocument.ActiveViewName,
                Units = _currentDocument.Units
            };
        }

        #endregion

        #region Element Operations

        /// <summary>
        /// Gets selected elements in the current document.
        /// </summary>
        public async Task<IEnumerable<RevitElement>> GetSelectedElementsAsync(
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                if (_currentDocument == null)
                {
                    return Enumerable.Empty<RevitElement>();
                }

                // In real implementation, this would query Revit API
                // For now, return mock data from document context
                return _currentDocument.SelectedElements ?? Enumerable.Empty<RevitElement>();
            }, cancellationToken);
        }

        /// <summary>
        /// Gets elements by category.
        /// </summary>
        public async Task<IEnumerable<RevitElement>> GetElementsByCategoryAsync(
            string categoryName,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                if (_currentDocument == null)
                {
                    return Enumerable.Empty<RevitElement>();
                }

                // Filter elements by category
                return _currentDocument.AllElements?
                    .Where(e => e.Category.Equals(categoryName, StringComparison.OrdinalIgnoreCase))
                    ?? Enumerable.Empty<RevitElement>();
            }, cancellationToken);
        }

        /// <summary>
        /// Gets element by ID.
        /// </summary>
        public async Task<RevitElement> GetElementByIdAsync(
            string elementId,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                return _currentDocument?.AllElements?
                    .FirstOrDefault(e => e.Id == elementId);
            }, cancellationToken);
        }

        /// <summary>
        /// Creates a new element in the document.
        /// </summary>
        public async Task<RevitElement> CreateElementAsync(
            ElementCreationRequest request,
            CancellationToken cancellationToken = default)
        {
            RevitElement createdElement = null;

            var result = await _transactionManager.ExecuteAsync(
                $"Create {request.ElementType}",
                async (context, ct) =>
                {
                    // In real implementation, this would create element via Revit API
                    createdElement = new RevitElement
                    {
                        Id = Guid.NewGuid().ToString(),
                        Category = request.Category,
                        TypeName = request.ElementType,
                        Name = request.Name,
                        Location = request.Location,
                        Parameters = request.Parameters ?? new Dictionary<string, object>()
                    };

                    context.LogOperation($"Created element: {createdElement.Id}");
                    await Task.CompletedTask;
                },
                cancellationToken);

            if (!result.Success)
            {
                throw new InvalidOperationException($"Failed to create element: {result.ErrorMessage}");
            }

            ElementChanged?.Invoke(this, new ElementChangedEventArgs(createdElement, ChangeType.Created));
            Logger.Info($"Created element: {createdElement.Id} ({request.ElementType})");

            return createdElement;
        }

        /// <summary>
        /// Modifies an existing element.
        /// </summary>
        public async Task<bool> ModifyElementAsync(
            string elementId,
            Dictionary<string, object> modifications,
            CancellationToken cancellationToken = default)
        {
            var element = await GetElementByIdAsync(elementId, cancellationToken);
            if (element == null)
            {
                throw new ArgumentException($"Element not found: {elementId}");
            }

            var result = await _transactionManager.ExecuteAsync(
                $"Modify Element {elementId}",
                async (context, ct) =>
                {
                    foreach (var mod in modifications)
                    {
                        element.Parameters[mod.Key] = mod.Value;
                        context.LogOperation($"Set {mod.Key} = {mod.Value}");
                    }
                    await Task.CompletedTask;
                },
                cancellationToken);

            if (result.Success)
            {
                ElementChanged?.Invoke(this, new ElementChangedEventArgs(element, ChangeType.Modified));
            }

            return result.Success;
        }

        /// <summary>
        /// Deletes an element.
        /// </summary>
        public async Task<bool> DeleteElementAsync(
            string elementId,
            CancellationToken cancellationToken = default)
        {
            var element = await GetElementByIdAsync(elementId, cancellationToken);
            if (element == null)
            {
                return false;
            }

            var result = await _transactionManager.ExecuteAsync(
                $"Delete Element {elementId}",
                async (context, ct) =>
                {
                    // In real implementation, delete via Revit API
                    _currentDocument?.AllElements?.Remove(element);
                    context.LogOperation($"Deleted element: {elementId}");
                    await Task.CompletedTask;
                },
                cancellationToken);

            if (result.Success)
            {
                ElementChanged?.Invoke(this, new ElementChangedEventArgs(element, ChangeType.Deleted));
            }

            return result.Success;
        }

        #endregion

        #region Parameter Operations

        /// <summary>
        /// Gets parameter value from an element.
        /// </summary>
        public async Task<object> GetParameterValueAsync(
            string elementId,
            string parameterName,
            CancellationToken cancellationToken = default)
        {
            var element = await GetElementByIdAsync(elementId, cancellationToken);
            if (element == null)
            {
                throw new ArgumentException($"Element not found: {elementId}");
            }

            return element.Parameters.TryGetValue(parameterName, out var value) ? value : null;
        }

        /// <summary>
        /// Sets parameter value on an element.
        /// </summary>
        public async Task<bool> SetParameterValueAsync(
            string elementId,
            string parameterName,
            object value,
            CancellationToken cancellationToken = default)
        {
            return await ModifyElementAsync(
                elementId,
                new Dictionary<string, object> { { parameterName, value } },
                cancellationToken);
        }

        /// <summary>
        /// Gets all parameters for an element.
        /// </summary>
        public async Task<Dictionary<string, object>> GetAllParametersAsync(
            string elementId,
            CancellationToken cancellationToken = default)
        {
            var element = await GetElementByIdAsync(elementId, cancellationToken);
            return element?.Parameters ?? new Dictionary<string, object>();
        }

        /// <summary>
        /// Applies a design proposal to the document.
        /// </summary>
        public async Task<ApplyProposalResult> ApplyDesignProposalAsync(
            DesignProposal proposal,
            CancellationToken cancellationToken = default)
        {
            var result = new ApplyProposalResult
            {
                ProposalId = proposal.Id,
                StartTime = DateTime.UtcNow
            };

            var transactionResult = await _transactionManager.ExecuteAsync(
                $"Apply Design Proposal: {proposal.Name}",
                async (context, ct) =>
                {
                    foreach (var modification in proposal.Modifications)
                    {
                        ct.ThrowIfCancellationRequested();

                        try
                        {
                            await ApplyModificationAsync(modification, ct);
                            result.AppliedModifications.Add(modification);
                            context.LogOperation($"Applied: {modification.Description}");
                        }
                        catch (Exception ex)
                        {
                            result.FailedModifications.Add((modification, ex.Message));
                            Logger.Warn(ex, $"Failed to apply modification: {modification.Description}");
                        }
                    }
                },
                cancellationToken);

            result.Success = transactionResult.Success;
            result.EndTime = DateTime.UtcNow;

            Logger.Info($"Applied proposal {proposal.Id}: {result.AppliedModifications.Count} applied, {result.FailedModifications.Count} failed");

            return result;
        }

        private async Task ApplyModificationAsync(
            ProposalModification modification,
            CancellationToken cancellationToken)
        {
            switch (modification.Type)
            {
                case ModificationType.Create:
                    await CreateElementAsync(new ElementCreationRequest
                    {
                        ElementType = modification.ElementType,
                        Category = modification.Category,
                        Name = modification.Name,
                        Location = modification.Location,
                        Parameters = modification.Parameters
                    }, cancellationToken);
                    break;

                case ModificationType.Modify:
                    await ModifyElementAsync(
                        modification.ElementId,
                        modification.Parameters,
                        cancellationToken);
                    break;

                case ModificationType.Delete:
                    await DeleteElementAsync(modification.ElementId, cancellationToken);
                    break;
            }
        }

        #endregion

        #region View Operations

        /// <summary>
        /// Gets all views in the document.
        /// </summary>
        public IEnumerable<ViewInfo> GetViews()
        {
            return _currentDocument?.Views ?? Enumerable.Empty<ViewInfo>();
        }

        /// <summary>
        /// Sets the active view.
        /// </summary>
        public void SetActiveView(string viewId)
        {
            if (_currentDocument != null)
            {
                _currentDocument.ActiveViewName = viewId;
                Logger.Info($"Set active view: {viewId}");
            }
        }

        #endregion
    }

    #region Interfaces and Supporting Types

    /// <summary>
    /// Interface for Revit integration operations.
    /// </summary>
    public interface IRevitBridge
    {
        DocumentContext CurrentDocument { get; }
        void SetDocumentContext(DocumentContext context);
        Task<IEnumerable<RevitElement>> GetSelectedElementsAsync(CancellationToken cancellationToken = default);
        Task<RevitElement> CreateElementAsync(ElementCreationRequest request, CancellationToken cancellationToken = default);
        Task<bool> ModifyElementAsync(string elementId, Dictionary<string, object> modifications, CancellationToken cancellationToken = default);
        Task<bool> DeleteElementAsync(string elementId, CancellationToken cancellationToken = default);
        Task<ApplyProposalResult> ApplyDesignProposalAsync(DesignProposal proposal, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Document context for Revit operations.
    /// </summary>
    public class DocumentContext
    {
        public string DocumentPath { get; set; }
        public string Title { get; set; }
        public bool IsWorkshared { get; set; }
        public string ActiveViewName { get; set; }
        public string Units { get; set; } = "Metric";
        public List<RevitElement> SelectedElements { get; set; } = new List<RevitElement>();
        public List<RevitElement> AllElements { get; set; } = new List<RevitElement>();
        public List<ViewInfo> Views { get; set; } = new List<ViewInfo>();
    }

    /// <summary>
    /// Revit element representation.
    /// </summary>
    public class RevitElement
    {
        public string Id { get; set; }
        public string Category { get; set; }
        public string TypeName { get; set; }
        public string Name { get; set; }
        public string FamilyName { get; set; }
        public LocationPoint Location { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Location point in 3D space.
    /// </summary>
    public class LocationPoint
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
    }

    /// <summary>
    /// Element creation request.
    /// </summary>
    public class ElementCreationRequest
    {
        public string ElementType { get; set; }
        public string Category { get; set; }
        public string Name { get; set; }
        public string FamilyName { get; set; }
        public LocationPoint Location { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
    }

    /// <summary>
    /// Design proposal for batch operations.
    /// </summary>
    public class DesignProposal
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public List<ProposalModification> Modifications { get; set; } = new List<ProposalModification>();
    }

    /// <summary>
    /// Single modification in a proposal.
    /// </summary>
    public class ProposalModification
    {
        public ModificationType Type { get; set; }
        public string ElementId { get; set; }
        public string ElementType { get; set; }
        public string Category { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public LocationPoint Location { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
    }

    public enum ModificationType
    {
        Create,
        Modify,
        Delete,
        Move
    }

    /// <summary>
    /// Result of applying a design proposal.
    /// </summary>
    public class ApplyProposalResult
    {
        public string ProposalId { get; set; }
        public bool Success { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public List<ProposalModification> AppliedModifications { get; set; } = new List<ProposalModification>();
        public List<(ProposalModification Modification, string Error)> FailedModifications { get; set; } = new List<(ProposalModification, string)>();

        public TimeSpan Duration => EndTime - StartTime;
    }

    /// <summary>
    /// Document information.
    /// </summary>
    public class DocumentInfo
    {
        public string DocumentPath { get; set; }
        public string Title { get; set; }
        public bool IsWorkshared { get; set; }
        public string ActiveView { get; set; }
        public string Units { get; set; }
    }

    /// <summary>
    /// View information.
    /// </summary>
    public class ViewInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string ViewType { get; set; }
    }

    // Event Args
    public class ElementChangedEventArgs : EventArgs
    {
        public RevitElement Element { get; }
        public ChangeType ChangeType { get; }

        public ElementChangedEventArgs(RevitElement element, ChangeType changeType)
        {
            Element = element;
            ChangeType = changeType;
        }
    }

    public class SelectionChangedEventArgs : EventArgs
    {
        public IEnumerable<RevitElement> SelectedElements { get; }

        public SelectionChangedEventArgs(IEnumerable<RevitElement> elements)
        {
            SelectedElements = elements;
        }
    }

    public class DocumentChangedEventArgs : EventArgs
    {
        public DocumentContext Document { get; }

        public DocumentChangedEventArgs(DocumentContext document)
        {
            Document = document;
        }
    }

    public enum ChangeType
    {
        Created,
        Modified,
        Deleted
    }

    #endregion
}
