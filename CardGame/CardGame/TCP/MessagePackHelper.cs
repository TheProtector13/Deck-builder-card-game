using System.Net;
using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;

namespace CardGame.TCP {
    internal static class MessagePackHelper {
        public static readonly MessagePackSerializerOptions MsgpackOptions;

        public enum CardType {
            Deck,
            Scrap,
            Hand,
            Shop
        }

        public enum ActionType {
            Play,
            Buy,
            Steal,
            Flip,
            Scrap,
            ScrapFromShop,
            ScrapFromHand,
            Destroy,
            Draw,
            EndTurn,
            ForestPlanet,
            IcePlanet,
            DesertPlanet
        }

        static MessagePackHelper()
        {
            var resolver = CompositeResolver.Create(new IMessagePackFormatter[] { new Vector3Formatter(), new IPAddressFormatter() },
                new IFormatterResolver[] { StandardResolver.Instance });
            MsgpackOptions = MessagePackSerializerOptions.Standard.WithResolver(resolver)
                .WithCompression(MessagePackCompression.Lz4BlockArray);
        }

        [MessagePackObject(AllowPrivate = true)]
        public record DiscoveryPayload(
        [property: Key(0)] string Type,   // "DISCOVER" | "JOIN" | "CONFIRM"
        [property: Key(1)] string Username,
        [property: Key(2)] IPAddress IP,
        [property: Key(3)] long Timestamp);

        [MessagePackObject(AllowPrivate = true)]
        public record CardListPayload(
        [property: Key(0)] bool HostCards,
        [property: Key(1)] CardType CardType,
        [property: Key(2)] CardDetails[] Cards,
        [property: Key(3)] long Timestamp);

        [MessagePackObject(AllowPrivate = true)]
        public record ActionPayload(
        [property: Key(0)] ActionType Action,
        [property: Key(1)] CardDetails? Card,
        [property: Key(2)] long Timestamp);
    }
}
