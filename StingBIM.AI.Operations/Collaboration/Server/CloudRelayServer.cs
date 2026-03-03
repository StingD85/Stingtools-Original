// =============================================================================
// StingBIM.AI.Collaboration - Cloud Relay Server
// Self-hosted or cloud-deployed relay server for remote office collaboration
// Supports Azure SignalR Service, AWS, or self-hosted deployment
// =============================================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NLog;

namespace StingBIM.AI.Collaboration.Server
{
    /// <summary>
    /// Cloud relay server that enables collaboration between geographically
    /// distributed offices. Can be deployed on Azure, AWS, or on-premises.
    /// </summary>
    public class CloudRelayServer : IAsyncDisposable
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        // Server configuration
        private readonly CloudRelayConfig _config;
        private HttpListener? _httpListener;
        private CancellationTokenSource? _cts;
        private bool _isRunning;

        // Connection management
        private readonly ConcurrentDictionary<string, RelayConnection> _connections = new();
        private readonly ConcurrentDictionary<string, RelayRoom> _rooms = new();
        private readonly ConcurrentDictionary<string, TeamRegistration> _teams = new();

        // Authentication
        private readonly ConcurrentDictionary<string, ApiKey> _apiKeys = new();
        private readonly TokenValidator _tokenValidator;

        // Metrics
        private readonly ServerMetrics _metrics = new();
        private Timer? _metricsTimer;

        // Events
        public event EventHandler<ClientConnectedEventArgs>? ClientConnected;
        public event EventHandler<ClientDisconnectedEventArgs>? ClientDisconnected;
        public event EventHandler<MessageRelayedEventArgs>? MessageRelayed;

        public bool IsRunning => _isRunning;
        public int ConnectionCount => _connections.Count;
        public int RoomCount => _rooms.Count;
        public ServerMetrics Metrics => _metrics;

        public CloudRelayServer(CloudRelayConfig config)
        {
            _config = config;
            _tokenValidator = new TokenValidator(config.JwtSecret);
        }

        #region Server Lifecycle

        /// <summary>
        /// Start the relay server
        /// </summary>
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (_isRunning)
            {
                Logger.Warn("Server is already running");
                return;
            }

            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            try
            {
                // Initialize HTTP listener
                _httpListener = new HttpListener();
                _httpListener.Prefixes.Add($"http://{_config.Host}:{_config.Port}/");
                _httpListener.Prefixes.Add($"http://{_config.Host}:{_config.Port}/ws/");
                _httpListener.Prefixes.Add($"http://{_config.Host}:{_config.Port}/api/");

                _httpListener.Start();
                _isRunning = true;

                // Start metrics collection
                _metricsTimer = new Timer(CollectMetrics, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));

                Logger.Info($"Cloud Relay Server started on {_config.Host}:{_config.Port}");

                // Start accepting connections
                await AcceptConnectionsAsync(_cts.Token);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to start server");
                throw;
            }
        }

        /// <summary>
        /// Stop the relay server
        /// </summary>
        public async Task StopAsync()
        {
            if (!_isRunning) return;

            Logger.Info("Stopping Cloud Relay Server...");

            _cts?.Cancel();

            // Disconnect all clients gracefully
            var disconnectTasks = _connections.Values
                .Select(c => DisconnectClientAsync(c.ConnectionId, "Server shutting down"));
            await Task.WhenAll(disconnectTasks);

            _httpListener?.Stop();
            _metricsTimer?.Dispose();

            _isRunning = false;
            Logger.Info("Cloud Relay Server stopped");
        }

        private async Task AcceptConnectionsAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _isRunning)
            {
                try
                {
                    var context = await _httpListener!.GetContextAsync();

                    // Route request
                    _ = Task.Run(() => HandleRequestAsync(context, ct), ct);
                }
                catch (HttpListenerException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error accepting connection");
                }
            }
        }

        private async Task HandleRequestAsync(HttpListenerContext context, CancellationToken ct)
        {
            var path = context.Request.Url?.AbsolutePath ?? "/";

            try
            {
                if (context.Request.IsWebSocketRequest)
                {
                    await HandleWebSocketAsync(context, ct);
                }
                else if (path.StartsWith("/api/"))
                {
                    await HandleApiRequestAsync(context);
                }
                else if (path == "/health")
                {
                    await HandleHealthCheckAsync(context);
                }
                else
                {
                    context.Response.StatusCode = 404;
                    context.Response.Close();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error handling request: {path}");
                context.Response.StatusCode = 500;
                context.Response.Close();
            }
        }

        #endregion

        #region WebSocket Handling

        private async Task HandleWebSocketAsync(HttpListenerContext context, CancellationToken ct)
        {
            WebSocketContext? wsContext = null;

            try
            {
                // Authenticate
                var token = context.Request.QueryString["token"];
                var teamId = context.Request.QueryString["team"];
                var userId = context.Request.QueryString["user"];

                if (!ValidateConnection(token, teamId, userId, out var validationError))
                {
                    context.Response.StatusCode = 401;
                    await WriteJsonResponseAsync(context.Response, new { error = validationError });
                    return;
                }

                // Accept WebSocket
                wsContext = await context.AcceptWebSocketAsync(null);
                var ws = wsContext.WebSocket;

                // Create connection
                var connection = new RelayConnection
                {
                    ConnectionId = Guid.NewGuid().ToString(),
                    WebSocket = ws,
                    TeamId = teamId!,
                    UserId = userId!,
                    ConnectedAt = DateTime.UtcNow,
                    RemoteEndpoint = context.Request.RemoteEndPoint?.ToString() ?? "unknown"
                };

                _connections[connection.ConnectionId] = connection;
                _metrics.TotalConnections++;
                _metrics.ActiveConnections = _connections.Count;

                // Join team room
                await JoinRoomAsync(connection, $"team_{teamId}");

                // Notify
                ClientConnected?.Invoke(this, new ClientConnectedEventArgs(connection));
                Logger.Info($"Client connected: {userId} from {connection.RemoteEndpoint}");

                // Send welcome message
                await SendToClientAsync(connection, new RelayMessage
                {
                    Type = "welcome",
                    Payload = new
                    {
                        connectionId = connection.ConnectionId,
                        serverTime = DateTime.UtcNow,
                        teamMembers = GetTeamMembers(teamId!)
                    }
                });

                // Start message loop
                await ProcessMessagesAsync(connection, ct);
            }
            catch (WebSocketException ex)
            {
                Logger.Debug(ex, "WebSocket error");
            }
            finally
            {
                if (wsContext != null)
                {
                    var connId = _connections.Values
                        .FirstOrDefault(c => c.WebSocket == wsContext.WebSocket)?.ConnectionId;
                    if (connId != null)
                    {
                        await DisconnectClientAsync(connId, "Connection closed");
                    }
                }
            }
        }

        private async Task ProcessMessagesAsync(RelayConnection connection, CancellationToken ct)
        {
            var buffer = new byte[_config.MaxMessageSize];

            while (connection.WebSocket.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                try
                {
                    var result = await connection.WebSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer), ct);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        var message = JsonConvert.DeserializeObject<RelayMessage>(json);

                        if (message != null)
                        {
                            await HandleMessageAsync(connection, message);
                        }
                    }

                    connection.LastActivity = DateTime.UtcNow;
                    connection.MessagesReceived++;
                    _metrics.TotalMessagesRelayed++;
                }
                catch (WebSocketException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error processing message");
                }
            }
        }

        private async Task HandleMessageAsync(RelayConnection sender, RelayMessage message)
        {
            message.SenderId = sender.UserId;
            message.Timestamp = DateTime.UtcNow;

            switch (message.Type?.ToLower())
            {
                case "broadcast":
                    // Broadcast to all team members
                    await BroadcastToRoomAsync($"team_{sender.TeamId}", message, sender.ConnectionId);
                    break;

                case "direct":
                    // Send to specific user
                    if (!string.IsNullOrEmpty(message.TargetId))
                    {
                        await SendToUserAsync(message.TargetId, message);
                    }
                    break;

                case "room_join":
                    // Join a specific room
                    var roomId = message.Payload?.ToString();
                    if (!string.IsNullOrEmpty(roomId))
                    {
                        await JoinRoomAsync(sender, roomId);
                    }
                    break;

                case "room_leave":
                    // Leave a room
                    var leaveRoomId = message.Payload?.ToString();
                    if (!string.IsNullOrEmpty(leaveRoomId))
                    {
                        await LeaveRoomAsync(sender, leaveRoomId);
                    }
                    break;

                case "room_message":
                    // Send to specific room
                    if (!string.IsNullOrEmpty(message.RoomId))
                    {
                        await BroadcastToRoomAsync(message.RoomId, message, sender.ConnectionId);
                    }
                    break;

                case "presence":
                    // Update presence and broadcast
                    sender.Status = message.Payload?.ToString() ?? "online";
                    await BroadcastToRoomAsync($"team_{sender.TeamId}", new RelayMessage
                    {
                        Type = "presence_update",
                        SenderId = sender.UserId,
                        Payload = new { userId = sender.UserId, status = sender.Status }
                    }, null);
                    break;

                case "typing":
                    // Broadcast typing indicator
                    await BroadcastToRoomAsync($"team_{sender.TeamId}", message, sender.ConnectionId);
                    break;

                case "ping":
                    // Respond with pong
                    await SendToClientAsync(sender, new RelayMessage { Type = "pong" });
                    break;

                default:
                    // Relay unknown messages to team
                    await BroadcastToRoomAsync($"team_{sender.TeamId}", message, sender.ConnectionId);
                    break;
            }

            MessageRelayed?.Invoke(this, new MessageRelayedEventArgs(sender, message));
        }

        #endregion

        #region Room Management

        private async Task JoinRoomAsync(RelayConnection connection, string roomId)
        {
            var room = _rooms.GetOrAdd(roomId, _ => new RelayRoom { RoomId = roomId });

            if (!room.Members.Contains(connection.ConnectionId))
            {
                room.Members.Add(connection.ConnectionId);
                connection.Rooms.Add(roomId);

                // Notify room members
                await BroadcastToRoomAsync(roomId, new RelayMessage
                {
                    Type = "user_joined",
                    SenderId = connection.UserId,
                    Payload = new { userId = connection.UserId, roomId }
                }, connection.ConnectionId);

                Logger.Debug($"{connection.UserId} joined room {roomId}");
            }
        }

        private async Task LeaveRoomAsync(RelayConnection connection, string roomId)
        {
            if (_rooms.TryGetValue(roomId, out var room))
            {
                room.Members.Remove(connection.ConnectionId);
                connection.Rooms.Remove(roomId);

                // Notify room members
                await BroadcastToRoomAsync(roomId, new RelayMessage
                {
                    Type = "user_left",
                    SenderId = connection.UserId,
                    Payload = new { userId = connection.UserId, roomId }
                }, null);

                // Clean up empty rooms (except team rooms)
                if (room.Members.Count == 0 && !roomId.StartsWith("team_"))
                {
                    _rooms.TryRemove(roomId, out _);
                }

                Logger.Debug($"{connection.UserId} left room {roomId}");
            }
        }

        private async Task BroadcastToRoomAsync(string roomId, RelayMessage message, string? excludeConnectionId)
        {
            if (!_rooms.TryGetValue(roomId, out var room)) return;

            var tasks = new List<Task>();

            foreach (var memberId in room.Members.ToList())
            {
                if (memberId == excludeConnectionId) continue;

                if (_connections.TryGetValue(memberId, out var member))
                {
                    tasks.Add(SendToClientAsync(member, message));
                }
            }

            await Task.WhenAll(tasks);
        }

        #endregion

        #region Client Communication

        private async Task SendToClientAsync(RelayConnection connection, RelayMessage message)
        {
            if (connection.WebSocket.State != WebSocketState.Open) return;

            try
            {
                var json = JsonConvert.SerializeObject(message);
                var buffer = Encoding.UTF8.GetBytes(json);

                await connection.WebSocket.SendAsync(
                    new ArraySegment<byte>(buffer),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None);

                connection.MessagesSent++;
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, $"Failed to send to {connection.UserId}");
            }
        }

        private async Task SendToUserAsync(string userId, RelayMessage message)
        {
            var connections = _connections.Values
                .Where(c => c.UserId == userId)
                .ToList();

            foreach (var connection in connections)
            {
                await SendToClientAsync(connection, message);
            }
        }

        private async Task DisconnectClientAsync(string connectionId, string reason)
        {
            if (!_connections.TryRemove(connectionId, out var connection)) return;

            // Leave all rooms
            foreach (var roomId in connection.Rooms.ToList())
            {
                await LeaveRoomAsync(connection, roomId);
            }

            // Close WebSocket
            try
            {
                if (connection.WebSocket.State == WebSocketState.Open)
                {
                    await connection.WebSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        reason,
                        CancellationToken.None);
                }
            }
            catch { }

            _metrics.ActiveConnections = _connections.Count;
            ClientDisconnected?.Invoke(this, new ClientDisconnectedEventArgs(connection, reason));
            Logger.Info($"Client disconnected: {connection.UserId} - {reason}");
        }

        #endregion

        #region API Endpoints

        private async Task HandleApiRequestAsync(HttpListenerContext context)
        {
            var path = context.Request.Url?.AbsolutePath ?? "";
            var method = context.Request.HttpMethod;

            // Authenticate API request
            var apiKey = context.Request.Headers["X-API-Key"];
            if (!ValidateApiKey(apiKey))
            {
                context.Response.StatusCode = 401;
                await WriteJsonResponseAsync(context.Response, new { error = "Invalid API key" });
                return;
            }

            switch (path)
            {
                case "/api/teams":
                    if (method == "POST")
                        await HandleRegisterTeamAsync(context);
                    else if (method == "GET")
                        await HandleListTeamsAsync(context);
                    break;

                case "/api/connections":
                    await HandleListConnectionsAsync(context);
                    break;

                case "/api/rooms":
                    await HandleListRoomsAsync(context);
                    break;

                case "/api/metrics":
                    await HandleMetricsAsync(context);
                    break;

                case "/api/broadcast":
                    if (method == "POST")
                        await HandleApiBroadcastAsync(context);
                    break;

                default:
                    context.Response.StatusCode = 404;
                    break;
            }

            context.Response.Close();
        }

        private async Task HandleRegisterTeamAsync(HttpListenerContext context)
        {
            var body = await ReadRequestBodyAsync(context.Request);
            var registration = JsonConvert.DeserializeObject<TeamRegistration>(body);

            if (registration == null || string.IsNullOrEmpty(registration.TeamId))
            {
                context.Response.StatusCode = 400;
                await WriteJsonResponseAsync(context.Response, new { error = "Invalid team data" });
                return;
            }

            registration.RegisteredAt = DateTime.UtcNow;
            registration.ConnectionCode = GenerateConnectionCode(registration.TeamId);
            _teams[registration.TeamId] = registration;

            await WriteJsonResponseAsync(context.Response, new
            {
                teamId = registration.TeamId,
                connectionCode = registration.ConnectionCode,
                wsEndpoint = $"ws://{_config.Host}:{_config.Port}/ws?team={registration.TeamId}"
            });
        }

        private async Task HandleListTeamsAsync(HttpListenerContext context)
        {
            var teams = _teams.Values.Select(t => new
            {
                t.TeamId,
                t.TeamName,
                t.RegisteredAt,
                activeMembers = _connections.Values.Count(c => c.TeamId == t.TeamId)
            });

            await WriteJsonResponseAsync(context.Response, teams);
        }

        private async Task HandleListConnectionsAsync(HttpListenerContext context)
        {
            var connections = _connections.Values.Select(c => new
            {
                c.ConnectionId,
                c.UserId,
                c.TeamId,
                c.Status,
                c.ConnectedAt,
                c.LastActivity,
                c.MessagesReceived,
                c.MessagesSent
            });

            await WriteJsonResponseAsync(context.Response, connections);
        }

        private async Task HandleListRoomsAsync(HttpListenerContext context)
        {
            var rooms = _rooms.Values.Select(r => new
            {
                r.RoomId,
                memberCount = r.Members.Count,
                r.CreatedAt
            });

            await WriteJsonResponseAsync(context.Response, rooms);
        }

        private async Task HandleMetricsAsync(HttpListenerContext context)
        {
            await WriteJsonResponseAsync(context.Response, _metrics);
        }

        private async Task HandleApiBroadcastAsync(HttpListenerContext context)
        {
            var body = await ReadRequestBodyAsync(context.Request);
            var request = JsonConvert.DeserializeObject<ApiBroadcastRequest>(body);

            if (request == null)
            {
                context.Response.StatusCode = 400;
                return;
            }

            var message = new RelayMessage
            {
                Type = "api_broadcast",
                Payload = request.Message
            };

            if (!string.IsNullOrEmpty(request.TeamId))
            {
                await BroadcastToRoomAsync($"team_{request.TeamId}", message, null);
            }
            else if (!string.IsNullOrEmpty(request.RoomId))
            {
                await BroadcastToRoomAsync(request.RoomId, message, null);
            }

            await WriteJsonResponseAsync(context.Response, new { success = true });
        }

        private async Task HandleHealthCheckAsync(HttpListenerContext context)
        {
            var health = new
            {
                status = "healthy",
                uptime = DateTime.UtcNow - _metrics.StartTime,
                connections = _connections.Count,
                rooms = _rooms.Count,
                messagesRelayed = _metrics.TotalMessagesRelayed
            };

            await WriteJsonResponseAsync(context.Response, health);
        }

        #endregion

        #region Authentication

        private bool ValidateConnection(string? token, string? teamId, string? userId, out string error)
        {
            error = "";

            if (string.IsNullOrEmpty(teamId))
            {
                error = "Team ID required";
                return false;
            }

            if (string.IsNullOrEmpty(userId))
            {
                error = "User ID required";
                return false;
            }

            // If authentication is enabled, validate token
            if (_config.RequireAuthentication)
            {
                if (string.IsNullOrEmpty(token))
                {
                    error = "Authentication token required";
                    return false;
                }

                if (!_tokenValidator.ValidateToken(token, teamId, userId))
                {
                    error = "Invalid or expired token";
                    return false;
                }
            }

            return true;
        }

        private bool ValidateApiKey(string? apiKey)
        {
            if (!_config.RequireApiKey) return true;
            if (string.IsNullOrEmpty(apiKey)) return false;

            return _apiKeys.ContainsKey(apiKey) ||
                   apiKey == _config.MasterApiKey;
        }

        /// <summary>
        /// Generate a new API key for a team
        /// </summary>
        public string GenerateApiKey(string teamId, string description)
        {
            var key = $"sb_{Convert.ToBase64String(RandomNumberGenerator.GetBytes(24))}";
            _apiKeys[key] = new ApiKey
            {
                Key = key,
                TeamId = teamId,
                Description = description,
                CreatedAt = DateTime.UtcNow
            };
            return key;
        }

        private string GenerateConnectionCode(string teamId)
        {
            var data = $"{teamId}:{DateTime.UtcNow.Ticks}";
            var hash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(data)));
            return $"SB-{hash[..8].ToUpper()}-{hash[8..16].ToUpper()}";
        }

        #endregion

        #region Helpers

        private List<object> GetTeamMembers(string teamId)
        {
            return _connections.Values
                .Where(c => c.TeamId == teamId)
                .Select(c => new { c.UserId, c.Status, c.ConnectedAt } as object)
                .ToList();
        }

        private async Task<string> ReadRequestBodyAsync(HttpListenerRequest request)
        {
            using var reader = new System.IO.StreamReader(request.InputStream, request.ContentEncoding);
            return await reader.ReadToEndAsync();
        }

        private async Task WriteJsonResponseAsync(HttpListenerResponse response, object data)
        {
            response.ContentType = "application/json";
            var json = JsonConvert.SerializeObject(data);
            var buffer = Encoding.UTF8.GetBytes(json);
            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer);
        }

        private void CollectMetrics(object? state)
        {
            _metrics.ActiveConnections = _connections.Count;
            _metrics.ActiveRooms = _rooms.Count;
            _metrics.LastMetricsUpdate = DateTime.UtcNow;

            // Clean up stale connections
            var staleTime = DateTime.UtcNow.AddMinutes(-5);
            foreach (var conn in _connections.Values.ToList())
            {
                if (conn.LastActivity < staleTime && conn.WebSocket.State != WebSocketState.Open)
                {
                    _ = DisconnectClientAsync(conn.ConnectionId, "Stale connection");
                }
            }
        }

        #endregion

        #region IAsyncDisposable

        public async ValueTask DisposeAsync()
        {
            await StopAsync();
            _httpListener?.Close();
            _metricsTimer?.Dispose();
            _cts?.Dispose();
        }

        #endregion
    }

    #region Supporting Classes

    public class CloudRelayConfig
    {
        public string Host { get; set; } = "0.0.0.0";
        public int Port { get; set; } = 5150;
        public bool RequireAuthentication { get; set; } = false;
        public bool RequireApiKey { get; set; } = false;
        public string? JwtSecret { get; set; }
        public string? MasterApiKey { get; set; }
        public int MaxMessageSize { get; set; } = 64 * 1024; // 64KB
        public int MaxConnectionsPerTeam { get; set; } = 100;
    }

    public class RelayConnection
    {
        public string ConnectionId { get; set; } = string.Empty;
        public WebSocket WebSocket { get; set; } = null!;
        public string TeamId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Status { get; set; } = "online";
        public string RemoteEndpoint { get; set; } = string.Empty;
        public DateTime ConnectedAt { get; set; }
        public DateTime LastActivity { get; set; }
        public List<string> Rooms { get; set; } = new();
        public long MessagesReceived { get; set; }
        public long MessagesSent { get; set; }
    }

    public class RelayRoom
    {
        public string RoomId { get; set; } = string.Empty;
        public List<string> Members { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class RelayMessage
    {
        public string? Type { get; set; }
        public string? SenderId { get; set; }
        public string? TargetId { get; set; }
        public string? RoomId { get; set; }
        public object? Payload { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class TeamRegistration
    {
        public string TeamId { get; set; } = string.Empty;
        public string TeamName { get; set; } = string.Empty;
        public string? ContactEmail { get; set; }
        public string? ConnectionCode { get; set; }
        public DateTime RegisteredAt { get; set; }
    }

    public class ApiKey
    {
        public string Key { get; set; } = string.Empty;
        public string TeamId { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class ApiBroadcastRequest
    {
        public string? TeamId { get; set; }
        public string? RoomId { get; set; }
        public object? Message { get; set; }
    }

    public class ServerMetrics
    {
        public DateTime StartTime { get; set; } = DateTime.UtcNow;
        public long TotalConnections { get; set; }
        public int ActiveConnections { get; set; }
        public int ActiveRooms { get; set; }
        public long TotalMessagesRelayed { get; set; }
        public DateTime LastMetricsUpdate { get; set; }
    }

    public class TokenValidator
    {
        private readonly string? _secret;

        public TokenValidator(string? secret) => _secret = secret;

        public bool ValidateToken(string token, string teamId, string userId)
        {
            // Simplified validation - in production use proper JWT
            if (string.IsNullOrEmpty(_secret)) return true;

            try
            {
                // Would decode and validate JWT here
                return !string.IsNullOrEmpty(token);
            }
            catch
            {
                return false;
            }
        }
    }

    #endregion

    #region Event Args

    public class ClientConnectedEventArgs : EventArgs
    {
        public RelayConnection Connection { get; }
        public ClientConnectedEventArgs(RelayConnection connection) => Connection = connection;
    }

    public class ClientDisconnectedEventArgs : EventArgs
    {
        public RelayConnection Connection { get; }
        public string Reason { get; }
        public ClientDisconnectedEventArgs(RelayConnection connection, string reason)
        {
            Connection = connection;
            Reason = reason;
        }
    }

    public class MessageRelayedEventArgs : EventArgs
    {
        public RelayConnection Sender { get; }
        public RelayMessage Message { get; }
        public MessageRelayedEventArgs(RelayConnection sender, RelayMessage message)
        {
            Sender = sender;
            Message = message;
        }
    }

    #endregion
}
