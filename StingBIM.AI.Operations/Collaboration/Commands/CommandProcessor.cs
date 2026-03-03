// =============================================================================
// StingBIM.AI.Collaboration - Command Processor
// Handles /commands in chat for worksharing operations
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NLog;
using StingBIM.AI.Collaboration.Models;
using StingBIM.AI.Collaboration.Communication;
using StingBIM.AI.Collaboration.Worksharing;

namespace StingBIM.AI.Collaboration.Commands
{
    /// <summary>
    /// Processes chat commands for worksharing operations.
    /// Commands start with / (e.g., /who, /status, /sync)
    /// </summary>
    public class CommandProcessor
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly CollaborationHub _hub;
        private readonly WorksharingMonitor _monitor;
        private readonly Dictionary<string, Func<ParsedCommand, Task<CommandResult>>> _commands;

        // Events
        public event EventHandler<CommandExecutedEventArgs>? CommandExecuted;
        public event EventHandler<AIQueryEventArgs>? AIQueryRequested;

        // Command help definitions
        private static readonly Dictionary<string, string> CommandHelp = new()
        {
            { "help", "Show available commands" },
            { "who", "Show who's working on what. Usage: /who [area|element|workset]" },
            { "status", "Show your sync status and recommendations" },
            { "sync", "Show sync recommendations. Usage: /sync [preview|now]" },
            { "conflicts", "Show predicted conflicts" },
            { "activity", "Show recent team activity. Usage: /activity [15m|1h|today]" },
            { "request", "Request access to a workset. Usage: /request <workset-name>" },
            { "handoff", "Transfer workset to user. Usage: /handoff <workset> to @user" },
            { "lock", "Temporarily lock elements. Usage: /lock <element-id> [duration]" },
            { "notify", "Send notification to team. Usage: /notify <message>" },
            { "dm", "Direct message. Usage: /dm @user <message>" },
            { "worksets", "List all worksets and their owners" },
            { "users", "List connected team members" },
            { "ai", "Ask StingBIM AI. Usage: /ai <question>" }
        };

        public CommandProcessor(CollaborationHub hub, WorksharingMonitor monitor)
        {
            _hub = hub;
            _monitor = monitor;
            _commands = InitializeCommands();
        }

        #region Command Execution

        /// <summary>
        /// Process a command string
        /// </summary>
        public async Task<CommandResult> ProcessAsync(string input, string senderId)
        {
            if (string.IsNullOrWhiteSpace(input))
                return new CommandResult { Success = false, Message = "Empty command" };

            if (!input.StartsWith("/"))
                return new CommandResult { Success = false, Message = "Commands must start with /" };

            var parsed = ParseCommand(input, senderId);

            if (!_commands.TryGetValue(parsed.CommandName.ToLower(), out var handler))
            {
                return new CommandResult
                {
                    Success = false,
                    Message = $"Unknown command: /{parsed.CommandName}. Type /help for available commands."
                };
            }

            try
            {
                Logger.Info($"Executing command: /{parsed.CommandName} from {senderId}");
                var result = await handler(parsed);

                // Fire command executed event
                CommandExecuted?.Invoke(this, new CommandExecutedEventArgs(parsed, result));

                return result;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error executing command: /{parsed.CommandName}");
                return new CommandResult
                {
                    Success = false,
                    Message = $"Error executing command: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Check if input is a command
        /// </summary>
        public bool IsCommand(string input)
        {
            return !string.IsNullOrWhiteSpace(input) && input.TrimStart().StartsWith("/");
        }

        /// <summary>
        /// Parse command into parts
        /// </summary>
        public ParsedCommand ParseCommand(string input, string senderId)
        {
            var parts = input.TrimStart('/').Split(' ', StringSplitOptions.RemoveEmptyEntries);

            var command = new ParsedCommand
            {
                RawInput = input,
                SenderId = senderId,
                CommandName = parts.Length > 0 ? parts[0] : string.Empty,
                Arguments = parts.Length > 1 ? parts[1..].ToList() : new List<string>()
            };

            // Parse options (--key=value or -k value)
            for (int i = 0; i < command.Arguments.Count; i++)
            {
                var arg = command.Arguments[i];
                if (arg.StartsWith("--") && arg.Contains('='))
                {
                    var split = arg[2..].Split('=', 2);
                    command.Options[split[0]] = split[1];
                }
                else if (arg.StartsWith("-") && i + 1 < command.Arguments.Count)
                {
                    command.Options[arg[1..]] = command.Arguments[i + 1];
                    i++;
                }
            }

            return command;
        }

        #endregion

        #region Command Handlers

        private Dictionary<string, Func<ParsedCommand, Task<CommandResult>>> InitializeCommands()
        {
            return new Dictionary<string, Func<ParsedCommand, Task<CommandResult>>>
            {
                { "help", HandleHelpAsync },
                { "who", HandleWhoAsync },
                { "status", HandleStatusAsync },
                { "sync", HandleSyncAsync },
                { "conflicts", HandleConflictsAsync },
                { "activity", HandleActivityAsync },
                { "request", HandleRequestAsync },
                { "handoff", HandleHandoffAsync },
                { "lock", HandleLockAsync },
                { "notify", HandleNotifyAsync },
                { "dm", HandleDirectMessageAsync },
                { "worksets", HandleWorksetsAsync },
                { "users", HandleUsersAsync },
                { "ai", HandleAIAsync }
            };
        }

        private Task<CommandResult> HandleHelpAsync(ParsedCommand cmd)
        {
            var helpText = "📋 **StingBIM Worksharing Commands**\n\n";

            foreach (var (command, description) in CommandHelp)
            {
                helpText += $"**/{command}** - {description}\n";
            }

            helpText += "\n💡 Tip: Use @username to mention team members";

            return Task.FromResult(new CommandResult
            {
                Success = true,
                Message = helpText,
                ResponseMessages = new List<ChatMessage>
                {
                    new ChatMessage
                    {
                        Content = helpText,
                        Type = MessageType.SystemNotification,
                        IsSystemMessage = true
                    }
                }
            });
        }

        private async Task<CommandResult> HandleWhoAsync(ParsedCommand cmd)
        {
            var target = cmd.Arguments.FirstOrDefault()?.ToLower();

            string response;

            if (string.IsNullOrEmpty(target))
            {
                // Show all active users
                var users = _hub.TeamMembers.Where(u => u.Status != PresenceStatus.Offline).ToList();
                response = $"👥 **Active Team Members ({users.Count})**\n\n";

                foreach (var user in users)
                {
                    var statusIcon = GetStatusIcon(user.Status);
                    var elementCount = user.ActiveElements.Count;
                    response += $"{statusIcon} **{user.DisplayName}** ({user.Discipline})\n";
                    response += $"   📍 {user.CurrentWorkset ?? "No workset"} • {elementCount} elements\n";
                }
            }
            else
            {
                // Search for specific area/element
                var activities = _monitor.GetActiveElements()
                    .Where(a => a.ElementName.Contains(target, StringComparison.OrdinalIgnoreCase) ||
                                a.LevelName?.Contains(target, StringComparison.OrdinalIgnoreCase) == true ||
                                a.Category.Contains(target, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (activities.Any())
                {
                    response = $"🔍 **Activity matching '{target}'**\n\n";
                    foreach (var activity in activities.Take(10))
                    {
                        response += $"• **{activity.ElementName}** - {activity.Username} ({activity.ActivityType})\n";
                        response += $"   {activity.Category} • {activity.Timestamp:HH:mm}\n";
                    }
                }
                else
                {
                    response = $"No activity found matching '{target}'";
                }
            }

            return new CommandResult { Success = true, Message = response };
        }

        private async Task<CommandResult> HandleStatusAsync(ParsedCommand cmd)
        {
            var status = _monitor.GetSyncStatus(cmd.SenderId);

            var response = "📊 **Your Sync Status**\n\n";
            response += $"**State:** {GetStateIcon(status.State)} {status.State}\n";
            response += $"**Last Sync:** {(status.LastSyncTime == default ? "Never" : status.LastSyncTime.ToString("HH:mm"))}\n";
            response += $"**Local Changes:** {status.LocalChangesCount}\n";
            response += $"**Central Changes:** {status.CentralChangesCount}\n";
            response += $"**Potential Conflicts:** {status.PotentialConflicts}\n\n";
            response += $"💡 **Recommendation:** {status.SyncRecommendation ?? "No recommendation"}";

            return new CommandResult { Success = true, Message = response };
        }

        private async Task<CommandResult> HandleSyncAsync(ParsedCommand cmd)
        {
            var option = cmd.Arguments.FirstOrDefault()?.ToLower();

            if (option == "preview")
            {
                var conflicts = await _monitor.PredictConflictsAsync(cmd.SenderId);

                if (!conflicts.Any())
                {
                    return new CommandResult
                    {
                        Success = true,
                        Message = "✅ **Sync Preview**: No conflicts detected. Safe to sync!"
                    };
                }

                var response = $"⚠️ **Sync Preview: {conflicts.Count} potential conflict(s)**\n\n";
                foreach (var conflict in conflicts)
                {
                    var icon = GetSeverityIcon(conflict.Severity);
                    response += $"{icon} **{conflict.ElementName}**\n";
                    response += $"   {conflict.Description}\n";
                    response += $"   💡 {conflict.AIResolutionSuggestion}\n\n";
                }

                return new CommandResult { Success = true, Message = response };
            }

            return new CommandResult
            {
                Success = true,
                Message = "Use `/sync preview` to see potential conflicts before syncing.\n" +
                          "Sync from Revit: Collaborate > Synchronize with Central"
            };
        }

        private async Task<CommandResult> HandleConflictsAsync(ParsedCommand cmd)
        {
            var conflicts = await _monitor.PredictConflictsAsync(cmd.SenderId);

            if (!conflicts.Any())
            {
                return new CommandResult
                {
                    Success = true,
                    Message = "✅ No conflicts predicted. You're in good shape!"
                };
            }

            var response = $"🔍 **Predicted Conflicts ({conflicts.Count})**\n\n";

            foreach (var conflict in conflicts.OrderByDescending(c => c.Severity))
            {
                var icon = GetSeverityIcon(conflict.Severity);
                response += $"{icon} **{conflict.Severity}** - {conflict.ElementName}\n";
                response += $"   Other user: {conflict.RemoteUserName}\n";
                response += $"   Probability: {conflict.Probability:P0}\n";
                response += $"   💡 {conflict.AIResolutionSuggestion}\n\n";
            }

            return new CommandResult { Success = true, Message = response };
        }

        private async Task<CommandResult> HandleActivityAsync(ParsedCommand cmd)
        {
            var period = cmd.Arguments.FirstOrDefault()?.ToLower() ?? "15m";
            var timeSpan = period switch
            {
                "1h" => TimeSpan.FromHours(1),
                "today" => TimeSpan.FromHours(DateTime.Now.Hour),
                _ => TimeSpan.FromMinutes(15)
            };

            var summary = _monitor.GetActivitySummary(timeSpan);

            var response = $"📈 **Team Activity ({period})**\n\n";
            response += $"**Active Users:** {summary.ActiveUsers}\n";
            response += $"**Total Edits:** {summary.TotalEdits}\n\n";

            if (summary.EditsByUser.Any())
            {
                response += "**By User:**\n";
                foreach (var (user, count) in summary.EditsByUser.OrderByDescending(kv => kv.Value).Take(5))
                {
                    response += $"• {user}: {count} edits\n";
                }
            }

            if (summary.EditsByLevel.Any())
            {
                response += "\n**By Level:**\n";
                foreach (var (level, count) in summary.EditsByLevel.OrderByDescending(kv => kv.Value).Take(5))
                {
                    response += $"• {level}: {count} edits\n";
                }
            }

            return new CommandResult { Success = true, Message = response };
        }

        private async Task<CommandResult> HandleRequestAsync(ParsedCommand cmd)
        {
            var worksetName = string.Join(" ", cmd.Arguments);

            if (string.IsNullOrWhiteSpace(worksetName))
            {
                return new CommandResult
                {
                    Success = false,
                    Message = "Usage: /request <workset-name>"
                };
            }

            await _hub.RequestWorksetAsync(worksetName);

            return new CommandResult
            {
                Success = true,
                Message = $"📨 Request sent for workset: **{worksetName}**\nThe current owner will be notified."
            };
        }

        private async Task<CommandResult> HandleHandoffAsync(ParsedCommand cmd)
        {
            // Parse: /handoff <workset> to @user
            var input = string.Join(" ", cmd.Arguments);
            var match = Regex.Match(input, @"(.+?)\s+to\s+@(\w+)", RegexOptions.IgnoreCase);

            if (!match.Success)
            {
                return new CommandResult
                {
                    Success = false,
                    Message = "Usage: /handoff <workset-name> to @username"
                };
            }

            var worksetName = match.Groups[1].Value.Trim();
            var targetUsername = match.Groups[2].Value;

            var targetUser = _hub.TeamMembers.FirstOrDefault(u =>
                u.Username.Equals(targetUsername, StringComparison.OrdinalIgnoreCase) ||
                u.DisplayName.Equals(targetUsername, StringComparison.OrdinalIgnoreCase));

            if (targetUser == null)
            {
                return new CommandResult
                {
                    Success = false,
                    Message = $"User @{targetUsername} not found. Use /users to see connected members."
                };
            }

            await _hub.TransferWorksetAsync(worksetName, targetUser.UserId);

            return new CommandResult
            {
                Success = true,
                Message = $"✅ Workset **{worksetName}** transferred to **{targetUser.DisplayName}**"
            };
        }

        private Task<CommandResult> HandleLockAsync(ParsedCommand cmd)
        {
            // Placeholder for element locking
            return Task.FromResult(new CommandResult
            {
                Success = true,
                Message = "🔒 Element locking is managed through Revit worksets.\n" +
                          "Use /request or /handoff to manage workset access."
            });
        }

        private async Task<CommandResult> HandleNotifyAsync(ParsedCommand cmd)
        {
            var message = string.Join(" ", cmd.Arguments);

            if (string.IsNullOrWhiteSpace(message))
            {
                return new CommandResult
                {
                    Success = false,
                    Message = "Usage: /notify <message>"
                };
            }

            await _hub.SendMessageAsync($"📢 **Announcement from {_hub.LocalUser.DisplayName}:**\n{message}");

            return new CommandResult
            {
                Success = true,
                Message = "Notification sent to team."
            };
        }

        private async Task<CommandResult> HandleDirectMessageAsync(ParsedCommand cmd)
        {
            if (cmd.Arguments.Count < 2 || !cmd.Arguments[0].StartsWith("@"))
            {
                return new CommandResult
                {
                    Success = false,
                    Message = "Usage: /dm @username <message>"
                };
            }

            var targetUsername = cmd.Arguments[0][1..]; // Remove @
            var message = string.Join(" ", cmd.Arguments.Skip(1));

            var targetUser = _hub.TeamMembers.FirstOrDefault(u =>
                u.Username.Equals(targetUsername, StringComparison.OrdinalIgnoreCase));

            if (targetUser == null)
            {
                return new CommandResult
                {
                    Success = false,
                    Message = $"User @{targetUsername} not found."
                };
            }

            await _hub.SendDirectMessageAsync(targetUser.UserId, message);

            return new CommandResult
            {
                Success = true,
                Message = $"📩 Message sent to {targetUser.DisplayName}"
            };
        }

        private Task<CommandResult> HandleWorksetsAsync(ParsedCommand cmd)
        {
            var worksets = _monitor.GetWorksets().ToList();

            if (!worksets.Any())
            {
                return Task.FromResult(new CommandResult
                {
                    Success = true,
                    Message = "No workset information available. Open a workshared project to see worksets."
                });
            }

            var response = "📁 **Worksets**\n\n";
            foreach (var workset in worksets.OrderBy(w => w.Name))
            {
                var editIcon = workset.IsEditable ? "✏️" : "🔒";
                response += $"{editIcon} **{workset.Name}**\n";
                response += $"   Owner: {workset.Owner ?? "None"} • {workset.ElementCount} elements\n";
            }

            return Task.FromResult(new CommandResult { Success = true, Message = response });
        }

        private Task<CommandResult> HandleUsersAsync(ParsedCommand cmd)
        {
            var users = _hub.TeamMembers.ToList();

            if (!users.Any())
            {
                return Task.FromResult(new CommandResult
                {
                    Success = true,
                    Message = "No team members connected. You might be working offline."
                });
            }

            var response = $"👥 **Connected Users ({users.Count})**\n\n";
            foreach (var user in users.OrderBy(u => u.DisplayName))
            {
                var statusIcon = GetStatusIcon(user.Status);
                response += $"{statusIcon} **{user.DisplayName}** (@{user.Username})\n";
                response += $"   {user.Discipline} • {user.WorkstationName}\n";
            }

            return Task.FromResult(new CommandResult { Success = true, Message = response });
        }

        private Task<CommandResult> HandleAIAsync(ParsedCommand cmd)
        {
            var question = string.Join(" ", cmd.Arguments);

            if (string.IsNullOrWhiteSpace(question))
            {
                return Task.FromResult(new CommandResult
                {
                    Success = false,
                    Message = "Usage: /ai <your question>\n\nExample: /ai what's the status of Level 2?"
                });
            }

            // Fire AI query event for external AI integration
            AIQueryRequested?.Invoke(this, new AIQueryEventArgs(question, cmd.SenderId));

            // This would integrate with StingBIM's AI engine
            // For now, return helpful information based on keywords

            var response = "🤖 **StingBIM AI**\n\n";

            if (question.Contains("status", StringComparison.OrdinalIgnoreCase))
            {
                var status = _monitor.GetSyncStatus(cmd.SenderId);
                response += $"Your sync status: {status.State}\n";
                response += $"Local changes: {status.LocalChangesCount}\n";
                response += $"Recommendation: {status.SyncRecommendation}";
            }
            else if (question.Contains("conflict", StringComparison.OrdinalIgnoreCase))
            {
                response += "Use `/conflicts` to see predicted conflicts.\n";
                response += "Use `/sync preview` before syncing to check for issues.";
            }
            else
            {
                response += "I can help you with worksharing questions. Try asking about:\n";
                response += "• sync status\n";
                response += "• conflicts\n";
                response += "• who's working on [area]\n";
                response += "• workset information";
            }

            return Task.FromResult(new CommandResult { Success = true, Message = response });
        }

        #endregion

        #region Helpers

        private static string GetStatusIcon(PresenceStatus status) => status switch
        {
            PresenceStatus.Online => "🟢",
            PresenceStatus.Away => "🟡",
            PresenceStatus.Busy => "🔴",
            PresenceStatus.DoNotDisturb => "⛔",
            PresenceStatus.InSync => "🔄",
            _ => "⚫"
        };

        private static string GetStateIcon(SyncState state) => state switch
        {
            SyncState.UpToDate => "✅",
            SyncState.LocalChanges => "📤",
            SyncState.CentralChanges => "📥",
            SyncState.BothChanged => "🔄",
            SyncState.Syncing => "⏳",
            SyncState.Error => "❌",
            _ => "❔"
        };

        private static string GetSeverityIcon(ConflictSeverity severity) => severity switch
        {
            ConflictSeverity.Critical => "🔴",
            ConflictSeverity.High => "🟠",
            ConflictSeverity.Medium => "🟡",
            _ => "🟢"
        };

        #endregion
    }

    #region Event Args

    public class CommandExecutedEventArgs : EventArgs
    {
        public ParsedCommand Command { get; }
        public CommandResult Result { get; }
        public CommandExecutedEventArgs(ParsedCommand command, CommandResult result)
        {
            Command = command;
            Result = result;
        }
    }

    public class AIQueryEventArgs : EventArgs
    {
        public string Query { get; }
        public string SenderId { get; }
        public AIQueryEventArgs(string query, string senderId)
        {
            Query = query;
            SenderId = senderId;
        }
    }

    #endregion
}
