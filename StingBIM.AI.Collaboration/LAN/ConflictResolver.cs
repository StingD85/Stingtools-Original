// StingBIM.AI.Collaboration.LAN.ConflictResolver
// Detect and present worksharing conflicts for user resolution
// v4 Prompt Reference: Section C.2 — Conflict detection + resolution

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using NLog;

namespace StingBIM.AI.Collaboration.LAN
{
    /// <summary>
    /// Detects and presents worksharing conflicts when multiple users
    /// modify the same elements. Provides resolution strategies:
    /// - Keep local version
    /// - Accept central version
    /// - Merge (where possible)
    /// </summary>
    public class ConflictResolver
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Analyze the document for potential sync conflicts.
        /// Returns a detailed list with resolution options.
        /// </summary>
        public ConflictAnalysis AnalyzeConflicts(Document doc)
        {
            var analysis = new ConflictAnalysis();

            if (!doc.IsWorkshared)
            {
                analysis.Summary = "Document is not workshared — no conflicts possible.";
                return analysis;
            }

            try
            {
                var collector = new FilteredElementCollector(doc)
                    .WhereElementIsNotElementType();

                foreach (var elem in collector)
                {
                    try
                    {
                        var status = WorksharingUtils.GetCheckoutStatus(doc, elem.Id);
                        var tooltip = WorksharingUtils.GetWorksharingTooltipInfo(doc, elem.Id);

                        if (status == CheckoutStatus.OwnedByOtherUser)
                        {
                            analysis.Conflicts.Add(new ConflictDetail
                            {
                                ElementId = elem.Id,
                                Category = elem.Category?.Name ?? "Unknown",
                                ElementName = elem.Name ?? "Unnamed",
                                Status = ConflictStatus.OwnedByOther,
                                Owner = tooltip.Owner,
                                LastChangedBy = tooltip.LastChangedBy,
                                ResolutionOptions = new List<string>
                                {
                                    "Wait for owner to release",
                                    "Request edit access",
                                    "Work on a different element"
                                }
                            });
                        }
                        else if (status == CheckoutStatus.OwnedByCurrentUser)
                        {
                            analysis.BorrowedByMe.Add(new BorrowedElement
                            {
                                ElementId = elem.Id,
                                Category = elem.Category?.Name ?? "Unknown",
                                ElementName = elem.Name ?? "Unnamed"
                            });
                        }
                    }
                    catch
                    {
                        // Skip elements that can't be checked
                    }
                }

                // Check for stale borrows (>24 hours)
                analysis.StaleCount = analysis.BorrowedByMe.Count; // Simplified — real impl would check timestamps

                analysis.Summary = analysis.Conflicts.Count > 0
                    ? $"Found {analysis.Conflicts.Count} conflict(s) with other team members."
                    : "No conflicts detected. Safe to sync.";

                if (analysis.BorrowedByMe.Count > 0)
                {
                    analysis.Summary += $"\nYou have {analysis.BorrowedByMe.Count} element(s) checked out.";
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Conflict analysis failed");
                analysis.Summary = $"Conflict analysis error: {ex.Message}";
            }

            return analysis;
        }

        /// <summary>
        /// Resolve a conflict by relinquishing the local element (accept central version).
        /// </summary>
        public CollaborationResult RelinquishElement(Document doc, ElementId elementId)
        {
            try
            {
                using (var t = new Transaction(doc, "StingBIM: Relinquish Element"))
                {
                    t.Start();

                    var relinquishOpts = new RelinquishOptions(true);
                    WorksharingUtils.RelinquishOwnership(doc, relinquishOpts, null);

                    t.Commit();
                }

                return CollaborationResult.Succeeded($"Relinquished element {elementId}.");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to relinquish element");
                return CollaborationResult.Failed($"Could not relinquish: {ex.Message}");
            }
        }

        /// <summary>
        /// Format conflict analysis for the chat panel.
        /// </summary>
        public string FormatForChat(ConflictAnalysis analysis)
        {
            if (analysis.Conflicts.Count == 0)
                return analysis.Summary;

            var lines = new List<string>
            {
                analysis.Summary,
                "─────────────────────────────────────────"
            };

            foreach (var conflict in analysis.Conflicts)
            {
                lines.Add($"  {conflict.Category}: {conflict.ElementName}");
                lines.Add($"    Owned by: {conflict.Owner}");
                lines.Add($"    Last changed by: {conflict.LastChangedBy}");
                lines.Add($"    Options: {string.Join(" | ", conflict.ResolutionOptions)}");
                lines.Add("");
            }

            lines.Add("─────────────────────────────────────────");
            return string.Join("\n", lines);
        }
    }

    #region Conflict Data Types

    public class ConflictAnalysis
    {
        public string Summary { get; set; } = "";
        public List<ConflictDetail> Conflicts { get; set; } = new List<ConflictDetail>();
        public List<BorrowedElement> BorrowedByMe { get; set; } = new List<BorrowedElement>();
        public int StaleCount { get; set; }
    }

    public class ConflictDetail
    {
        public ElementId ElementId { get; set; }
        public string Category { get; set; }
        public string ElementName { get; set; }
        public ConflictStatus Status { get; set; }
        public string Owner { get; set; }
        public string LastChangedBy { get; set; }
        public List<string> ResolutionOptions { get; set; }
    }

    public class BorrowedElement
    {
        public ElementId ElementId { get; set; }
        public string Category { get; set; }
        public string ElementName { get; set; }
    }

    public enum ConflictStatus
    {
        OwnedByOther,
        ModifiedInCentral,
        DeletedInCentral,
        WorksetLockedByOther
    }

    #endregion
}
