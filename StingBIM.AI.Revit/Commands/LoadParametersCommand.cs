using System;
using System.Windows;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using NLog;

namespace StingBIM.AI.Revit.Commands
{
    /// <summary>
    /// External command that loads and binds shared parameters to the active document.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class LoadParametersCommand : IExternalCommand
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

                Logger.Info("Loading StingBIM parameters...");

                var loader = new StingBIM.Data.Parameters.ParameterLoader();

                // Load parameters asynchronously
                var task = System.Threading.Tasks.Task.Run(async () =>
                {
                    var parameters = await loader.LoadAsync();
                    return parameters;
                });

                var loadedParameters = task.Result;
                int count = loadedParameters?.Count ?? 0;

                MessageBox.Show(
                    $"Loaded {count} ISO 19650-compliant parameters.\n\nUse Category Binder to apply bindings to the active document.",
                    "StingBIM Parameters",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                Logger.Info($"Loaded {count} parameters");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to load parameters");
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
