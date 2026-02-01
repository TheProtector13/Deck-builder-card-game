using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace CardGame.TCP {
    internal static class CryptographyHelper {
        private static TimeSpan _ntpOffset = TimeSpan.Zero;
        private static int _ntpOffsetSetTry = 10;

        static CryptographyHelper()
        {
            _ = Task.Run(SetNtpOffsetAsync);
        }

        private async static Task SetNtpOffsetAsync()
        {
            while (_ntpOffsetSetTry > 0) {
                try {
                    _ntpOffset = GetNetworkTimeOffset("pool.ntp.org");
                    _ntpOffsetSetTry = 0;
                    return;
                }
                catch (Exception e) {
                    Debug.WriteLine("Failed to get NTP time offset, retrying... " + e);
                    _ntpOffsetSetTry--;
                    await Task.Delay(5000);
                }
            }
        }

        private static TimeSpan GetNetworkTimeOffset(string ntpServer, int port = 123, int timeoutMs = 3000)
        {
            using var udp = new UdpClient();
            udp.Client.ReceiveTimeout = timeoutMs;

            var ntpData = new byte[48];
            ntpData[0] = 0x1B; // NTP request

            udp.Send(ntpData, ntpData.Length, ntpServer, port);
            var ipe = new IPEndPoint(IPAddress.Any, port);
            var result = udp.Receive(ref ipe);
            if (result.Length < 48) throw new Exception("Invalid NTP response");
            // Transmit timestamp: byte 40–43 int part, 44–47 fractional
            uint intPart = BinaryPrimitives.ReadUInt32BigEndian(result.AsSpan(40));
            uint fracPart = BinaryPrimitives.ReadUInt32BigEndian(result.AsSpan(44));

            const ulong epochOffset = 2208988800UL; // NTP -> Unix timestamp
            double milliseconds = ((intPart - epochOffset) * 1000.0) + (fracPart * 1000.0 / 0x100000000UL);

            var networkTime = DateTimeOffset.FromUnixTimeMilliseconds((long)milliseconds).UtcDateTime;
            return networkTime - DateTime.UtcNow;
        }

        public static byte[] DeriveKeyFromSecret(string secret) => SHA512.HashData(Encoding.UTF8.GetBytes(secret));

        /// <summary>
        /// Determines whether two byte sequences are equal in a manner that is resistant to timing attacks.
        /// </summary>
        /// <remarks>This method performs the comparison in fixed time, regardless of the input values, to
        /// help prevent timing attacks that could reveal information about the compared data.</remarks>
        /// <param name="a">The first read-only span of bytes to compare.</param>
        /// <param name="b">The second read-only span of bytes to compare.</param>
        /// <returns>true if the byte sequences are equal; otherwise, false.</returns>
        public static bool AreEqualFixedTime(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b) => CryptographicOperations.FixedTimeEquals(a, b);

        public static long NowMs() => (long)(DateTime.UtcNow + _ntpOffset - DateTime.UnixEpoch).TotalMilliseconds;

        public static string GenerateRandomSecret(int length = 8)
        {
            if (length <= 0) return string.Empty;
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var secretChars = new char[length];
            using (var rng = RandomNumberGenerator.Create()) {
                byte[] randomBytes = new byte[length];
                rng.GetBytes(randomBytes);
                for (int i = 0; i < length; i++) {
                    secretChars[i] = chars[randomBytes[i] % chars.Length];
                }
            }
            return new string(secretChars);
        }

    }
}
