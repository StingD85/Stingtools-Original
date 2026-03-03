using System;
using System.Reflection;
using Autodesk.Revit.UI;
using NLog;

namespace StingBIM.AI.Revit
{
    /// <summary>
    /// Main Revit add-in entry point implementing IExternalApplication.
    /// Revit loads this class on startup via the .addin manifest.
    /// </summary>
    public class RevitApplication : IExternalApplication
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private const string TabName = "StingBIM";

        public Result OnStartup(UIControlledApplication application)
        {
            Logger.Info("StingBIM AI v7 starting up...");

            try
            {
                CreateRibbonUI(application);
                Logger.Info("StingBIM AI v7 loaded successfully");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to initialize StingBIM AI");
                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            Logger.Info("StingBIM AI v7 shutting down...");
            return Result.Succeeded;
        }

        private void CreateRibbonUI(UIControlledApplication application)
        {
            // Create the StingBIM tab
            try
            {
                application.CreateRibbonTab(TabName);
            }
            catch (Autodesk.Revit.Exceptions.ArgumentException)
            {
                // Tab already exists
                Logger.Debug("StingBIM tab already exists");
            }

            string assemblyPath = Assembly.GetExecutingAssembly().Location;

            // ── AI Assistant Panel ──
            RibbonPanel aiPanel = application.CreateRibbonPanel(TabName, "AI Assistant");

            var chatButton = new PushButtonData(
                "StingBIM_AIChat",
                "AI Chat",
                assemblyPath,
                "StingBIM.AI.Revit.Commands.ShowChatCommand");
            chatButton.ToolTip = "Open the StingBIM AI Chat assistant";
            chatButton.LongDescription =
                "Launch the AI-powered chat panel for BIM automation, " +
                "parameter management, schedule generation, and standards compliance.";
            aiPanel.AddItem(chatButton);

            // ── Data & Parameters Panel ──
            RibbonPanel dataPanel = application.CreateRibbonPanel(TabName, "Data");

            var paramsButton = new PushButtonData(
                "StingBIM_Parameters",
                "Load\nParameters",
                assemblyPath,
                "StingBIM.AI.Revit.Commands.LoadParametersCommand");
            paramsButton.ToolTip = "Load 818 ISO 19650-compliant shared parameters";
            paramsButton.LongDescription =
                "Loads the StingBIM shared parameter definitions and prepares them for binding to categories.";
            dataPanel.AddItem(paramsButton);

            // ── Standards & Compliance Panel ──
            RibbonPanel standardsPanel = application.CreateRibbonPanel(TabName, "Standards");

            var complianceButton = new PushButtonData(
                "StingBIM_Compliance",
                "Compliance\nCheck",
                assemblyPath,
                "StingBIM.AI.Revit.Commands.ComplianceCheckCommand");
            complianceButton.ToolTip = "Run standards compliance check";
            complianceButton.LongDescription =
                "Check the active document against 32 international building standards " +
                "including IBC, ASHRAE, Eurocodes, and East African standards.";
            standardsPanel.AddItem(complianceButton);

            // ── Settings Panel ──
            RibbonPanel settingsPanel = application.CreateRibbonPanel(TabName, "Settings");

            var settingsButton = new PushButtonData(
                "StingBIM_Settings",
                "Settings",
                assemblyPath,
                "StingBIM.AI.Revit.Commands.ShowSettingsCommand");
            settingsButton.ToolTip = "Configure StingBIM AI settings";
            settingsButton.LongDescription =
                "Configure voice input, response verbosity, AI behavior, " +
                "and privacy settings for the StingBIM AI assistant.";
            settingsPanel.AddItem(settingsButton);

            Logger.Info("StingBIM ribbon created: 4 panels, 4 buttons");
        }
    }
}
