using System;
using System.Numerics;

#nullable enable
namespace CardGame {
    internal class Card : ICloneable {
        private bool _flipped = false;
        /// <summary>
        /// Gets or sets a value indicating whether the object is flipped.
        /// If true, the back of the card is rendered.
        /// </summary>
        public bool Flipped
        {
            get => _flipped;
            set {
                if (_flipped != value) {
                    _flipped = value;
                }
            }
        }

        public string Name { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string Quote { get; init; } = string.Empty;
        public Fraction CardFraction { get; init; } = Fraction.None;
        public int Attack { get; init; } = 0;
        public int Health { get; init; } = 0;
        public int Money { get; init; } = 0;
        public int Price { get; init; } = 0;
        public bool EffectsTerrainType { get; init; } = false;
        public Vector3 EffectsTerrainAmount { get; init; } = Vector3.Zero;
        public bool BaseApplied { get; set; } = false;
        public bool EffectsApplied { get; set; } = false;
        public Effect CardEffect { get; init; } = Effect.None;
        public int EffectAmount { get; init; } = 0;
        public Fraction EffectRequirement { get; init; } = Fraction.None;

        public enum Effect {
            ScrapEnemyCard,
            ScrapFromShop,
            AntiShow,
            StealCard,
            DrawCard,
            ScrapOwnCard,
            AttackBonus,
            HealthBonus,
            MoneyBonus,
            ShowHand,
            ShowDeck,
            SelfDestruct,
            None
        }

        public enum Fraction {
            Alliance,
            CollectorCult,
            Empire,
            Machines,
            TheEye,
            None
        }

        public Card(CardDetails details)
        {
            this.Name = details.CardName;
            this.Description = details.CardDescription;
            this.Quote = details.CardQuote;
            this.CardFraction = details.CardFraction;
            this.Attack = details.Attack;
            this.Health = details.Health;
            this.Money = details.Money;
            this.Price = details.Price;
            this.EffectsTerrainType = details.EffectsTerrainType;
            this.EffectsTerrainAmount = details.EffectsTerrainAmount;
            this.CardEffect = details.CardEffect;
            this.EffectAmount = details.EffectAmount;
            this.EffectRequirement = details.EffectRequirement;
        }

        public int GetTrueAttack()
        {
            if (EffectsTerrainType) {
                return (int)MathF.Round(Attack * (1f + (int)DeckGenerator.TerrainType switch {
                    0 => EffectsTerrainAmount.X,
                    1 => EffectsTerrainAmount.Y,
                    2 => EffectsTerrainAmount.Z,
                    _ => 0
                }));
            }
            return Attack;
        }

        public CardDetails GetCardDetails()
        {
            return new CardDetails(
                this.CardFraction,
                this.Name,
                this.Description,
                this.Quote,
                this.Attack,
                this.Health,
                this.Money,
                this.Price,
                this.EffectsTerrainType,
                this.EffectsTerrainAmount,
                this.CardEffect,
                this.EffectAmount,
                this.EffectRequirement);
        }

        public void ResetPlayedStatus()
        {
            BaseApplied = false;
            EffectsApplied = false;
        }

        public object Clone()
        {
            return new Card(GetCardDetails()) {
                Flipped = this.Flipped,
                BaseApplied = this.BaseApplied,
                EffectsApplied = this.EffectsApplied
            };
        }
    }
}
