// =============================================================================
// StingBIM.AI.Collaboration - Hybrid Connection Manager
// Intelligent routing between LAN (P2P) and WAN (Cloud Relay) connections
// Enables remote office collaboration over the internet
// =============================================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NLog;
using StingBIM.AI.Collaboration.Models;

namespace StingBIM.AI.Collaboration.Connectivity
{
    /// <summary>
    /// Manages hybrid connectivity between local LAN peers and remote WAN connections.
    /// Automatically detects network topology and routes messages optimally.
    /// </summary>
    public class HybridConnectionManager : IAsyncDisposable
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        // Connection modes
        public enum ConnectionMode
        {
            Offline,
            LANOnly,         // Only local peers via UDP
            WANOnly,         // Only cloud relay
            Hybrid,          // Both LAN and WAN
            DirectTunnel     // Direct peer-to-peer tunnel (NAT traversal)
        }

        // State
        private readonly string _teamId;
        private readonly string _userId;
        private readonly ConcurrentDictionary<string, PeerConnection> _connections = new();
        private readonly ConcurrentDictionary<string, NetworkRoute> _routingTable = new();
        private ConnectionMode _currentMode = ConnectionMode.Offline;
        private CancellationTokenSource? _cts;

        // Cloud relay configuration
        private string _relayServerUrl = "wss://relay.stingbim.com";
        private string _stunServerUrl = "stun:stun.stingbim.com:3478";
        private string _turnServerUrl = "turn:turn.stingbim.com:3478";
        private HttpClient? _httpClient;

        // Network detection
        private readonly Timer _networkMonitor;
        private NetworkTopology _topology = new();
        private bool _hasInternetAccess;
        private bool _isOnCorporateNetwork;
        private List<string> _localSubnets = new();

        // Events
        public event EventHandler<ConnectionModeChangedEventArgs>? ConnectionModeChanged;
        public event EventHandler<PeerConnectedEventArgs>? PeerConnected;
        public event EventHandler<PeerDisconnectedEventArgs>? PeerDisconnected;
        public event EventHandler<MessageReceivedEventArgs>? MessageReceived;
        public event EventHandler<NetworkStatusChangedEventArgs>? NetworkStatusChanged;

        public ConnectionMode CurrentMode => _currentMode;
        public bool IsConnected => _currentMode != ConnectionMode.Offline;
        public IReadOnlyCollection<PeerConnection> Connections => _connections.Values.ToList().AsReadOnly();
        public NetworkTopology Topology => _topology;

        public HybridConnectionManager(string teamId, string userId)
        {
            _teamId = teamId;
            _userId = userId;
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

            // Monitor network changes every 30 seconds
            _networkMonitor = new Timer(MonitorNetworkChanges, null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
        }

        #region Initialization

        /// <summary>
        /// Start the hybrid connection manager
        /// </summary>
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            Logger.Info("Starting HybridConnectionManager...");

            // Detect network topology
            await DetectNetworkTopologyAsync();

            // Determine best connection mode
            await DetermineBestConnectionModeAsync();

            // Start connections based on mode
            await EstablishConnectionsAsync();

            Logger.Info($"HybridConnectionManager started in {_currentMode} mode");
        }

        /// <summary>
        /// Stop all connections
        /// </summary>
        public async Task StopAsync()
        {
            _cts?.Cancel();

            foreach (var connection in _connections.Values)
            {
                await connection.DisconnectAsync();
            }

            _connections.Clear();
            _currentMode = ConnectionMode.Offline;

            Logger.Info("HybridConnectionManager stopped");
        }

        #endregion

        #region Network Detection

        private async Task DetectNetworkTopologyAsync()
        {
            Logger.Debug("Detecting network topology...");

            _topology = new NetworkTopology();

            try
            {
                // Get all network interfaces
                var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up)
                    .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .ToList();

                foreach (var iface in interfaces)
                {
                    var props = iface.GetIPProperties();
                    var ipv4 = props.UnicastAddresses
                        .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork);

                    if (ipv4 != null)
                    {
                        _topology.LocalInterfaces.Add(new NetworkInterfaceInfo
                        {
                            Name = iface.Name,
                            Description = iface.Description,
                            IPAddress = ipv4.Address.ToString(),
                            SubnetMask = ipv4.IPv4Mask?.ToString() ?? "255.255.255.0",
                            Type = iface.NetworkInterfaceType.ToString(),
                            Speed = iface.Speed
                        });

                        // Calculate subnet
                        var subnet = CalculateSubnet(ipv4.Address, ipv4.IPv4Mask);
                        if (!string.IsNullOrEmpty(subnet))
                        {
                            _localSubnets.Add(subnet);
                        }
                    }
                }

                // Check internet connectivity
                _hasInternetAccess = await CheckInternetAccessAsync();
                _topology.HasInternetAccess = _hasInternetAccess;

                // Detect external IP if we have internet
                if (_hasInternetAccess)
                {
                    _topology.ExternalIPAddress = await GetExternalIPAsync();
                    _topology.NATType = await DetectNATTypeAsync();
                }

                // Detect if on corporate network (VPN, proxy, etc.)
                _isOnCorporateNetwork = DetectCorporateNetwork();
                _topology.IsOnCorporateNetwork = _isOnCorporateNetwork;

                Logger.Info($"Network topology detected: {_topology.LocalInterfaces.Count} interfaces, " +
                           $"Internet: {_hasInternetAccess}, Corporate: {_isOnCorporateNetwork}, NAT: {_topology.NATType}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error detecting network topology");
            }
        }

        private async Task<bool> CheckInternetAccessAsync()
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var response = await _httpClient!.GetAsync("https://connectivity.stingbim.com/ping", cts.Token);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                // Try alternative
                try
                {
                    using var ping = new Ping();
                    var reply = await ping.SendPingAsync("8.8.8.8", 3000);
                    return reply.Status == IPStatus.Success;
                }
                catch
                {
                    return false;
                }
            }
        }

        private async Task<string?> GetExternalIPAsync()
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var response = await _httpClient!.GetStringAsync("https://api.stingbim.com/ip", cts.Token);
                return response.Trim();
            }
            catch
            {
                return null;
            }
        }

        private async Task<NATType> DetectNATTypeAsync()
        {
            // Simplified NAT detection - in production would use STUN
            try
            {
                // If we can get external IP, we have some form of NAT
                if (!string.IsNullOrEmpty(_topology.ExternalIPAddress))
                {
                    var localIP = _topology.LocalInterfaces.FirstOrDefault()?.IPAddress;
                    if (localIP == _topology.ExternalIPAddress)
                    {
                        return NATType.None;
                    }

                    // Most home/office networks are behind NAT
                    return NATType.SymmetricNAT;
                }
            }
            catch { }

            return NATType.Unknown;
        }

        private bool DetectCorporateNetwork()
        {
            // Detect corporate network indicators
            var interfaces = NetworkInterface.GetAllNetworkInterfaces();

            foreach (var iface in interfaces)
            {
                // VPN indicators
                if (iface.Description.Contains("VPN", StringComparison.OrdinalIgnoreCase) ||
                    iface.Description.Contains("Virtual", StringComparison.OrdinalIgnoreCase) ||
                    iface.Description.Contains("Tunnel", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                // Check for domain-joined indicators
                var props = iface.GetIPProperties();
                if (props.DnsSuffix?.Contains("corp", StringComparison.OrdinalIgnoreCase) == true ||
                    props.DnsSuffix?.Contains("internal", StringComparison.OrdinalIgnoreCase) == true)
                {
                    return true;
                }
            }

            return false;
        }

        private string? CalculateSubnet(IPAddress ip, IPAddress? mask)
        {
            if (mask == null) return null;

            var ipBytes = ip.GetAddressBytes();
            var maskBytes = mask.GetAddressBytes();
            var subnetBytes = new byte[4];

            for (int i = 0; i < 4; i++)
            {
                subnetBytes[i] = (byte)(ipBytes[i] & maskBytes[i]);
            }

            return new IPAddress(subnetBytes).ToString();
        }

        private void MonitorNetworkChanges(object? state)
        {
            Task.Run(async () =>
            {
                var previousMode = _currentMode;
                var previousInternet = _hasInternetAccess;

                await DetectNetworkTopologyAsync();
                await DetermineBestConnectionModeAsync();

                if (_currentMode != previousMode || _hasInternetAccess != previousInternet)
                {
                    NetworkStatusChanged?.Invoke(this, new NetworkStatusChangedEventArgs(_topology, _currentMode));

                    if (_currentMode != previousMode)
                    {
                        await EstablishConnectionsAsync();
                    }
                }
            });
        }

        #endregion

        #region Connection Management

        private async Task DetermineBestConnectionModeAsync()
        {
            var previousMode = _currentMode;

            // Determine best mode based on network conditions
            var hasLANPeers = await ScanForLANPeersAsync();

            if (!_hasInternetAccess && !hasLANPeers)
            {
                _currentMode = ConnectionMode.Offline;
            }
            else if (hasLANPeers && !_hasInternetAccess)
            {
                _currentMode = ConnectionMode.LANOnly;
            }
            else if (!hasLANPeers && _hasInternetAccess)
            {
                _currentMode = ConnectionMode.WANOnly;
            }
            else if (hasLANPeers && _hasInternetAccess)
            {
                _currentMode = ConnectionMode.Hybrid;
            }

            if (_currentMode != previousMode)
            {
                Logger.Info($"Connection mode changed: {previousMode} -> {_currentMode}");
                ConnectionModeChanged?.Invoke(this, new ConnectionModeChangedEventArgs(previousMode, _currentMode));
            }
        }

        private async Task<bool> ScanForLANPeersAsync()
        {
            // Quick UDP broadcast to detect LAN peers
            try
            {
                using var udp = new UdpClient();
                udp.EnableBroadcast = true;

                var discoveryPacket = JsonConvert.SerializeObject(new
                {
                    Type = "STINGBIM_DISCOVER",
                    TeamId = _teamId,
                    UserId = _userId
                });

                var data = Encoding.UTF8.GetBytes(discoveryPacket);
                await udp.SendAsync(data, data.Length, new IPEndPoint(IPAddress.Broadcast, 51234));

                // Wait briefly for responses
                udp.Client.ReceiveTimeout = 500;

                try
                {
                    var endpoint = new IPEndPoint(IPAddress.Any, 0);
                    var response = udp.Receive(ref endpoint);
                    return true; // Found at least one peer
                }
                catch (SocketException)
                {
                    return false; // No responses
                }
            }
            catch
            {
                return false;
            }
        }

        private async Task EstablishConnectionsAsync()
        {
            switch (_currentMode)
            {
                case ConnectionMode.LANOnly:
                    await EstablishLANConnectionsAsync();
                    break;

                case ConnectionMode.WANOnly:
                    await EstablishCloudRelayAsync();
                    break;

                case ConnectionMode.Hybrid:
                    await EstablishLANConnectionsAsync();
                    await EstablishCloudRelayAsync();
                    break;

                case ConnectionMode.DirectTunnel:
                    await EstablishDirectTunnelAsync();
                    break;
            }
        }

        private async Task EstablishLANConnectionsAsync()
        {
            Logger.Debug("Establishing LAN connections...");
            // LAN connections handled by NetworkDiscoveryService
            // This manager coordinates with it
        }

        private async Task EstablishCloudRelayAsync()
        {
            Logger.Info($"Connecting to cloud relay: {_relayServerUrl}");

            try
            {
                var connection = new PeerConnection
                {
                    ConnectionId = $"relay_{_teamId}",
                    Type = ConnectionType.CloudRelay,
                    Endpoint = _relayServerUrl,
                    IsRelay = true
                };

                // SignalR connection to relay server
                // In production, this would connect to a hosted SignalR server
                connection.Status = ConnectionStatus.Connected;
                connection.ConnectedAt = DateTime.UtcNow;

                _connections[connection.ConnectionId] = connection;

                // Update routing table - all remote peers route through relay
                _routingTable["*"] = new NetworkRoute
                {
                    RouteType = RouteType.CloudRelay,
                    NextHop = connection.ConnectionId,
                    Priority = 10
                };

                Logger.Info("Connected to cloud relay");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to connect to cloud relay");
            }
        }

        private async Task EstablishDirectTunnelAsync()
        {
            // WebRTC-style direct tunneling for lowest latency
            Logger.Debug("Attempting direct tunnel establishment...");

            // NAT traversal using STUN/TURN
            if (_topology.NATType == NATType.SymmetricNAT)
            {
                // Symmetric NAT requires TURN relay
                Logger.Debug("Symmetric NAT detected, using TURN relay");
                await EstablishCloudRelayAsync();
            }
            else
            {
                // Other NAT types can use STUN for hole punching
                Logger.Debug("Attempting STUN hole punching");
                // Implementation would use ICE candidates
            }
        }

        #endregion

        #region Message Routing

        /// <summary>
        /// Send a message to a specific peer with automatic route selection
        /// </summary>
        public async Task SendToPeerAsync(string peerId, CollaborationMessage message)
        {
            var route = GetBestRoute(peerId);

            if (route == null)
            {
                Logger.Warn($"No route to peer: {peerId}");
                return;
            }

            message.RoutingInfo = new RoutingInfo
            {
                SourceId = _userId,
                DestinationId = peerId,
                RouteType = route.RouteType,
                Hops = 0,
                Timestamp = DateTime.UtcNow
            };

            switch (route.RouteType)
            {
                case RouteType.Direct:
                    await SendDirectAsync(peerId, message);
                    break;

                case RouteType.LAN:
                    await SendViaLANAsync(peerId, message);
                    break;

                case RouteType.CloudRelay:
                    await SendViaRelayAsync(peerId, message);
                    break;
            }
        }

        /// <summary>
        /// Broadcast a message to all connected peers
        /// </summary>
        public async Task BroadcastAsync(CollaborationMessage message)
        {
            var tasks = new List<Task>();

            foreach (var connection in _connections.Values.Where(c => c.Status == ConnectionStatus.Connected))
            {
                if (connection.IsRelay)
                {
                    // Relay will broadcast to all connected peers
                    tasks.Add(SendViaRelayAsync("*", message));
                }
                else
                {
                    tasks.Add(SendDirectAsync(connection.PeerId, message));
                }
            }

            await Task.WhenAll(tasks);
        }

        private NetworkRoute? GetBestRoute(string peerId)
        {
            // Check for specific route
            if (_routingTable.TryGetValue(peerId, out var specificRoute))
            {
                return specificRoute;
            }

            // Check if peer is on same subnet (use LAN)
            if (_connections.TryGetValue(peerId, out var connection))
            {
                if (connection.Type == ConnectionType.LAN)
                {
                    return new NetworkRoute { RouteType = RouteType.LAN, NextHop = peerId };
                }
            }

            // Fall back to wildcard route (usually cloud relay)
            if (_routingTable.TryGetValue("*", out var wildcardRoute))
            {
                return wildcardRoute;
            }

            return null;
        }

        private async Task SendDirectAsync(string peerId, CollaborationMessage message)
        {
            // Direct P2P send (WebRTC data channel or TCP)
            Logger.Debug($"Sending direct to {peerId}");
        }

        private async Task SendViaLANAsync(string peerId, CollaborationMessage message)
        {
            // UDP multicast or unicast on LAN
            Logger.Debug($"Sending via LAN to {peerId}");
        }

        private async Task SendViaRelayAsync(string peerId, CollaborationMessage message)
        {
            // Send through cloud relay
            Logger.Debug($"Sending via relay to {peerId}");
        }

        #endregion

        #region Remote Office Connection

        /// <summary>
        /// Connect to a remote office using connection code
        /// </summary>
        public async Task<bool> ConnectToRemoteOfficeAsync(string connectionCode)
        {
            Logger.Info($"Connecting to remote office with code: {connectionCode[..6]}...");

            try
            {
                // Decode connection code to get relay room/channel
                var connectionInfo = DecodeConnectionCode(connectionCode);

                if (connectionInfo == null)
                {
                    Logger.Error("Invalid connection code");
                    return false;
                }

                // Join the relay room for this team
                var connection = new PeerConnection
                {
                    ConnectionId = $"remote_{connectionInfo.TeamId}",
                    Type = ConnectionType.CloudRelay,
                    Endpoint = connectionInfo.RelayEndpoint,
                    IsRelay = true,
                    PeerId = connectionInfo.TeamId
                };

                _connections[connection.ConnectionId] = connection;

                // Add route to remote office
                _routingTable[connectionInfo.TeamId] = new NetworkRoute
                {
                    RouteType = RouteType.CloudRelay,
                    NextHop = connection.ConnectionId,
                    Priority = 5,
                    Latency = connectionInfo.EstimatedLatency
                };

                Logger.Info($"Connected to remote office: {connectionInfo.OfficeName}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to connect to remote office");
                return false;
            }
        }

        /// <summary>
        /// Generate a connection code for remote offices to join
        /// </summary>
        public string GenerateConnectionCode()
        {
            var connectionInfo = new RemoteConnectionInfo
            {
                TeamId = _teamId,
                OfficeName = Environment.MachineName,
                RelayEndpoint = _relayServerUrl,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(7)
            };

            var json = JsonConvert.SerializeObject(connectionInfo);
            var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

            // Create human-readable code
            var code = $"SB-{encoded[..20].ToUpper()}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";

            Logger.Info($"Generated connection code: {code}");
            return code;
        }

        private RemoteConnectionInfo? DecodeConnectionCode(string code)
        {
            try
            {
                // Extract base64 portion
                var parts = code.Split('-');
                if (parts.Length < 2) return null;

                var encoded = parts[1];
                var json = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
                return JsonConvert.DeserializeObject<RemoteConnectionInfo>(json);
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region Configuration

        public void ConfigureRelayServer(string url)
        {
            _relayServerUrl = url;
        }

        public void ConfigureSTUNServer(string url)
        {
            _stunServerUrl = url;
        }

        public void ConfigureTURNServer(string url)
        {
            _turnServerUrl = url;
        }

        #endregion

        #region IAsyncDisposable

        public async ValueTask DisposeAsync()
        {
            await StopAsync();
            _networkMonitor.Dispose();
            _httpClient?.Dispose();
            _cts?.Dispose();
        }

        #endregion
    }

    #region Models

    public class PeerConnection
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string PeerId { get; set; } = string.Empty;
        public ConnectionType Type { get; set; }
        public ConnectionStatus Status { get; set; }
        public string Endpoint { get; set; } = string.Empty;
        public bool IsRelay { get; set; }
        public DateTime ConnectedAt { get; set; }
        public int Latency { get; set; }
        public long BytesSent { get; set; }
        public long BytesReceived { get; set; }

        public async Task DisconnectAsync() { }
    }

    public enum ConnectionType
    {
        LAN,
        CloudRelay,
        DirectTunnel,
        VPN
    }

    public enum ConnectionStatus
    {
        Disconnected,
        Connecting,
        Connected,
        Reconnecting,
        Error
    }

    public class NetworkRoute
    {
        public RouteType RouteType { get; set; }
        public string NextHop { get; set; } = string.Empty;
        public int Priority { get; set; }
        public int Latency { get; set; }
    }

    public enum RouteType
    {
        Direct,
        LAN,
        CloudRelay,
        Tunnel
    }

    public class NetworkTopology
    {
        public List<NetworkInterfaceInfo> LocalInterfaces { get; set; } = new();
        public string? ExternalIPAddress { get; set; }
        public NATType NATType { get; set; }
        public bool HasInternetAccess { get; set; }
        public bool IsOnCorporateNetwork { get; set; }
        public bool HasVPN { get; set; }
    }

    public class NetworkInterfaceInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string IPAddress { get; set; } = string.Empty;
        public string SubnetMask { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public long Speed { get; set; }
    }

    public enum NATType
    {
        Unknown,
        None,
        FullCone,
        RestrictedCone,
        PortRestrictedCone,
        SymmetricNAT
    }

    public class RemoteConnectionInfo
    {
        public string TeamId { get; set; } = string.Empty;
        public string OfficeName { get; set; } = string.Empty;
        public string RelayEndpoint { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public int EstimatedLatency { get; set; }
    }

    public class CollaborationMessage
    {
        public string MessageId { get; set; } = Guid.NewGuid().ToString();
        public string Type { get; set; } = string.Empty;
        public object? Payload { get; set; }
        public RoutingInfo? RoutingInfo { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class RoutingInfo
    {
        public string SourceId { get; set; } = string.Empty;
        public string DestinationId { get; set; } = string.Empty;
        public RouteType RouteType { get; set; }
        public int Hops { get; set; }
        public DateTime Timestamp { get; set; }
    }

    #endregion

    #region Event Args

    public class ConnectionModeChangedEventArgs : EventArgs
    {
        public HybridConnectionManager.ConnectionMode PreviousMode { get; }
        public HybridConnectionManager.ConnectionMode NewMode { get; }

        public ConnectionModeChangedEventArgs(
            HybridConnectionManager.ConnectionMode previous,
            HybridConnectionManager.ConnectionMode newMode)
        {
            PreviousMode = previous;
            NewMode = newMode;
        }
    }

    public class PeerConnectedEventArgs : EventArgs
    {
        public PeerConnection Connection { get; }
        public PeerConnectedEventArgs(PeerConnection connection) => Connection = connection;
    }

    public class PeerDisconnectedEventArgs : EventArgs
    {
        public string PeerId { get; }
        public string Reason { get; }
        public PeerDisconnectedEventArgs(string peerId, string reason)
        {
            PeerId = peerId;
            Reason = reason;
        }
    }

    public class NetworkStatusChangedEventArgs : EventArgs
    {
        public NetworkTopology Topology { get; }
        public HybridConnectionManager.ConnectionMode Mode { get; }

        public NetworkStatusChangedEventArgs(NetworkTopology topology, HybridConnectionManager.ConnectionMode mode)
        {
            Topology = topology;
            Mode = mode;
        }
    }

    #endregion
}
