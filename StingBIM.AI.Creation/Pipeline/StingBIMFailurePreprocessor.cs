// StingBIM.AI.Creation.Pipeline.StingBIMFailurePreprocessor
// Revit failure handling — suppress warnings, surface errors as human-readable messages
// v4 Prompt Reference: Section A.0.3 Sacred Rule 3, Section E.2 Transaction Patterns

using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using NLog;

namespace StingBIM.AI.Creation.Pipeline
{
    /// <summary>
    /// Preprocesses Revit failures during transactions.
    /// Suppresses non-critical warnings (duplicates, overlaps) while
    /// surfacing errors to the user through ErrorExplainer.
    /// Must be attached to every transaction.
    /// </summary>
    public class StingBIMFailurePreprocessor : IFailuresPreprocessor
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly List<FailureInfo> _capturedWarnings = new List<FailureInfo>();
        private readonly List<FailureInfo> _capturedErrors = new List<FailureInfo>();

        /// <summary>
        /// Warnings captured during the transaction (for reporting).
        /// </summary>
        public IReadOnlyList<FailureInfo> CapturedWarnings => _capturedWarnings;

        /// <summary>
        /// Errors captured during the transaction (for reporting).
        /// </summary>
        public IReadOnlyList<FailureInfo> CapturedErrors => _capturedErrors;

        /// <summary>
        /// If true, automatically deletes warning messages (default behavior).
        /// </summary>
        public bool SuppressWarnings { get; set; } = true;

        public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
        {
            try
            {
                var failureMessages = failuresAccessor.GetFailureMessages();

                foreach (var failure in failureMessages)
                {
                    var severity = failure.GetSeverity();
                    var description = failure.GetDescriptionText();
                    var failureId = failure.GetFailureDefinitionId();
                    var elementIds = failure.GetFailingElements();

                    var info = new FailureInfo
                    {
                        Severity = severity,
                        Description = description,
                        FailureDefinitionId = failureId,
                        AffectedElementIds = elementIds
                    };

                    if (severity == FailureSeverity.Warning)
                    {
                        _capturedWarnings.Add(info);
                        Logger.Debug($"Revit warning suppressed: {description}");

                        if (SuppressWarnings)
                        {
                            failuresAccessor.DeleteWarning(failure);
                        }
                    }
                    else if (severity == FailureSeverity.Error)
                    {
                        _capturedErrors.Add(info);
                        Logger.Warn($"Revit error: {description}");

                        // Try to resolve the error if possible
                        if (failure.HasResolutions())
                        {
                            var resolution = failure.GetDefaultResolution();
                            if (resolution != null)
                            {
                                failuresAccessor.ResolveFailure(failure);
                                Logger.Info($"Auto-resolved error: {description}");
                            }
                        }
                    }
                    else if (severity == FailureSeverity.DocumentCorruption)
                    {
                        _capturedErrors.Add(info);
                        Logger.Error($"Document corruption detected: {description}");
                        return FailureProcessingResult.ProceedWithRollBack;
                    }
                }

                if (_capturedErrors.Count > 0 &&
                    _capturedErrors.Exists(e => e.Severity == FailureSeverity.Error))
                {
                    // Check if all errors were resolved
                    var remaining = failuresAccessor.GetFailureMessages();
                    if (remaining.Count > 0)
                    {
                        var unresolvedErrors = false;
                        foreach (var msg in remaining)
                        {
                            if (msg.GetSeverity() >= FailureSeverity.Error)
                            {
                                unresolvedErrors = true;
                                break;
                            }
                        }
                        if (unresolvedErrors)
                            return FailureProcessingResult.ProceedWithRollBack;
                    }
                }

                return FailureProcessingResult.Continue;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Exception in failure preprocessor");
                return FailureProcessingResult.Continue;
            }
        }
    }

    /// <summary>
    /// Captured failure information for reporting.
    /// </summary>
    public class FailureInfo
    {
        public FailureSeverity Severity { get; set; }
        public string Description { get; set; }
        public FailureDefinitionId FailureDefinitionId { get; set; }
        public ICollection<ElementId> AffectedElementIds { get; set; }
    }
}
