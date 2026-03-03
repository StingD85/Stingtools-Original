// =============================================================================
// StingBIM.AI.Collaboration - Network Discovery Service
// UDP-based peer discovery for LAN worksharing collaboration
// =============================================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NLog;
using StingBIM.AI.Collaboration.Models;

namespace StingBIM.AI.Collaboration.Discovery
{
    /// <summary>
    /// Service for discovering other StingBIM instances on the local network.
    /// Uses UDP broadcast for peer discovery and maintains a registry of connected peers.
    /// </summary>
    public class NetworkDiscoveryService : IDisposable
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        // Network configuration
        private const int DiscoveryPort = 51234;
        private const int BroadcastIntervalMs = 5000;
        private const int PeerTimeoutMs = 15000;
        private const string DiscoveryMagic = "STINGBIM_COLLAB_V7";

        // Network components
        private UdpClient? _udpListener;
        private UdpClient? _udpBroadcaster;
        private CancellationTokenSource? _cts;
        private Task? _listenerTask;
        private Task? _broadcasterTask;
        private Task? _cleanupTask;

        // Peer registry
        private readonly ConcurrentDictionary<string, DiscoveredPeer> _peers = new();

        // Local identity
        private readonly string _localPeerId;
        private string _username = Environment.UserName;
        private string _projectName = string.Empty;
        private string _projectGuid = string.Empty;

        // Events
        public event EventHandler<PeerDiscoveredEventArgs>? PeerDiscovered;
        public event EventHandler<PeerLostEventArgs>? PeerLost;
        public event EventHandler<PeerUpdatedEventArgs>? PeerUpdated;

        public bool IsRunning { get; private set; }
        public IReadOnlyCollection<DiscoveredPeer> Peers => _peers.Values.ToList().AsReadOnly();

        public NetworkDiscoveryService()
        {
            _localPeerId = GeneratePeerId();
            Logger.Info($"NetworkDiscoveryService initialized with PeerId: {_localPeerId}");
        }

        #region Public Methods

        /// <summary>
        /// Start the discovery service
        /// </summary>
        public async Task StartAsync(string username, string projectName, string projectGuid)
        {
            if (IsRunning)
            {
                Logger.Warn("Discovery service is already running");
                return;
            }

            _username = username;
            _projectName = projectName;
            _projectGuid = projectGuid;
            _cts = new CancellationTokenSource();

            try
            {
                // Start UDP listener
                _udpListener = new UdpClient(DiscoveryPort);
                _udpListener.EnableBroadcast = true;

                // Start UDP broadcaster
                _udpBroadcaster = new UdpClient();
                _udpBroadcaster.EnableBroadcast = true;

                IsRunning = true;

                // Start background tasks
                _listenerTask = ListenForPeersAsync(_cts.Token);
                _broadcasterTask = BroadcastPresenceAsync(_cts.Token);
                _cleanupTask = CleanupStalePeersAsync(_cts.Token);

                Logger.Info($"Discovery service started on port {DiscoveryPort}");
                Logger.Info($"Local user: {_username}, Project: {_projectName}");

                // Initial broadcast
                await BroadcastOnce();
            }
            catch (SocketException ex)
            {
                Logger.Error(ex, $"Failed to start discovery service on port {DiscoveryPort}");
                throw;
            }
        }

        /// <summary>
        /// Stop the discovery service
        /// </summary>
        public async Task StopAsync()
        {
            if (!IsRunning) return;

            Logger.Info("Stopping discovery service...");

            _cts?.Cancel();

            // Wait for tasks to complete
            var tasks = new List<Task>();
            if (_listenerTask != null) tasks.Add(_listenerTask);
            if (_broadcasterTask != null) tasks.Add(_broadcasterTask);
            if (_cleanupTask != null) tasks.Add(_cleanupTask);

            try
            {
                await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (OperationCanceledException) { }
            catch (TimeoutException) { }

            _udpListener?.Close();
            _udpBroadcaster?.Close();

            IsRunning = false;
            _peers.Clear();

            Logger.Info("Discovery service stopped");
        }

        /// <summary>
        /// Get peers working on the same project
        /// </summary>
        public IEnumerable<DiscoveredPeer> GetProjectPeers()
        {
            return _peers.Values.Where(p =>
                p.ProjectName == _projectName ||
                (!string.IsNullOrEmpty(_projectGuid) && p.PeerId.Contains(_projectGuid)));
        }

        /// <summary>
        /// Get all active peers on the network
        /// </summary>
        public IEnumerable<DiscoveredPeer> GetAllPeers()
        {
            return _peers.Values.Where(p => p.IsConnected);
        }

        /// <summary>
        /// Update current project information
        /// </summary>
        public async Task UpdateProjectAsync(string projectName, string projectGuid)
        {
            _projectName = projectName;
            _projectGuid = projectGuid;
            await BroadcastOnce();
        }

        #endregion

        #region Private Methods

        private async Task ListenForPeersAsync(CancellationToken ct)
        {
            Logger.Debug("Starting peer listener...");

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var result = await _udpListener!.ReceiveAsync(ct);
                    var message = Encoding.UTF8.GetString(result.Buffer);

                    ProcessDiscoveryMessage(message, result.RemoteEndPoint);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.Interrupted)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error receiving discovery packet");
                }
            }
        }

        private async Task BroadcastPresenceAsync(CancellationToken ct)
        {
            Logger.Debug("Starting presence broadcaster...");

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await BroadcastOnce();
                    await Task.Delay(BroadcastIntervalMs, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error broadcasting presence");
                }
            }
        }

        private async Task BroadcastOnce()
        {
            var packet = new DiscoveryPacket
            {
                PacketType = DiscoveryMagic,
                Version = "7.0",
                PeerId = _localPeerId,
                Username = _username,
                Hostname = Environment.MachineName,
                ListenPort = DiscoveryPort,
                ProjectName = _projectName,
                ProjectGuid = _projectGuid,
                Timestamp = DateTime.UtcNow
            };

            var json = JsonConvert.SerializeObject(packet);
            var data = Encoding.UTF8.GetBytes(json);

            // Broadcast to all network interfaces
            foreach (var broadcastAddress in GetBroadcastAddresses())
            {
                try
                {
                    var endpoint = new IPEndPoint(broadcastAddress, DiscoveryPort);
                    await _udpBroadcaster!.SendAsync(data, data.Length, endpoint);
                }
                catch (Exception ex)
                {
                    Logger.Trace(ex, $"Failed to broadcast to {broadcastAddress}");
                }
            }
        }

        private void ProcessDiscoveryMessage(string message, IPEndPoint remoteEndpoint)
        {
            try
            {
                var packet = JsonConvert.DeserializeObject<DiscoveryPacket>(message);
                if (packet == null || packet.PacketType != DiscoveryMagic)
                    return;

                // Ignore our own broadcasts
                if (packet.PeerId == _localPeerId)
                    return;

                var peer = new DiscoveredPeer
                {
                    PeerId = packet.PeerId,
                    Hostname = packet.Hostname,
                    IPAddress = remoteEndpoint.Address.ToString(),
                    Port = packet.ListenPort,
                    Username = packet.Username,
                    ProjectName = packet.ProjectName,
                    LastHeartbeat = DateTime.UtcNow,
                    IsConnected = true
                };

                if (_peers.TryGetValue(packet.PeerId, out var existingPeer))
                {
                    // Update existing peer
                    peer.DiscoveredAt = existingPeer.DiscoveredAt;
                    _peers[packet.PeerId] = peer;

                    if (existingPeer.ProjectName != peer.ProjectName)
                    {
                        PeerUpdated?.Invoke(this, new PeerUpdatedEventArgs(peer));
                    }
                }
                else
                {
                    // New peer discovered
                    peer.DiscoveredAt = DateTime.UtcNow;
                    _peers[packet.PeerId] = peer;

                    Logger.Info($"Discovered new peer: {peer.Username}@{peer.Hostname} ({peer.IPAddress})");
                    PeerDiscovered?.Invoke(this, new PeerDiscoveredEventArgs(peer));
                }
            }
            catch (JsonException ex)
            {
                Logger.Trace(ex, "Failed to parse discovery packet");
            }
        }

        private async Task CleanupStalePeersAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(PeerTimeoutMs / 2, ct);

                    var now = DateTime.UtcNow;
                    var stalePeers = _peers.Values
                        .Where(p => (now - p.LastHeartbeat).TotalMilliseconds > PeerTimeoutMs)
                        .ToList();

                    foreach (var peer in stalePeers)
                    {
                        if (_peers.TryRemove(peer.PeerId, out var removed))
                        {
                            removed.IsConnected = false;
                            Logger.Info($"Peer lost: {peer.Username}@{peer.Hostname}");
                            PeerLost?.Invoke(this, new PeerLostEventArgs(removed));
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private static IEnumerable<IPAddress> GetBroadcastAddresses()
        {
            var addresses = new List<IPAddress>();

            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up)
                        continue;

                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                        continue;

                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily != AddressFamily.InterNetwork)
                            continue;

                        if (ua.IPv4Mask == null)
                            continue;

                        var broadcastAddress = GetBroadcastAddress(ua.Address, ua.IPv4Mask);
                        addresses.Add(broadcastAddress);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Error getting broadcast addresses");
            }

            // Fallback to general broadcast
            if (addresses.Count == 0)
            {
                addresses.Add(IPAddress.Broadcast);
            }

            return addresses.Distinct();
        }

        private static IPAddress GetBroadcastAddress(IPAddress address, IPAddress mask)
        {
            var ipBytes = address.GetAddressBytes();
            var maskBytes = mask.GetAddressBytes();
            var broadcastBytes = new byte[ipBytes.Length];

            for (int i = 0; i < broadcastBytes.Length; i++)
            {
                broadcastBytes[i] = (byte)(ipBytes[i] | ~maskBytes[i]);
            }

            return new IPAddress(broadcastBytes);
        }

        private static string GeneratePeerId()
        {
            var machineId = Environment.MachineName;
            var userId = Environment.UserName;
            var timestamp = DateTime.UtcNow.Ticks;
            return $"{machineId}_{userId}_{timestamp:X}".GetHashCode().ToString("X8");
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            StopAsync().Wait(TimeSpan.FromSeconds(5));
            _cts?.Dispose();
            _udpListener?.Dispose();
            _udpBroadcaster?.Dispose();
        }

        #endregion
    }

    #region Event Args

    public class PeerDiscoveredEventArgs : EventArgs
    {
        public DiscoveredPeer Peer { get; }
        public PeerDiscoveredEventArgs(DiscoveredPeer peer) => Peer = peer;
    }

    public class PeerLostEventArgs : EventArgs
    {
        public DiscoveredPeer Peer { get; }
        public PeerLostEventArgs(DiscoveredPeer peer) => Peer = peer;
    }

    public class PeerUpdatedEventArgs : EventArgs
    {
        public DiscoveredPeer Peer { get; }
        public PeerUpdatedEventArgs(DiscoveredPeer peer) => Peer = peer;
    }

    #endregion
}
