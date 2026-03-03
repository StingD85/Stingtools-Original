// ============================================================================
// StingBIM AI - Revit Application Entry Point
// IExternalApplication implementation for Revit add-in initialization
// Creates ribbon UI and initializes AI subsystems
// ============================================================================

using System;
using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB.Events;
using NLog;
using StingBIM.Core.Configuration;
using StingBIM.Core.Services;

namespace StingBIM.Revit.Commands
{
    /// <summary>
    /// Main entry point for StingBIM AI Revit add-in.
    /// Implements IExternalApplication for Revit integration.
    /// </summary>
    public class StingBIMApplication : IExternalApplication
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        // Static reference to the UI application for use by commands
        internal static UIControlledApplication UiApp { get; private set; }

        // AI system components
        private static AISystemManager _aiManager;
        internal static AISystemManager AIManager => _aiManager;

        // Dockable pane IDs
        internal static readonly DockablePaneId ChatPanelId =
            new DockablePaneId(new Guid("8A3F5B2C-1D4E-4F6A-9B8C-7D2E1F0A3B63"));

        internal static readonly DockablePaneId SettingsPanelId =
            new DockablePaneId(new Guid("8A3F5B2C-1D4E-4F6A-9B8C-7D2E1F0A3B64"));

        #region IExternalApplication Implementation

        /// <summary>
        /// Called when Revit starts. Initialize the add-in.
        /// </summary>
        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                UiApp = application;

                Logger.Info("==============================================");
                Logger.Info("StingBIM AI v7.0 - Starting up...");
                Logger.Info("==============================================");

                // Initialize configuration
                InitializeConfiguration();

                // Initialize AI subsystems
                InitializeAISubsystems();

                // Create ribbon UI
                CreateRibbonUI(application);

                // Register dockable panes
                RegisterDockablePanes(application);

                // Subscribe to Revit events
                SubscribeToRevitEvents(application);

                Logger.Info("StingBIM AI initialized successfully");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to initialize StingBIM AI");
                TaskDialog.Show("StingBIM AI Error",
                    $"Failed to initialize StingBIM AI:\n\n{ex.Message}\n\nPlease check the log file for details.");
                return Result.Failed;
            }
        }

        /// <summary>
        /// Called when Revit shuts down. Clean up resources.
        /// </summary>
        public Result OnShutdown(UIControlledApplication application)
        {
            try
            {
                Logger.Info("StingBIM AI shutting down...");

                // Unsubscribe from events
                UnsubscribeFromRevitEvents(application);

                // Shutdown AI subsystems
                _aiManager?.Shutdown();

                // Save any pending data
                SavePendingData();

                Logger.Info("StingBIM AI shutdown complete");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during StingBIM AI shutdown");
                return Result.Failed;
            }
        }

        #endregion

        #region Initialization

        private void InitializeConfiguration()
        {
            // Get assembly location for relative paths
            var assemblyPath = Assembly.GetExecutingAssembly().Location;
            var assemblyDir = Path.GetDirectoryName(assemblyPath);

            // Initialize StingBIM configuration
            var configPath = Path.Combine(assemblyDir, "config", "stingbim.json");
            StingBIMConfiguration.Initialize(configPath);

            Logger.Info($"Configuration loaded from: {configPath}");
        }

        private void InitializeAISubsystems()
        {
            Logger.Info("Initializing AI subsystems...");

            // Create AI system manager
            _aiManager = new AISystemManager();

            // Initialize in background to not block Revit startup
            System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    await _aiManager.InitializeAsync();
                    Logger.Info("AI subsystems initialized");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to initialize AI subsystems");
                }
            });
        }

        #endregion

        #region Ribbon UI

        private void CreateRibbonUI(UIControlledApplication application)
        {
            Logger.Info("Creating StingBIM ribbon...");

            // Create the StingBIM tab
            const string tabName = "StingBIM AI";
            application.CreateRibbonTab(tabName);

            // Create panels
            CreateAIPanel(application, tabName);
            CreateAnalysisPanel(application, tabName);
            CreateAutomationPanel(application, tabName);
            CreateSettingsPanel(application, tabName);

            Logger.Info("Ribbon created successfully");
        }

        private void CreateAIPanel(UIControlledApplication app, string tabName)
        {
            var panel = app.CreateRibbonPanel(tabName, "AI Assistant");
            var assemblyPath = Assembly.GetExecutingAssembly().Location;

            // AI Assistant button (large)
            var aiAssistantData = new PushButtonData(
                "AIAssistant",
                "AI\nAssistant",
                assemblyPath,
                "StingBIM.Revit.Commands.AIAssistantCommand")
            {
                ToolTip = "Open the StingBIM AI Assistant",
                LongDescription = "Launch the AI-powered chat interface for natural language design commands. " +
                                  "Ask questions, create elements, check compliance, and more using plain English.",
                LargeImage = GetEmbeddedImage("ai_assistant_32"),
                Image = GetEmbeddedImage("ai_assistant_16")
            };
            panel.AddItem(aiAssistantData);

            // Voice Command button
            var voiceData = new PushButtonData(
                "VoiceCommand",
                "Voice\nCommand",
                assemblyPath,
                "StingBIM.Revit.Commands.VoiceCommandCommand")
            {
                ToolTip = "Speak a design command",
                LongDescription = "Use your microphone to speak design commands. " +
                                  "StingBIM will transcribe and execute your spoken instructions.",
                LargeImage = GetEmbeddedImage("voice_32"),
                Image = GetEmbeddedImage("voice_16")
            };
            panel.AddItem(voiceData);
        }

        private void CreateAnalysisPanel(UIControlledApplication app, string tabName)
        {
            var panel = app.CreateRibbonPanel(tabName, "Analysis");
            var assemblyPath = Assembly.GetExecutingAssembly().Location;

            // Compliance Check
            var complianceData = new PushButtonData(
                "ComplianceCheck",
                "Check\nCompliance",
                assemblyPath,
                "StingBIM.Revit.Commands.ComplianceCheckCommand")
            {
                ToolTip = "Run compliance checks",
                LongDescription = "Automatically check your model against building codes including " +
                                  "IBC, ADA accessibility, ASHRAE energy, and NFPA fire safety requirements.",
                LargeImage = GetEmbeddedImage("compliance_32"),
                Image = GetEmbeddedImage("compliance_16")
            };
            panel.AddItem(complianceData);

            // Clash Detection
            var clashData = new PushButtonData(
                "ClashDetection",
                "Clash\nDetection",
                assemblyPath,
                "StingBIM.Revit.Commands.ClashDetectionCommand")
            {
                ToolTip = "Run clash detection",
                LongDescription = "Detect geometric conflicts between disciplines with AI-powered " +
                                  "resolution suggestions. Supports MEP vs Structure, MEP vs Architecture, and more.",
                LargeImage = GetEmbeddedImage("clash_32"),
                Image = GetEmbeddedImage("clash_16")
            };
            panel.AddItem(clashData);

            // Model Health
            var healthData = new PushButtonData(
                "ModelHealth",
                "Model\nHealth",
                assemblyPath,
                "StingBIM.Revit.Commands.ModelHealthCommand")
            {
                ToolTip = "Check model health",
                LongDescription = "Analyze model quality including warnings, unused elements, " +
                                  "parameter consistency, and best practices compliance.",
                LargeImage = GetEmbeddedImage("health_32"),
                Image = GetEmbeddedImage("health_16")
            };
            panel.AddItem(healthData);
        }

        private void CreateAutomationPanel(UIControlledApplication app, string tabName)
        {
            var panel = app.CreateRibbonPanel(tabName, "Automation");
            var assemblyPath = Assembly.GetExecutingAssembly().Location;

            // Smart Schedules
            var scheduleData = new PushButtonData(
                "SmartSchedules",
                "Smart\nSchedules",
                assemblyPath,
                "StingBIM.Revit.Commands.SmartScheduleCommand")
            {
                ToolTip = "Generate intelligent schedules",
                LongDescription = "Create schedules with AI-populated parameters, automatic formatting, " +
                                  "and compliance verification. Includes door, window, room, and equipment schedules.",
                LargeImage = GetEmbeddedImage("schedule_32"),
                Image = GetEmbeddedImage("schedule_16")
            };
            panel.AddItem(scheduleData);

            // Quantity Takeoff
            var qtoData = new PushButtonData(
                "QuantityTakeoff",
                "Quantity\nTakeoff",
                assemblyPath,
                "StingBIM.Revit.Commands.QuantityTakeoffCommand")
            {
                ToolTip = "Generate quantity takeoff",
                LongDescription = "Automatically extract quantities with cost estimation using " +
                                  "regional pricing data. Supports materials, labor, and equipment costs.",
                LargeImage = GetEmbeddedImage("quantity_32"),
                Image = GetEmbeddedImage("quantity_16")
            };
            panel.AddItem(qtoData);

            // Auto-Populate Parameters
            var autoPopData = new PushButtonData(
                "AutoPopulate",
                "Auto\nParameters",
                assemblyPath,
                "StingBIM.Revit.Commands.AutoPopulateCommand")
            {
                ToolTip = "Auto-populate parameters",
                LongDescription = "Use AI to automatically fill in missing parameter values based on " +
                                  "element context, project standards, and learned patterns.",
                LargeImage = GetEmbeddedImage("autopop_32"),
                Image = GetEmbeddedImage("autopop_16")
            };
            panel.AddItem(autoPopData);
        }

        private void CreateSettingsPanel(UIControlledApplication app, string tabName)
        {
            var panel = app.CreateRibbonPanel(tabName, "Settings");
            var assemblyPath = Assembly.GetExecutingAssembly().Location;

            // AI Settings
            var settingsData = new PushButtonData(
                "AISettings",
                "AI\nSettings",
                assemblyPath,
                "StingBIM.Revit.Commands.AISettingsCommand")
            {
                ToolTip = "Configure AI settings",
                LongDescription = "Configure StingBIM AI preferences including language model selection, " +
                                  "regional standards, voice settings, and learning preferences.",
                LargeImage = GetEmbeddedImage("settings_32"),
                Image = GetEmbeddedImage("settings_16")
            };
            panel.AddItem(settingsData);

            // Help / About
            var helpData = new PushButtonData(
                "Help",
                "Help",
                assemblyPath,
                "StingBIM.Revit.Commands.HelpCommand")
            {
                ToolTip = "StingBIM AI Help",
                LongDescription = "View documentation, tutorials, and about information for StingBIM AI.",
                LargeImage = GetEmbeddedImage("help_32"),
                Image = GetEmbeddedImage("help_16")
            };
            panel.AddItem(helpData);
        }

        private BitmapImage GetEmbeddedImage(string imageName)
        {
            try
            {
                // Try to load from embedded resources
                var assemblyPath = Assembly.GetExecutingAssembly().Location;
                var assemblyDir = Path.GetDirectoryName(assemblyPath);
                var imagePath = Path.Combine(assemblyDir, "Resources", "Icons", $"{imageName}.png");

                if (File.Exists(imagePath))
                {
                    return new BitmapImage(new Uri(imagePath));
                }

                // Return placeholder if image not found
                Logger.Warn($"Image not found: {imagePath}");
                return null;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"Failed to load image: {imageName}");
                return null;
            }
        }

        #endregion

        #region Dockable Panes

        private void RegisterDockablePanes(UIControlledApplication application)
        {
            Logger.Info("Registering dockable panes...");

            try
            {
                // Register Chat Panel
                var chatPaneProvider = new ChatPanelProvider();
                application.RegisterDockablePane(
                    ChatPanelId,
                    "StingBIM AI Assistant",
                    chatPaneProvider);

                // Register Settings Panel
                var settingsPaneProvider = new SettingsPanelProvider();
                application.RegisterDockablePane(
                    SettingsPanelId,
                    "StingBIM Settings",
                    settingsPaneProvider);

                Logger.Info("Dockable panes registered");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to register dockable panes");
            }
        }

        #endregion

        #region Revit Events

        private void SubscribeToRevitEvents(UIControlledApplication application)
        {
            application.ControlledApplication.DocumentOpened += OnDocumentOpened;
            application.ControlledApplication.DocumentClosing += OnDocumentClosing;
            application.ControlledApplication.DocumentChanged += OnDocumentChanged;
            application.ViewActivated += OnViewActivated;
        }

        private void UnsubscribeFromRevitEvents(UIControlledApplication application)
        {
            application.ControlledApplication.DocumentOpened -= OnDocumentOpened;
            application.ControlledApplication.DocumentClosing -= OnDocumentClosing;
            application.ControlledApplication.DocumentChanged -= OnDocumentChanged;
            application.ViewActivated -= OnViewActivated;
        }

        private void OnDocumentOpened(object sender, DocumentOpenedEventArgs e)
        {
            Logger.Info($"Document opened: {e.Document?.Title ?? "Unknown"}");
            _aiManager?.SetActiveDocument(e.Document);
        }

        private void OnDocumentClosing(object sender, DocumentClosingEventArgs e)
        {
            Logger.Info($"Document closing: {e.Document?.Title ?? "Unknown"}");
        }

        private void OnDocumentChanged(object sender, DocumentChangedEventArgs e)
        {
            // Track changes for AI learning
            var addedCount = e.GetAddedElementIds()?.Count ?? 0;
            var deletedCount = e.GetDeletedElementIds()?.Count ?? 0;
            var modifiedCount = e.GetModifiedElementIds()?.Count ?? 0;

            if (addedCount + deletedCount + modifiedCount > 0)
            {
                Logger.Debug($"Document changed: +{addedCount} -{deletedCount} ~{modifiedCount}");
                _aiManager?.OnDocumentChanged(e);
            }
        }

        private void OnViewActivated(object sender, Autodesk.Revit.UI.Events.ViewActivatedEventArgs e)
        {
            Logger.Debug($"View activated: {e.CurrentActiveView?.Name ?? "Unknown"}");
            _aiManager?.SetActiveView(e.CurrentActiveView);
        }

        #endregion

        #region Data Persistence

        private void SavePendingData()
        {
            try
            {
                // Save learning data
                _aiManager?.SaveLearningData();

                // Save user preferences
                _aiManager?.SaveUserPreferences();

                Logger.Info("Pending data saved");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to save pending data");
            }
        }

        #endregion
    }

    #region Supporting Classes

    /// <summary>
    /// Manages AI subsystem initialization and lifecycle.
    /// </summary>
    internal class AISystemManager
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private bool _isInitialized;

        // AI subsystem references (would be actual implementations)
        private object _nlpEngine;
        private object _agentCoordinator;
        private object _knowledgeGraph;
        private object _memoryManager;

        // Current context
        private object _activeDocument;
        private object _activeView;

        public async System.Threading.Tasks.Task InitializeAsync()
        {
            if (_isInitialized) return;

            Logger.Info("Initializing AI subsystems...");

            // Initialize NLP engine
            _nlpEngine = await InitializeNLPAsync();

            // Initialize agent coordinator
            _agentCoordinator = await InitializeAgentsAsync();

            // Initialize knowledge graph
            _knowledgeGraph = await InitializeKnowledgeAsync();

            // Initialize memory manager
            _memoryManager = await InitializeMemoryAsync();

            _isInitialized = true;
            Logger.Info("All AI subsystems initialized");
        }

        private async System.Threading.Tasks.Task<object> InitializeNLPAsync()
        {
            await System.Threading.Tasks.Task.Delay(100); // Simulate async init
            Logger.Info("NLP engine initialized");
            return new object();
        }

        private async System.Threading.Tasks.Task<object> InitializeAgentsAsync()
        {
            await System.Threading.Tasks.Task.Delay(100);
            Logger.Info("Agent coordinator initialized");
            return new object();
        }

        private async System.Threading.Tasks.Task<object> InitializeKnowledgeAsync()
        {
            await System.Threading.Tasks.Task.Delay(100);
            Logger.Info("Knowledge graph initialized");
            return new object();
        }

        private async System.Threading.Tasks.Task<object> InitializeMemoryAsync()
        {
            await System.Threading.Tasks.Task.Delay(100);
            Logger.Info("Memory manager initialized");
            return new object();
        }

        public void Shutdown()
        {
            Logger.Info("Shutting down AI subsystems...");
            _isInitialized = false;
        }

        public void SetActiveDocument(object document) => _activeDocument = document;
        public void SetActiveView(object view) => _activeView = view;
        public void OnDocumentChanged(object eventArgs) { /* Track for learning */ }
        public void SaveLearningData() { /* Persist learning */ }
        public void SaveUserPreferences() { /* Save preferences */ }
    }

    /// <summary>
    /// Dockable pane provider for the chat panel.
    /// </summary>
    internal class ChatPanelProvider : IDockablePaneProvider
    {
        public void SetupDockablePane(DockablePaneProviderData data)
        {
            data.FrameworkElement = null; // Would be actual WPF control
            data.InitialState = new DockablePaneState
            {
                DockPosition = DockPosition.Right,
                TabBehind = DockablePanes.BuiltInDockablePanes.PropertiesPalette
            };
            data.VisibleByDefault = false;
        }
    }

    /// <summary>
    /// Dockable pane provider for the settings panel.
    /// </summary>
    internal class SettingsPanelProvider : IDockablePaneProvider
    {
        public void SetupDockablePane(DockablePaneProviderData data)
        {
            data.FrameworkElement = null;
            data.InitialState = new DockablePaneState
            {
                DockPosition = DockPosition.Floating
            };
            data.VisibleByDefault = false;
        }
    }

    #endregion
}
