using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MessagePack;
using static CardGame.TCP.MessagePackHelper;

#nullable enable
namespace CardGame.TCP {
    internal class UDP_Broadcast_Helper {
        private static readonly int Port = 38799;
        private static readonly IPEndPoint MulticastAddress = new(IPAddress.Parse("239.255.0.1"), Port);
        private static readonly IPEndPoint BroadcastAddress = new(IPAddress.Broadcast, Port);
        private static readonly IPAddress iPAddress;
        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("CGv1"); //Card Game version 1
        private static readonly bool CanUseMulticast = CheckMulticastAvailable();
        private static bool multicastEnabled = CanUseMulticast;
        private static string _secret = string.Empty;
        private static byte[] _hmac = [];
        private static readonly UdpClient _udp;
        private static CancellationTokenSource? _cancellationToken;
        private static Task? _Task;
        private static PeriodicTimer? _beaconTimer;

        public static bool MulticastEnabled
        {
            get => multicastEnabled;
            set {
                if (CanUseMulticast)
                    multicastEnabled = value;
            }
        }
        public static string UserName { get; private set; } = "Player" + Random.Shared.Next(1000).ToString("D3");
        public static string Secret
        {
            get => _secret;
            private set {
                _secret = value;
                _hmac = CryptographyHelper.DeriveKeyFromSecret(_secret);
            }
        }
        public static Task<TcpTlsPeer>? Connection { get; private set; } = null;
        private static IPEndPoint? _clientEndpoint;
        private static string? _clientName;
        public static string ClientName => _clientName ?? string.Empty;

        static UDP_Broadcast_Helper()
        {
            _udp = new UdpClient(Port);
            _udp.EnableBroadcast = true;
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0)) {
                socket.Connect("8.8.8.8", 65530);
                var endPoint = socket.LocalEndPoint as IPEndPoint;
                iPAddress = endPoint!.Address;
            }
            if (CanUseMulticast) {
                try {
                    _udp.JoinMulticastGroup(MulticastAddress.Address, iPAddress);
                }
                catch {
                    foreach (var ni in NetworkInterface.GetAllNetworkInterfaces()) {
                        try {
                            if (ni.OperationalStatus != OperationalStatus.Up) continue;
                            if (!ni.SupportsMulticast) continue;
                            if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                            var addrs = ni.GetIPProperties().UnicastAddresses;
                            var ipv4 = addrs
                                .Select(a => a.Address)
                                .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a));

                            if (ipv4 != null) {
                                _udp.JoinMulticastGroup(MulticastAddress.Address, ipv4);
                            }
                        }
                        catch { }
                    }
                }
            }
        }

        //Hosting
        public static void StartHosting(string username)
        {
            if (username != string.Empty)
                UserName = username;
            Secret = CryptographyHelper.GenerateRandomSecret();

            if (_cancellationToken != null && !_cancellationToken.IsCancellationRequested) throw new InvalidOperationException("Already started");
            _cancellationToken = new CancellationTokenSource();
            CancellationToken token = _cancellationToken.Token;

            _Task = Task.Run(() => ReceiveLoopAsync(token), token);

            _beaconTimer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            _ = Task.Run(async () => {
                while (await _beaconTimer.WaitForNextTickAsync(token)) {
                    if (Connection is not null) break;
                    try {
                        await SendDiscoverAsync(token);
                    }
                    catch { }
                }
            }, token);
        }

        public static async Task StopAsync()
        {
            _beaconTimer?.Dispose();
            _cancellationToken?.Cancel();
            if (_Task != null) {
                await _Task.ConfigureAwait(false);
                _Task = null;
            }
            _discovered.Clear();
            Connection = null;
        }

        public static void Dispose()
        {
            StopAsync().Wait();
            if (CanUseMulticast)
                try { _udp.DropMulticastGroup(MulticastAddress.Address); } catch { }
            _udp.Dispose();
        }

        private static async Task SendDiscoverAsync(CancellationToken token)
        {
            var payload = new DiscoveryPayload("DISCOVER", UserName, iPAddress, CryptographyHelper.NowMs());
            byte[] serialized = MessagePackSerializer.Serialize(payload, MsgpackOptions);

            // compute HMAC-SHA512 over serialized
            byte[] hmac;
            using (var hmacSha = new HMACSHA512(_hmac)) {
                hmac = hmacSha.ComputeHash(serialized);
            }

            // build packet: MAGIC | serialized | HMAC
            var packet = new byte[Magic.Length + serialized.Length + hmac.Length];
            int idx = 0;
            Buffer.BlockCopy(Magic, 0, packet, idx, Magic.Length);
            idx += Magic.Length;
            Buffer.BlockCopy(serialized, 0, packet, idx, serialized.Length);
            idx += serialized.Length;
            Buffer.BlockCopy(hmac, 0, packet, idx, hmac.Length);

            if (MulticastEnabled) {
                await _udp.SendAsync(packet, packet.Length, MulticastAddress);
            }
            else {
                await _udp.SendAsync(packet, packet.Length, BroadcastAddress);
            }

            Debug.WriteLine("Host: broadcasted DISCOVER");
        }

        private static async Task ReceiveLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested && Connection is null) {
                UdpReceiveResult res;
                try {
                    res = await _udp.ReceiveAsync(token);
                }
                catch (OperationCanceledException) { break; }
                catch { continue; }

                // process packet
                _ = Task.Run(() => ProcessIncomingPacket(res, token));
            }
        }

        private static void ProcessIncomingPacket(UdpReceiveResult res, CancellationToken token)
        {
            try {
                byte[] buf = res.Buffer;
                if (buf.Length < Magic.Length + 64) return; // too short (hmac 64)
                if (res.RemoteEndPoint.Address.Equals(iPAddress)) return; // from self
                if (!buf.AsSpan(0, Magic.Length).SequenceEqual(Magic)) return;

                int idx = Magic.Length;
                int hmacLen = 64;
                int serializedLen = buf.Length - Magic.Length - hmacLen;
                if (serializedLen <= 0) return;

                var serialized = new byte[serializedLen];
                Buffer.BlockCopy(buf, idx, serialized, 0, serializedLen);
                idx += serializedLen;
                var recvHmac = new byte[hmacLen];
                Buffer.BlockCopy(buf, idx, recvHmac, 0, hmacLen);

                // verify HMAC
                byte[] expected;
                using (var hmacSha = new HMACSHA512(_hmac)) {
                    expected = hmacSha.ComputeHash(serialized);
                }

                if (!CryptographyHelper.AreEqualFixedTime(expected, recvHmac)) {
                    Debug.WriteLine("Host: invalid HMAC");
                    return;
                }

                // deserialize
                var payload = MessagePackSerializer.Deserialize<DiscoveryPayload>(serialized, MsgpackOptions);
                if (!res.RemoteEndPoint.Address.Equals(payload.IP)) {
                    Debug.WriteLine("Host: incorrect IP in payload");
                    return;
                }
                if (payload.Type != "JOIN") return;

                if (Math.Abs(CryptographyHelper.NowMs() - payload.Timestamp) > 2000) {
                    Debug.WriteLine("Host: stale JOIN");
                    return;
                }

                // Accept client
                _clientEndpoint = res.RemoteEndPoint;
                _clientName = payload.Username;
                Connection = TcpTlsPeer.CreateAsync(IPAddress.Any, Port - 1, _hmac, true);

                Debug.WriteLine($"Host: accepted join from {_clientName} @ {_clientEndpoint}");

                // send CONFIRM to client
                SendConfirmToClientAsync(_clientEndpoint, token).Wait();
            }
            catch (Exception ex) {
                Debug.WriteLine("Host: packet processing error: " + ex.Message);
            }
        }

        private static async Task SendConfirmToClientAsync(IPEndPoint clientEp, CancellationToken token)
        {
            DiscoveryPayload payload = new("CONFIRM", UserName, iPAddress, CryptographyHelper.NowMs());
            byte[] serialized = MessagePackSerializer.Serialize(payload, MessagePackHelper.MsgpackOptions);
            byte[] hmac;
            using (var hmacSha = new HMACSHA512(_hmac)) {
                hmac = hmacSha.ComputeHash(serialized);
            }
            byte[] packet = new byte[Magic.Length + serialized.Length + hmac.Length];
            int idx = 0;
            Buffer.BlockCopy(Magic, 0, packet, idx, Magic.Length);
            idx += Magic.Length;
            Buffer.BlockCopy(serialized, 0, packet, idx, serialized.Length);
            idx += serialized.Length;
            Buffer.BlockCopy(hmac, 0, packet, idx, hmac.Length);

            await _udp.SendAsync(packet, packet.Length, clientEp);

            Debug.WriteLine("Host: sent CONFIRM to client");
        }

        //Clientside
        private static readonly ConcurrentDictionary<IPAddress, Tuple<DiscoveryPayload, byte[], byte[]>> _discovered = [];
        private static readonly ConcurrentDictionary<IPEndPoint, TaskCompletionSource<bool>> _pendingConfirms = [];

        public static void StartClient(string username)
        {
            if (username != string.Empty)
                UserName = username;
            if (_cancellationToken != null && !_cancellationToken.IsCancellationRequested) throw new InvalidOperationException("Already started or another mode is running!");
            _cancellationToken = new CancellationTokenSource();
            CancellationToken token = _cancellationToken.Token;
            _Task = Task.Run(() => ListenLoopAsync(token), token);
        }

        private static async Task ListenLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested) {
                UdpReceiveResult res;
                try { res = await _udp.ReceiveAsync(token); }
                catch (OperationCanceledException) { break; }
                catch { continue; }

                _ = Task.Run(() => ProcessPacket(res), token);
            }
        }

        private static void ProcessPacket(UdpReceiveResult res)
        {
            try {
                byte[] buf = res.Buffer;
                if (buf.Length < Magic.Length + 64) return; // too short (hmac 64)
                if (res.RemoteEndPoint.Address.Equals(iPAddress)) return; // from self
                if (!buf.AsSpan(0, Magic.Length).SequenceEqual(Magic)) return;
                int idx = Magic.Length;
                int hmacLen = 64;
                int serializedLen = buf.Length - Magic.Length - hmacLen;
                if (serializedLen <= 0) return;
                var serialized = new byte[serializedLen];
                Buffer.BlockCopy(buf, idx, serialized, 0, serializedLen);
                idx += serializedLen;
                var recvHmac = new byte[hmacLen];
                Buffer.BlockCopy(buf, idx, recvHmac, 0, hmacLen);
                // deserialize
                var payload = MessagePackSerializer.Deserialize<DiscoveryPayload>(serialized, MsgpackOptions);
                if (!res.RemoteEndPoint.Address.Equals(payload.IP)) {
                    Debug.WriteLine("Client: incorrect IP in payload");
                    return;
                }
                if (Math.Abs(CryptographyHelper.NowMs() - payload.Timestamp) > 2000) {
                    Debug.WriteLine("Client: stale Packet");
                    return;
                }
                if (payload.Type == "DISCOVER") {
                    // store discovered host
                    Tuple<DiscoveryPayload, byte[], byte[]> newelement = new(payload, serialized, recvHmac);
                    _discovered.AddOrUpdate(payload.IP, newelement, (k, v) => newelement);
                    Debug.WriteLine($"Client: discovered host {payload.Username} @ {payload.IP}");
                }
                else if (payload.Type == "CONFIRM") {
                    byte[] expected2;
                    using (var hmacSha = new HMACSHA512(_hmac)) {
                        expected2 = hmacSha.ComputeHash(serialized);
                    }
                    if (!CryptographyHelper.AreEqualFixedTime(expected2, recvHmac)) return;
                    var ep = res.RemoteEndPoint;
                    if (_pendingConfirms.TryRemove(ep, out var tcs)) {
                        tcs.SetResult(true);
                    }
                }
                else { return; }
            }
            catch (Exception ex) {
                Debug.WriteLine("Client: packet processing error: " + ex.Message);
            }
        }

        public static Tuple<DiscoveryPayload, byte[], byte[]>[] GetDiscovered()
        {
            var now = CryptographyHelper.NowMs();
            var arr = _discovered
                .Where(kv => now - kv.Value.Item1.Timestamp <= 2000)
                .Select(kv => kv.Value)
                .ToArray();
            return arr;
        }

        public static async Task<bool> SendJoinAsync(Tuple<DiscoveryPayload, byte[], byte[]> selected, string secret, int timeoutMs = 5000)
        {
            Secret = secret;
            // verify HMAC
            byte[] expected;
            using (var hmacSha = new HMACSHA512(_hmac)) {
                expected = hmacSha.ComputeHash(selected.Item2);
            }
            if (!CryptographyHelper.AreEqualFixedTime(expected, selected.Item3)) {
                Debug.WriteLine("Client: invalid HMAC");
                return false;
            }
            //
            var payload = new DiscoveryPayload("JOIN", UserName, iPAddress, CryptographyHelper.NowMs());
            byte[] serialized = MessagePackSerializer.Serialize(payload, MsgpackOptions);
            byte[] hmac;
            using (var hmacSha = new HMACSHA512(_hmac)) {
                hmac = hmacSha.ComputeHash(serialized);
            }

            byte[] packet = new byte[Magic.Length + serialized.Length + hmac.Length];
            int idx = 0;
            Buffer.BlockCopy(Magic, 0, packet, idx, Magic.Length);
            idx += Magic.Length;
            Buffer.BlockCopy(serialized, 0, packet, idx, serialized.Length);
            idx += serialized.Length;
            Buffer.BlockCopy(hmac, 0, packet, idx, hmac.Length);

            IPEndPoint hostEp = new(selected.Item1.IP, Port);
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingConfirms[hostEp] = tcs;
            await _udp.SendAsync(packet, packet.Length, hostEp);

            using var cts = new CancellationTokenSource(timeoutMs);
            using (cts.Token.Register(() => tcs.TrySetCanceled())) {
                try {
                    await tcs.Task;
                    Debug.WriteLine("Client: received CONFIRM from host");
                    Connection = TcpTlsPeer.CreateAsync(selected.Item1.IP, Port - 1, _hmac, false);
                    return true;
                }
                catch (TaskCanceledException) {
                    _pendingConfirms.TryRemove(hostEp, out _);
                    return false;
                }
            }
        }

        //Tools
        private static bool CheckMulticastAvailable()
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces()) {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                if (!nic.SupportsMulticast) continue;
                var props = nic.GetIPProperties();
                if (props.UnicastAddresses.Any(u => u.Address.AddressFamily == AddressFamily.InterNetwork))
                    return true;
            }
            return false;
        }

    }
}
