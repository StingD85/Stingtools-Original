// StingBIM.AI.UI.StingBIMApplication
// Revit External Application Entry Point
// This class is loaded by Revit when the add-in starts

using System;
using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;

namespace StingBIM.AI.UI
{
    /// <summary>
    /// Main entry point for the StingBIM Revit add-in.
    /// Implements IExternalApplication to integrate with Revit.
    /// </summary>
    public class StingBIMApplication : IExternalApplication
    {
        // Static reference to the UI application
        public static UIControlledApplication Application { get; private set; }

        /// <summary>
        /// Called when Revit starts up.
        /// </summary>
        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                Application = application;

                // Create ribbon tab
                string tabName = "StingBIM";
                try
                {
                    application.CreateRibbonTab(tabName);
                }
                catch
                {
                    // Tab may already exist
                }

                // Create ribbon panel
                RibbonPanel panel = application.CreateRibbonPanel(tabName, "AI Assistant");

                // Get the assembly path for button icons
                string assemblyPath = Assembly.GetExecutingAssembly().Location;
                string assemblyDir = Path.GetDirectoryName(assemblyPath);

                // Add Chat Panel button
                PushButtonData chatButtonData = new PushButtonData(
                    "StingBIM_Chat",
                    "AI Chat",
                    assemblyPath,
                    "StingBIM.AI.UI.Commands.ShowChatCommand");
                chatButtonData.ToolTip = "Open StingBIM AI Chat Panel";
                chatButtonData.LongDescription = "Chat with the StingBIM AI assistant for help with BIM tasks, " +
                    "parameter management, schedule generation, and more.";

                PushButton chatButton = panel.AddItem(chatButtonData) as PushButton;

                // Add Settings button
                PushButtonData settingsButtonData = new PushButtonData(
                    "StingBIM_Settings",
                    "Settings",
                    assemblyPath,
                    "StingBIM.AI.UI.Commands.ShowSettingsCommand");
                settingsButtonData.ToolTip = "Open StingBIM Settings";

                PushButton settingsButton = panel.AddItem(settingsButtonData) as PushButton;

                // Add About button
                PushButtonData aboutButtonData = new PushButtonData(
                    "StingBIM_About",
                    "About",
                    assemblyPath,
                    "StingBIM.AI.UI.Commands.ShowAboutCommand");
                aboutButtonData.ToolTip = "About StingBIM v7";

                PushButton aboutButton = panel.AddItem(aboutButtonData) as PushButton;

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("StingBIM Error",
                    $"Failed to initialize StingBIM add-in:\n\n{ex.Message}");
                return Result.Failed;
            }
        }

        /// <summary>
        /// Called when Revit shuts down.
        /// </summary>
        public Result OnShutdown(UIControlledApplication application)
        {
            // Cleanup resources
            return Result.Succeeded;
        }
    }
}
