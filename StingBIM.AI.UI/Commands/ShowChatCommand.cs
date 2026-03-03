// StingBIM.AI.UI.Commands.ShowChatCommand
// Command to show the AI Chat panel

using System;
using System.Windows;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Autodesk.Revit.Attributes;
using StingBIM.AI.UI.Windows;

namespace StingBIM.AI.UI.Commands
{
    /// <summary>
    /// Command to open the StingBIM AI Chat panel.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ShowChatCommand : IExternalCommand
    {
        private static ChatWindow _chatWindow;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIApplication uiApp = commandData.Application;

                // Create or show the chat window
                if (_chatWindow == null || !_chatWindow.IsLoaded)
                {
                    _chatWindow = new ChatWindow(uiApp);
                }

                _chatWindow.Show();
                _chatWindow.Activate();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("StingBIM Error", $"Failed to open Chat panel:\n\n{ex.Message}");
                return Result.Failed;
            }
        }
    }
}
