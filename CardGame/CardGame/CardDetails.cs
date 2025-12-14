using Microsoft.Xna.Framework;
using static CardGame.Card;

namespace CardGame {
    internal readonly struct CardDetails {
        public CardDetails(Fraction fraction, string CardName, string CardDescription, string CardQuote, int Attack, int Health, int Money, int Price, bool EffectsTerrainType, Vector3 EffectsTerrainAmount, Effect CardEffect, int EffectAmount, Fraction EffectRequirement)
        {
            this.CardFraction = fraction;
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
    }
}
