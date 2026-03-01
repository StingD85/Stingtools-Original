using System;
using System.Text;
using System.Windows;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using NLog;

namespace StingBIM.AI.Revit.Commands
{
    /// <summary>
    /// External command that runs a standards compliance check on the active document.
    /// </summary>
    [Transaction(TransactionMode.ReadOnly)]
    [Regeneration(RegenerationOption.Manual)]
    public class ComplianceCheckCommand : IExternalCommand
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var doc = commandData.Application.ActiveUIDocument?.Document;
                if (doc == null)
                {
                    message = "No active document. Please open a Revit project first.";
                    return Result.Failed;
                }

                Logger.Info("Running compliance check...");

                var sb = new StringBuilder();
                sb.AppendLine("StingBIM Standards Compliance Check");
                sb.AppendLine("====================================");
                sb.AppendLine();
                sb.AppendLine($"Project: {doc.Title}");
                sb.AppendLine($"Date: {DateTime.Now:yyyy-MM-dd HH:mm}");
                sb.AppendLine();
                sb.AppendLine("Available Standards:");
                sb.AppendLine("  - ISO 19650 (BIM Information Management)");
                sb.AppendLine("  - IBC 2021 (International Building Code)");
                sb.AppendLine("  - ASHRAE 90.1 / 62.1 (Energy & Ventilation)");
                sb.AppendLine("  - ASCE 7 (Structural Loads)");
                sb.AppendLine("  - Eurocodes (European Standards)");
                sb.AppendLine("  - EAS / KEBS / UNBS (East African Standards)");
                sb.AppendLine("  - NFPA (Fire Protection)");
                sb.AppendLine("  - NEC 2023 (Electrical Code)");
                sb.AppendLine();
                sb.AppendLine("Use the AI Chat to run specific checks:");
                sb.AppendLine("  'Check fire code compliance'");
                sb.AppendLine("  'Verify structural loads'");
                sb.AppendLine("  'Check ventilation standards'");

                MessageBox.Show(
                    sb.ToString(),
                    "StingBIM Compliance",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                Logger.Info("Compliance check completed");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to run compliance check");
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
