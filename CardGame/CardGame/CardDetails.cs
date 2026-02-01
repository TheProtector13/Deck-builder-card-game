using System;
using MessagePack;
using Microsoft.Xna.Framework;
using static CardGame.Card;

namespace CardGame {
    [MessagePackObject(AllowPrivate = true)]
    internal readonly struct CardDetails : IEquatable<CardDetails> {
        [SerializationConstructor]
        public CardDetails(Fraction CardFraction, string CardName, string CardDescription, string CardQuote, int Attack, int Health, int Money, int Price, bool EffectsTerrainType, Vector3 EffectsTerrainAmount, Effect CardEffect, int EffectAmount, Fraction EffectRequirement)
        {
            this.CardFraction = CardFraction;
            this.CardName = CardName;
            this.CardDescription = CardDescription;
            this.CardQuote = CardQuote;
            this.Attack = Attack;
            this.Health = Health;
            this.Money = Money;
            this.Price = Price;
            this.EffectsTerrainType = EffectsTerrainType;
            this.EffectsTerrainAmount = EffectsTerrainAmount;
            this.CardEffect = CardEffect;
            this.EffectAmount = EffectAmount;
            this.EffectRequirement = EffectRequirement;
        }

        [property: Key(0)] public Fraction CardFraction { get; init; } = Fraction.None;
        [property: Key(1)] public string CardName { get; init; } = string.Empty;
        [property: Key(2)] public string CardDescription { get; init; } = string.Empty;
        [property: Key(3)] public string CardQuote { get; init; } = string.Empty;
        [property: Key(4)] public int Attack { get; init; } = 0;
        [property: Key(5)] public int Health { get; init; } = 0;
        [property: Key(6)] public int Money { get; init; } = 0;
        [property: Key(7)] public int Price { get; init; } = 0;
        [property: Key(8)] public bool EffectsTerrainType { get; init; } = false;
        [property: Key(9)] public Vector3 EffectsTerrainAmount { get; init; } = Vector3.Zero;
        [property: Key(10)] public Effect CardEffect { get; init; } = Effect.None;
        [property: Key(11)] public int EffectAmount { get; init; } = 0;
        [property: Key(12)] public Fraction EffectRequirement { get; init; } = Fraction.None;

        public bool Equals(CardDetails other)
        {
            return CardFraction == other.CardFraction &&
                string.Equals(CardName, other.CardName, StringComparison.Ordinal) &&
                string.Equals(CardDescription, other.CardDescription, StringComparison.Ordinal) &&
                string.Equals(CardQuote, other.CardQuote, StringComparison.Ordinal) &&
                Attack == other.Attack &&
                Health == other.Health &&
                Money == other.Money &&
                Price == other.Price &&
                EffectsTerrainType == other.EffectsTerrainType &&
                EffectsTerrainAmount.Equals(other.EffectsTerrainAmount) &&
                CardEffect == other.CardEffect &&
                EffectAmount == other.EffectAmount &&
                EffectRequirement == other.EffectRequirement;
        }

        public override bool Equals(object obj) => obj is CardDetails other && Equals(other);

        public static bool operator ==(CardDetails left, CardDetails right) => left.Equals(right);
        public static bool operator !=(CardDetails left, CardDetails right) => !left.Equals(right);

        public override int GetHashCode()
        {
            var hc = new HashCode();
            hc.Add(CardFraction);
            hc.Add(CardName, StringComparer.Ordinal);
            hc.Add(CardDescription, StringComparer.Ordinal);
            hc.Add(CardQuote, StringComparer.Ordinal);
            hc.Add(Attack);
            hc.Add(Health);
            hc.Add(Money);
            hc.Add(Price);
            hc.Add(EffectsTerrainType);
            hc.Add(EffectsTerrainAmount.X);
            hc.Add(EffectsTerrainAmount.Y);
            hc.Add(EffectsTerrainAmount.Z);
            hc.Add(CardEffect);
            hc.Add(EffectAmount);
            hc.Add(EffectRequirement);
            return hc.ToHashCode();
        }

    }
}
