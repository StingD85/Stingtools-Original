// StingBIM.AI.Creation.Pipeline.ErrorExplainer
// Converts raw exceptions and Revit errors into human-readable messages
// v4 Prompt Reference: Section F Phase 1 Item 6 — replace all generic errors

using System;
using System.Collections.Generic;
using System.Linq;
using NLog;

namespace StingBIM.AI.Creation.Pipeline
{
    /// <summary>
    /// Translates raw exceptions and Revit error codes into user-friendly messages.
    /// Every error the user sees must pass through this class.
    /// </summary>
    public static class ErrorExplainer
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        // Known error patterns → user-friendly explanations
        private static readonly Dictionary<string, ErrorExplanation> KnownPatterns =
            new Dictionary<string, ErrorExplanation>(StringComparer.OrdinalIgnoreCase)
            {
                // Revit API errors
                ["The element cannot be deleted"] = new ErrorExplanation(
                    "This element is locked or part of a group and cannot be removed.",
                    "Try ungrouping or unlocking it first."),

                ["The line is too short"] = new ErrorExplanation(
                    "The wall or line you're trying to create is too short (less than 1mm in Revit).",
                    "Try using a length of at least 100mm."),

                ["cannot be bound twice"] = new ErrorExplanation(
                    "This parameter is already bound to this category.",
                    "No action needed — the binding already exists."),

                ["There is no open transaction"] = new ErrorExplanation(
                    "An internal error occurred — the operation wasn't wrapped in a transaction.",
                    "This is a StingBIM bug. Please try again."),

                ["is not available in the current context"] = new ErrorExplanation(
                    "The Revit command is not available right now.",
                    "Make sure you have a view open and no other dialog is blocking."),

                ["Overlapping walls"] = new ErrorExplanation(
                    "Two walls overlap at this location.",
                    "The walls have been created but may need to be joined. Check the geometry."),

                ["Room is not in a properly enclosed region"] = new ErrorExplanation(
                    "The room boundaries aren't fully closed — there's a gap in the walls.",
                    "Check that all walls are connected. Look for small gaps at corners."),

                ["The wall is too short"] = new ErrorExplanation(
                    "The wall length is below Revit's minimum.",
                    "Minimum wall length is about 1mm. Check your dimensions."),

                ["Cannot make type"] = new ErrorExplanation(
                    "The specified wall/floor/roof type doesn't exist in this project.",
                    "Use the available types in the project, or load the required family first."),

                ["could not be placed"] = new ErrorExplanation(
                    "The element couldn't be placed at the specified location.",
                    "Check that the location is valid and not blocked by other elements."),

                ["out of memory"] = new ErrorExplanation(
                    "Revit ran out of memory.",
                    "Try closing unused views, purging unused families, or saving and restarting Revit."),

                // Worksharing errors
                ["element is owned by"] = new ErrorExplanation(
                    "Another team member has this element checked out.",
                    "Ask them to sync their changes, then reload latest."),

                ["workset is not editable"] = new ErrorExplanation(
                    "The workset containing this element is checked out by another user.",
                    "Check the Worksets dialog to see who owns it."),

                // StingBIM-specific errors
                ["No wall types available"] = new ErrorExplanation(
                    "There are no wall types loaded in the current Revit project.",
                    "Load a wall family or template that includes wall types."),

                ["No floor types available"] = new ErrorExplanation(
                    "There are no floor types loaded in the current Revit project.",
                    "Load a floor family or template."),

                ["Level not found"] = new ErrorExplanation(
                    "The specified level doesn't exist in the project.",
                    "Check available levels in the project browser, or create the level first."),

                ["Document is read-only"] = new ErrorExplanation(
                    "The Revit document is opened as read-only.",
                    "Close and reopen the file with full permissions."),

                ["No active document"] = new ErrorExplanation(
                    "No Revit project is currently open.",
                    "Open a Revit project first, then try again.")
            };

        /// <summary>
        /// Explains an exception in human-readable terms.
        /// </summary>
        public static string Explain(Exception ex)
        {
            if (ex == null)
                return "An unknown error occurred.";

            var message = ex.Message ?? "";

            // Check known patterns
            foreach (var pattern in KnownPatterns)
            {
                if (message.IndexOf(pattern.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Logger.Debug($"Error matched pattern: {pattern.Key}");
                    return $"{pattern.Value.UserMessage}\n{pattern.Value.Suggestion}";
                }
            }

            // Inner exception might have more useful info
            if (ex.InnerException != null)
            {
                var innerExplain = Explain(ex.InnerException);
                if (!innerExplain.StartsWith("An unexpected error"))
                    return innerExplain;
            }

            // Fallback: clean up the raw message
            Logger.Warn($"Unrecognized error: {message}");
            return $"An unexpected error occurred: {CleanMessage(message)}\nPlease try again or rephrase your command.";
        }

        /// <summary>
        /// Explains a Revit failure (from StingBIMFailurePreprocessor).
        /// </summary>
        public static string ExplainFailure(FailureInfo failure)
        {
            if (failure == null)
                return "An unknown Revit failure occurred.";

            var desc = failure.Description ?? "";

            foreach (var pattern in KnownPatterns)
            {
                if (desc.IndexOf(pattern.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return $"{pattern.Value.UserMessage}\n{pattern.Value.Suggestion}";
                }
            }

            var elementCount = failure.AffectedElementIds?.Count ?? 0;
            var elementNote = elementCount > 0 ? $" ({elementCount} element(s) affected)" : "";
            return $"Revit reported: {desc}{elementNote}";
        }

        /// <summary>
        /// Creates a summary string for multiple failures.
        /// </summary>
        public static string SummarizeFailures(IEnumerable<FailureInfo> failures)
        {
            var list = failures?.ToList();
            if (list == null || list.Count == 0)
                return null;

            var warnings = list.Where(f => f.Severity == Autodesk.Revit.DB.FailureSeverity.Warning).ToList();
            var errors = list.Where(f => f.Severity >= Autodesk.Revit.DB.FailureSeverity.Error).ToList();

            var parts = new List<string>();

            if (errors.Count > 0)
            {
                parts.Add($"{errors.Count} error(s):");
                foreach (var e in errors.Take(3))
                    parts.Add($"  - {ExplainFailure(e)}");
                if (errors.Count > 3)
                    parts.Add($"  ... and {errors.Count - 3} more");
            }

            if (warnings.Count > 0)
                parts.Add($"{warnings.Count} warning(s) were auto-resolved.");

            return string.Join("\n", parts);
        }

        /// <summary>
        /// Formats a creation error for display in the chat panel.
        /// </summary>
        public static string FormatCreationError(string elementType, string operation, Exception ex)
        {
            var explanation = Explain(ex);
            return $"Could not {operation} the {elementType}.\n{explanation}";
        }

        private static string CleanMessage(string message)
        {
            // Remove stack trace noise, exception type prefixes
            var clean = message;
            var stackIdx = clean.IndexOf("   at ", StringComparison.Ordinal);
            if (stackIdx > 0) clean = clean.Substring(0, stackIdx).Trim();

            // Remove "Autodesk.Revit.Exceptions." prefix
            clean = clean.Replace("Autodesk.Revit.Exceptions.", "");

            return clean;
        }
    }

    /// <summary>
    /// A human-readable explanation with user message and suggestion.
    /// </summary>
    public class ErrorExplanation
    {
        public string UserMessage { get; }
        public string Suggestion { get; }

        public ErrorExplanation(string userMessage, string suggestion)
        {
            UserMessage = userMessage;
            Suggestion = suggestion;
        }
    }
}
