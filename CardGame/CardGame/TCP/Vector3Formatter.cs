using MessagePack;
using MessagePack.Formatters;
using Microsoft.Xna.Framework;

namespace CardGame.TCP {
    public class Vector3Formatter : IMessagePackFormatter<Vector3> {
        public void Serialize(ref MessagePackWriter writer, Vector3 value, MessagePackSerializerOptions options)
        {
            writer.WriteArrayHeader(3);
            writer.Write(value.X);
            writer.Write(value.Y);
            writer.Write(value.Z);
        }

        public Vector3 Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            var count = reader.ReadArrayHeader();
            if (count != 3) throw new MessagePackSerializationException("Invalid Vector3 format");
            float x = reader.ReadSingle();
            float y = reader.ReadSingle();
            float z = reader.ReadSingle();
            return new Vector3(x, y, z);
        }
    }
}
