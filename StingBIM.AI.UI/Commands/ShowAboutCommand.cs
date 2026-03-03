// StingBIM.AI.UI.Commands.ShowAboutCommand
// Command to show the About dialog

using System;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Autodesk.Revit.Attributes;

namespace StingBIM.AI.UI.Commands
{
    /// <summary>
    /// Command to show the StingBIM About dialog.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ShowAboutCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                TaskDialog dialog = new TaskDialog("About StingBIM");
                dialog.MainIcon = TaskDialogIcon.TaskDialogIconInformation;
                dialog.MainInstruction = "StingBIM v7";
                dialog.MainContent =
                    "Comprehensive BIM Parameter Management with AI\n\n" +
                    "Features:\n" +
                    "- 818 ISO 19650-compliant shared parameters\n" +
                    "- 146 professional schedule templates\n" +
                    "- 2,450+ materials with thermal/cost data\n" +
                    "- 52 engineering formulas\n" +
                    "- 32 international building standards\n" +
                    "- Offline AI assistant\n\n" +
                    "Target: Autodesk Revit 2025\n" +
                    "Framework: .NET 8.0";
                dialog.FooterText = "Copyright 2026 StingBIM. All rights reserved.";

                dialog.Show();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
