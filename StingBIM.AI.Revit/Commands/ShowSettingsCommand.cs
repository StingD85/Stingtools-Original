using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using NLog;

namespace StingBIM.AI.Revit.Commands
{
    /// <summary>
    /// External command that opens the Settings panel.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ShowSettingsCommand : IExternalCommand
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                Logger.Info("Opening StingBIM Settings...");
                var settingsPanel = new UI.Panels.SettingsPanel();
                settingsPanel.ShowDialog();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to open Settings");
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
