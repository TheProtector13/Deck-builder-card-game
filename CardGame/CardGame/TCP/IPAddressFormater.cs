using System.Buffers;
using System.Net;
using MessagePack;
using MessagePack.Formatters;

namespace CardGame.TCP {
    public class IPAddressFormatter : IMessagePackFormatter<IPAddress> {
        public void Serialize(ref MessagePackWriter writer, IPAddress value, MessagePackSerializerOptions options)
        {
            if (value == null) {
                writer.WriteNil();
                return;
            }
            byte[] bytes = value.GetAddressBytes();
            writer.Write(bytes);
        }

        public IPAddress Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return null!;

            var seq = reader.ReadBytes() ?? throw new MessagePackSerializationException("Expected IP address bytes but got null.");

            byte[] bytes = seq.ToArray();
            if (bytes.Length != 4 && bytes.Length != 16)
                throw new MessagePackSerializationException($"Invalid IP length: {bytes.Length}");
            return new IPAddress(bytes);
        }
    }
}
