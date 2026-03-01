using System;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using NLog;
using StingBIM.AI.Automation.Maintenance;
using StingBIM.AI.Collaboration.Protocol;
using StingBIM.AI.Core.Models;
using StingBIM.AI.Creation.Pipeline;
using StingBIM.AI.NLP.Dialogue;
using StingBIM.AI.NLP.Pipeline;
using StingBIM.AI.Revit.Services;
using StingBIM.AI.UI.Panels;

namespace StingBIM.AI.Revit.Commands
{
    /// <summary>
    /// External command that opens the StingBIM AI Chat panel.
    /// Wires up NLP, Creation Pipeline, Collaboration, and Facilities Management modules.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ShowChatCommand : IExternalCommand
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                Logger.Info("Opening StingBIM AI Chat...");

                // Get active Revit document for model queries and element creation
                var uiDoc = commandData.Application.ActiveUIDocument;
                var doc = uiDoc?.Document;

                // NLP pipeline
                var contextTracker = new ContextTracker();
                var responseGenerator = new ResponseGenerator();
                var tokenizer = new Tokenizer();
                var embeddingModel = new EmbeddingModel();
                var intentClassifier = new IntentClassifier(embeddingModel, tokenizer);
                var entityExtractor = new EntityExtractor();

                // Wire up model query service so the AI can answer model questions
                if (doc != null)
                {
                    responseGenerator.ModelQueryService = new RevitModelQueryService(doc);
                    Logger.Info($"Model query service connected to document: {doc.Title}");
                }

                var conversationManager = new ConversationManager(
                    contextTracker, responseGenerator, intentClassifier, entityExtractor);

                // Creation pipeline — routes NLP intents to actual Revit element creation
                CommandRouter commandRouter = null;
                if (doc != null && !doc.IsReadOnly)
                {
                    commandRouter = new CommandRouter(doc);
                    Logger.Info("Creation pipeline (CommandRouter) initialized");
                }

                // Collaboration module
                var collaborationProtocol = new AgentCollaborationProtocol();

                // Facilities Management module
                var maintenanceScheduler = new PredictiveMaintenanceScheduler();

                // Create chat panel
                var chatPanel = new ChatPanel(conversationManager);

                // Wire up command execution for all modules
                conversationManager.CommandReady += (sender, e) =>
                {
                    HandleCommand(e.Command, e.Entities, e.OriginalInput,
                        commandRouter, collaborationProtocol, maintenanceScheduler, chatPanel);
                };

                chatPanel.Show();

                var modules = new List<string> { "NLP", "FM", "Collaboration" };
                if (commandRouter != null) modules.Add("Creation Pipeline");
                Logger.Info($"StingBIM AI Chat opened with modules: {string.Join(", ", modules)}");

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to open StingBIM AI Chat");
                message = ex.Message;
                return Result.Failed;
            }
        }

        /// <summary>
        /// Routes commands to the appropriate module: Creation, FM, or Collaboration.
        /// </summary>
        private void HandleCommand(
            DesignCommand command,
            Dictionary<string, object> entities,
            string originalInput,
            CommandRouter commandRouter,
            AgentCollaborationProtocol collaborationProtocol,
            PredictiveMaintenanceScheduler maintenanceScheduler,
            ChatPanel chatPanel)
        {
            var commandType = command?.CommandType?.ToUpperInvariant() ?? "";

            // ── Creation commands → CommandRouter → CreationOrchestrator → Revit ──
            if (CommandRouter.IsCreationIntent(commandType) && commandRouter != null)
            {
                Logger.Info($"Creation command dispatched: {commandType}");

                var result = commandRouter.Route(commandType, entities, originalInput);

                if (result.Handled)
                {
                    if (result.Success)
                    {
                        var message = result.Message;
                        if (result.CostEstimate != null && result.CostEstimate.TotalUGX > 0)
                        {
                            message += $"\nEstimated cost: {result.CostEstimate.FormattedTotal}";
                        }
                        chatPanel.ShowCommandFeedback(true, message);
                    }
                    else
                    {
                        chatPanel.ShowCommandFeedback(false, result.Error ?? result.Message);
                    }

                    // Show follow-up suggestion chips
                    if (result.Suggestions != null && result.Suggestions.Count > 0)
                    {
                        chatPanel.ShowSuggestionChips(result.Suggestions);
                    }
                }
                return;
            }

            // ── FM commands ──
            switch (commandType)
            {
                case "GENERATE_MAINTENANCE_SCHEDULE":
                case "PREDICT_FAILURES":
                case "OPTIMIZE_MAINTENANCE":
                case "ANALYZE_FAILURES":
                case "FM_GENERAL":
                    Logger.Info($"FM command dispatched: {commandType}");
                    chatPanel.ShowCommandFeedback(true, $"FM module ready for: {commandType}");
                    break;

                // ── Collaboration commands ──
                case "NEGOTIATE_DESIGN":
                case "GET_RECOMMENDATIONS":
                case "RESOLVE_CONFLICT":
                case "COLLABORATE":
                    Logger.Info($"Collaboration command dispatched: {commandType}");
                    chatPanel.ShowCommandFeedback(true, $"Collaboration module ready for: {commandType}");
                    break;
            }
        }
    }
}
