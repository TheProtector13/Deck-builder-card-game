using System;
using System.Numerics;
using static CardGame.Card;

namespace CardGame {
    internal readonly struct CardDetails : IEquatable<CardDetails> {
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

        public Fraction CardFraction { get; init; } = Fraction.None;
        public string CardName { get; init; } = string.Empty;
        public string CardDescription { get; init; } = string.Empty;
        public string CardQuote { get; init; } = string.Empty;
        public int Attack { get; init; } = 0;
        public int Health { get; init; } = 0;
        public int Money { get; init; } = 0;
        public int Price { get; init; } = 0;
        public bool EffectsTerrainType { get; init; } = false;
        public Vector3 EffectsTerrainAmount { get; init; } = Vector3.Zero;
        public Effect CardEffect { get; init; } = Effect.None;
        public int EffectAmount { get; init; } = 0;
        public Fraction EffectRequirement { get; init; } = Fraction.None;

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
