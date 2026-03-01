// StingBIM.AI.UI.Commands.ShowSettingsCommand
// Command to show the Settings panel

using System;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Autodesk.Revit.Attributes;

namespace StingBIM.AI.UI.Commands
{
    /// <summary>
    /// Command to open the StingBIM Settings panel.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ShowSettingsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                TaskDialog dialog = new TaskDialog("StingBIM Settings");
                dialog.MainIcon = TaskDialogIcon.TaskDialogIconInformation;
                dialog.MainInstruction = "StingBIM Settings";
                dialog.MainContent =
                    "Configuration Options:\n\n" +
                    "- GPU Acceleration: Enabled\n" +
                    "- Batch Size: 1000 elements\n" +
                    "- AI Features: Enabled\n" +
                    "- Cache Limit: 500 MB\n" +
                    "- Log Level: Info\n\n" +
                    "Settings are stored in:\n" +
                    "%APPDATA%\\StingBIM\\StingBIM.config.json";

                dialog.Show();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("StingBIM Error", $"Failed to open Settings:\n\n{ex.Message}");
                return Result.Failed;
            }
        }
    }
}
