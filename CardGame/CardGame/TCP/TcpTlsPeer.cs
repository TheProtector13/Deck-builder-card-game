using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using MessagePack;

#nullable enable
namespace CardGame.TCP {
    /// <summary>
    /// Mutual-TLS length-prefixed peer (TLS1.3 over TCP) that can act as Host (server) or Client.
    /// - Both sides generate an ephemeral self-signed certificate.
    /// - After TLS handshake each side verifies there is no MITM by exchanging HMAC-based proofs over the
    ///   already-observed TLS peer public key using the shared secret.
    /// - Wire message layout (main messages): [4 byte BE length][1 byte type][MessagePack(payload)]
    ///   type: 0 = CardListPayload, 1 = ActionPayload
    /// - Handshake verification message (application-level, sent immediately after TLS):
    ///   [4 byte BE length][8 byte timestamp][2 byte pubkeyLen][pubkey][32 byte proof1][32 byte proof2]
    ///   proof1 = HMAC_SHA256(sharedSecret, pubkey)
    ///   proof2 = HMAC_SHA256(sharedSecret, proof1 || timestampBytes)
    /// - Received payloads are kept in a timestamp-sorted list (by payload.Timestamp). Oldest at index 0.
    /// </summary>
    internal class TcpTlsPeer : IDisposable {
        private readonly IPAddress _targetIp;
        private readonly int _port;
        private readonly byte[] _sharedSecret;
        private readonly bool _isHost;
        public bool IsHost => _isHost;

        private X509Certificate2? myCert;
        private TcpListener? _listener;
        private TcpClient? _client;
        private SslStream? _sslStream;
        private NetworkStream? _netStream;

        private readonly CancellationTokenSource _cts = new();
        private Task? _receiveLoopTask;

        // Received packets sorted by SequenceNumber (oldest first)
        private readonly List<SequencedReceivedPacket> _received = [];
        private readonly object _receivedLock = new();

        private readonly SemaphoreSlim _sendLock = new(1, 1);
        private int _nextSendSequence = 0;
        private int _expectedReceiveSequence = 0;
        private readonly SortedDictionary<int, SequencedReceivedPacket> _outOfOrderPackets = [];

        private volatile bool _isConnected = false;

        private TcpTlsPeer(IPAddress targetIp, int port, byte[] sharedSecret, bool isHost)
        {
            _targetIp = targetIp;
            _port = port;
            _sharedSecret = sharedSecret ?? throw new ArgumentNullException(nameof(sharedSecret));
            _isHost = isHost;

            if (_sharedSecret.Length < 32)
                throw new ArgumentException("sharedSecret must be at least 32 bytes (recommended 64 bytes)", nameof(sharedSecret));
        }

        public static async Task<TcpTlsPeer> CreateAsync(IPAddress targetIp, int port, byte[] sharedSecret, bool isHost)
        {
            var p = new TcpTlsPeer(targetIp, port, sharedSecret, isHost);
            await p.InitializeAsync().ConfigureAwait(false);
            return p;
        }

        private async Task InitializeAsync()
        {
            // both sides create a self-signed certificate
            myCert = CreateSelfSignedCertificate();

            if (_isHost) {
                _listener = new TcpListener(_targetIp, _port);
                _listener.Start();
                _client = await _listener.AcceptTcpClientAsync(_cts.Token).ConfigureAwait(false);
                _netStream = _client.GetStream();

                _sslStream = new SslStream(_netStream, leaveInnerStreamOpen: false, userCertificateValidationCallback: (a, b, c, d) => true);

                var serverOptions = new SslServerAuthenticationOptions {
                    ServerCertificate = myCert,
                    ClientCertificateRequired = true,
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                    CertificateRevocationCheckMode = X509RevocationMode.NoCheck
                };

                await _sslStream.AuthenticateAsServerAsync(serverOptions, _cts.Token).ConfigureAwait(false);
            }
            else {
                _client = new TcpClient();
                for (int attempt = 0; attempt <= 4; attempt++) {
                    try {
                        await _client.ConnectAsync(_targetIp, _port, _cts.Token).ConfigureAwait(false);
                        break;
                    }
                    catch (OperationCanceledException) {
                        throw;
                    }
                    catch (SocketException) when (attempt < 4) {
                        await Task.Delay(300, _cts.Token).ConfigureAwait(false);
                        _client?.Dispose();
                        _client = new TcpClient();
                    }
                }
                _netStream = _client.GetStream();

                _sslStream = new SslStream(_netStream, leaveInnerStreamOpen: false, userCertificateValidationCallback: (a, b, c, d) => true);

                var clientCerts = new X509Certificate2Collection { myCert };
                var clientOptions = new SslClientAuthenticationOptions {
                    TargetHost = "p2p",
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                    ClientCertificates = clientCerts,
                    CertificateRevocationCheckMode = X509RevocationMode.NoCheck
                };

                await _sslStream.AuthenticateAsClientAsync(clientOptions, _cts.Token).ConfigureAwait(false);
            }

            // MITM check
            bool ok = await PerformMutualTlsSharedSecretCheckAsync(_sslStream!, _sharedSecret, _cts.Token).ConfigureAwait(false);
            if (!ok) {
                Dispose();
                throw new AuthenticationException("Mutual TLS shared-secret verification failed (possible MITM)");
            }

            Debug.WriteLine("TLS connection successfull!");
            _isConnected = true;
        }

        /// <summary>
        /// Send CardListPayload (type 0)
        /// </summary>
        public Task SendAsync(MessagePackHelper.CardListPayload payload) => SendTypedAsync(0, payload);

        /// <summary>
        /// Send ActionPayload (type 1)
        /// </summary>
        public Task SendAsync(MessagePackHelper.ActionPayload payload) => SendTypedAsync(1, payload);

        private async Task SendTypedAsync<T>(byte typeId, T payload)
        {
            if (!_isConnected || _sslStream is null) throw new InvalidOperationException("Not connected");

            byte[] body = MessagePackSerializer.Serialize(payload, MessagePackHelper.MsgpackOptions);
            byte[] packet = new byte[1 + 4 + body.Length];
            // Header: [1 byte type][4 byte BE sequence number][BODY]
            packet[0] = typeId;
            int sequence = Interlocked.Increment(ref _nextSendSequence) - 1;
            BinaryPrimitives.WriteInt32BigEndian(packet.AsSpan(1, 4), sequence);
            Buffer.BlockCopy(body, 0, packet, 5, body.Length);

            byte[] lenBuf = new byte[4];
            BinaryPrimitives.WriteInt32BigEndian(lenBuf, packet.Length);

            await _sendLock.WaitAsync().ConfigureAwait(false);
            try {
                await _sslStream.WriteAsync(lenBuf).ConfigureAwait(false);
                await _sslStream.WriteAsync(packet).ConfigureAwait(false);
                await _sslStream.FlushAsync().ConfigureAwait(false);
            }
            finally {
                _sendLock.Release();
            }
        }

        public void StartReceiving()
        {
            if (_receiveLoopTask != null) return;
            _receiveLoopTask = Task.Run(() => ReceiveLoopAsync(_cts.Token));
        }

        public async Task StopReceivingAsync() => await CloseConnectionAsync().ConfigureAwait(false);

        /// <summary>
        /// Returns true if the logical channel is still connected.
        /// </summary>
        public bool IsConnected => _isConnected;

        /// <summary>
        /// Closes the connection locally. After calling this IsConnected will be false.
        /// </summary>
        public async Task CloseConnectionAsync()
        {
            if (!_isConnected) return;
            _isConnected = false;
            try { _cts.Cancel(); } catch { }

            try { _sslStream?.Close(); } catch { }
            try { _netStream?.Close(); } catch { }
            try { _client?.Close(); } catch { }
            try { _listener?.Stop(); } catch { }

            if (_receiveLoopTask != null) {
                try { await _receiveLoopTask.ConfigureAwait(false); } catch { }
                _receiveLoopTask = null;
            }
        }

        public ReceivedPacket? TryDequeueOldest()
        {
            lock (_receivedLock) {
                if (_received.Count == 0) {
                    return null;
                }
                SequencedReceivedPacket output = _received[0];
                _received.RemoveAt(0);
                return output;
            }
        }

        public int GetNextExpectedSequence()
        {
            lock (_receivedLock) {
                return _expectedReceiveSequence;
            }
        }

        public int GetOutOfOrderCount()
        {
            lock (_receivedLock) {
                return _outOfOrderPackets.Count;
            }
        }

        private async Task ReceiveLoopAsync(CancellationToken ct)
        {
            if (_sslStream is null) return;
            try {
                while (!ct.IsCancellationRequested && _isConnected) {
                    byte[] lenBuf = new byte[4];
                    if (!await ReadExactlyAsync(_sslStream, lenBuf, ct).ConfigureAwait(false)) break;
                    int len = BinaryPrimitives.ReadInt32BigEndian(lenBuf);
                    if (len <= 0 || len > 10_000_000) break;

                    byte[] packet = new byte[len];
                    if (!await ReadExactlyAsync(_sslStream, packet, ct).ConfigureAwait(false)) break;

                    if (packet.Length < 5) continue;

                    byte type = packet[0];
                    int sequence = BinaryPrimitives.ReadInt32BigEndian(packet.AsSpan(1, 4));
                    byte[] body = new byte[packet.Length - 5];
                    Buffer.BlockCopy(packet, 5, body, 0, body.Length);

                    object? obj = type switch {
                        0 => MessagePackSerializer.Deserialize<MessagePackHelper.CardListPayload>(body, MessagePackHelper.MsgpackOptions),
                        1 => MessagePackSerializer.Deserialize<MessagePackHelper.ActionPayload>(body, MessagePackHelper.MsgpackOptions),
                        _ => null
                    };

                    if (obj is null) continue;

                    // extract timestamp
                    var prop = obj.GetType().GetProperty("Timestamp");
                    if (prop is null) continue;

                    long payloadTs = (long)prop.GetValue(obj)!;
                    long nowMs = CryptographyHelper.NowMs();
                    if (Math.Abs(nowMs - payloadTs) > 2000) continue;

                    var sequencedPacket = new SequencedReceivedPacket(obj, payloadTs, sequence);
                    ProcessReceivedPacket(sequencedPacket);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception) { }
            finally {
                _isConnected = false;
            }
        }

        private void ProcessReceivedPacket(SequencedReceivedPacket packet)
        {
            lock (_receivedLock) {
                if (packet.SequenceNumber == _expectedReceiveSequence) {
                    AddPacketToReceived(packet);
                    _expectedReceiveSequence++;

                    ProcessOutOfOrderPackets();
                }
                else if (packet.SequenceNumber > _expectedReceiveSequence) {
                    _outOfOrderPackets[packet.SequenceNumber] = packet;
                }
            }
        }

        private void ProcessOutOfOrderPackets()
        {
            while (_outOfOrderPackets.TryGetValue(_expectedReceiveSequence, out var packet)) {
                AddPacketToReceived(packet);
                _outOfOrderPackets.Remove(_expectedReceiveSequence);
                _expectedReceiveSequence++;
            }
        }

        private void AddPacketToReceived(SequencedReceivedPacket packet)
        {
            int idx = _received.BinarySearch(packet, SequencedReceivedPacket.SequenceComparer.Instance);
            if (idx < 0) idx = ~idx;
            _received.Insert(idx, packet);
        }

        private static async Task<bool> ReadExactlyAsync(Stream s, byte[] buffer, CancellationToken ct)
        {
            int off = 0;
            int toRead = buffer.Length;
            while (toRead > 0) {
                int r = await s.ReadAsync(buffer.AsMemory(off, toRead), ct).ConfigureAwait(false);
                if (r == 0) return false;
                off += r;
                toRead -= r;
            }
            return true;
        }

        private static async Task<bool> PerformMutualTlsSharedSecretCheckAsync(SslStream ssl, byte[] sharedSecret, CancellationToken ct)
        {
            // Get remote certificate public key
            var remoteCert = ssl.RemoteCertificate == null ? null : new X509Certificate2(ssl.RemoteCertificate);
            if (remoteCert == null) return false;
            byte[] remotePubKey = remoteCert.GetPublicKey();

            // Prepare our own pubkey
            var localCert = ssl.LocalCertificate == null ? null : new X509Certificate2(ssl.LocalCertificate);
            if (localCert == null) return false;
            byte[] localPubKey = localCert.GetPublicKey();

            // Build verification message
            long timestamp = CryptographyHelper.NowMs();
            byte[] timestampBytes = new byte[8];
            BinaryPrimitives.WriteInt64BigEndian(timestampBytes, timestamp);

            byte[] proof1 = HMACSHA256.HashData(sharedSecret, localPubKey);
            byte[] proof2 = HMACSHA256.HashData(sharedSecret, Concat(proof1, timestampBytes));

            // timestamp(8) | pubKeyLen(2) | pubKey | proof1(32) | proof2(32)
            ushort pkLen = (ushort)localPubKey.Length;
            int payloadLen = 8 + 2 + pkLen + 32 + 32;
            byte[] payload = new byte[payloadLen];
            int off = 0;
            Buffer.BlockCopy(timestampBytes, 0, payload, off, 8); off += 8;
            byte[] pkLenBuf = new byte[2];
            BinaryPrimitives.WriteUInt16BigEndian(pkLenBuf, pkLen);
            pkLenBuf.CopyTo(payload.AsSpan(off, 2)); off += 2;
            Buffer.BlockCopy(localPubKey, 0, payload, off, pkLen); off += pkLen;
            Buffer.BlockCopy(proof1, 0, payload, off, proof1.Length); off += proof1.Length;
            Buffer.BlockCopy(proof2, 0, payload, off, proof2.Length);

            // Send our verification payload
            byte[] lenBuf = new byte[4];
            BinaryPrimitives.WriteInt32BigEndian(lenBuf, payload.Length);
            await ssl.WriteAsync(lenBuf).ConfigureAwait(false);
            await ssl.WriteAsync(payload).ConfigureAwait(false);
            await ssl.FlushAsync().ConfigureAwait(false);

            // Read peer verification payload
            byte[] peerLenBuf = new byte[4];
            if (!await ReadExactlyAsync(ssl, peerLenBuf, ct).ConfigureAwait(false)) return false;
            int peerLen = BinaryPrimitives.ReadInt32BigEndian(peerLenBuf);
            if (peerLen <= 0 || peerLen > 10_000) return false;
            byte[] peerPayload = new byte[peerLen];
            if (!await ReadExactlyAsync(ssl, peerPayload, ct).ConfigureAwait(false)) return false;

            // Parse peer payload
            int pOff = 0;
            long peerTimestamp = BinaryPrimitives.ReadInt64BigEndian(peerPayload.AsSpan(pOff, 8)); pOff += 8;
            ushort peerPkLen = BinaryPrimitives.ReadUInt16BigEndian(peerPayload.AsSpan(pOff, 2)); pOff += 2;
            if (peerPkLen > peerLen - 8 - 2 - 64) return false;
            byte[] peerPubKey = new byte[peerPkLen];
            Buffer.BlockCopy(peerPayload, pOff, peerPubKey, 0, peerPkLen); pOff += peerPkLen;
            byte[] peerProof1 = new byte[32]; Buffer.BlockCopy(peerPayload, pOff, peerProof1, 0, 32); pOff += 32;
            byte[] peerProof2 = new byte[32]; Buffer.BlockCopy(peerPayload, pOff, peerProof2, 0, 32);

            // verify: peerPubKey matches remotePubKeyFromTLS
            if (!CryptographyHelper.AreEqualFixedTime(peerPubKey, remotePubKey)) return false; // MITM

            // verify: expectedProof1 = HMAC(sharedSecret, remotePubKeyFromTLS)
            byte[] expectedProof1 = HMACSHA256.HashData(sharedSecret, remotePubKey);
            if (!CryptographyHelper.AreEqualFixedTime(expectedProof1, peerProof1)) return false; // MITM or wrong shared secret

            // Verify peerProof2 matches expected = HMAC(sharedSecret, proof1 || timestamp)
            byte[] expectedPeerProof2 = HMACSHA256.HashData(sharedSecret, Concat(peerProof1, GetBigEndianBytes(peerTimestamp)));
            if (!CryptographyHelper.AreEqualFixedTime(expectedPeerProof2, peerProof2)) return false;

            long now = CryptographyHelper.NowMs();
            return Math.Abs(now - peerTimestamp) <= 5000;
        }

        private static byte[] Concat(params byte[][] parts)
        {
            int total = 0;
            foreach (var p in parts) total += p.Length;
            byte[] outb = new byte[total];
            int off = 0;
            foreach (var p in parts) {
                Buffer.BlockCopy(p, 0, outb, off, p.Length);
                off += p.Length;
            }
            return outb;
        }

        private static byte[] GetBigEndianBytes(long v)
        {
            Span<byte> b = stackalloc byte[8];
            BinaryPrimitives.WriteInt64BigEndian(b, v);
            return b.ToArray();
        }

        private static X509Certificate2 CreateSelfSignedCertificate()
        {
            using RSA rsa = RSA.Create(2048);
            var req = new CertificateRequest("CN=p2p.local", rsa, HashAlgorithmName.SHA512, RSASignaturePadding.Pkcs1);
            req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
            req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, false));
            req.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(req.PublicKey, false));
            var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(2));
            var pfxBytes = cert.Export(X509ContentType.Pfx);
            return new X509Certificate2(pfxBytes, (string?)null, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.PersistKeySet);
        }

        public void Dispose()
        {
            _isConnected = false;
            try { _cts.Cancel(); } catch { }
            try { _sslStream?.Dispose(); } catch { }
            try { _netStream?.Dispose(); } catch { }
            try { _client?.Dispose(); } catch { }
            try { _listener?.Stop(); } catch { }
            _cts.Dispose();
            _sendLock.Dispose();
            if (myCert != null) {
                try {
                    using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
                    store.Open(OpenFlags.ReadWrite);
                    var found = store.Certificates.Find(X509FindType.FindByThumbprint, myCert.Thumbprint, validOnly: false);
                    foreach (var c in found) {
                        store.Remove(c);
                        c.Dispose();
                    }
                }
                catch { }
                try { myCert.Dispose(); } catch { }
                myCert = null;
            }
        }
    }

    internal class SequencedReceivedPacket(object payload, long payloadTimestamp, int sequenceNumber) : ReceivedPacket(payload, payloadTimestamp) {
        public int SequenceNumber { get; } = sequenceNumber;

        public class SequenceComparer : IComparer<SequencedReceivedPacket> {
            public static readonly SequenceComparer Instance = new();
            public int Compare(SequencedReceivedPacket? x, SequencedReceivedPacket? y)
            {
                if (x == null && y == null) return 0;
                if (x == null) return -1;
                if (y == null) return 1;
                return x.SequenceNumber.CompareTo(y.SequenceNumber);
            }
        }
    }

    internal class ReceivedPacket(object payload, long payloadTimestamp) : IComparable<ReceivedPacket> {
        public object Payload { get; } = payload;
        public long PayloadTimestamp { get; } = payloadTimestamp;

        public int CompareTo(ReceivedPacket? other) => other == null ? -1 : PayloadTimestamp.CompareTo(other.PayloadTimestamp);

        public class TimestampComparer : IComparer<ReceivedPacket> {
            public static readonly TimestampComparer Instance = new();
            public int Compare(ReceivedPacket? x, ReceivedPacket? y)
            {
                if (x == null && y == null) return 0;
                if (x == null) return -1;
                if (y == null) return 1;
                return x.PayloadTimestamp.CompareTo(y.PayloadTimestamp);
            }
        }
    }
}
