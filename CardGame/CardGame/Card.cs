using System;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

#nullable enable
namespace CardGame {
    internal class Card : IDrawable, ICloneable {
        private static readonly float CARD_WIDTH_SCALE = 2f / 3f;
        private static readonly Texture2D cardBG;
        private static readonly Texture2D cardBack;
        private static readonly Texture2D borderEffect;
        private static readonly Texture2D[] fractionIcons;
        private static readonly Texture2D[] otherIcons;
        private readonly Texture2D cardIMG;
        private readonly Texture2D? cardIMG_FG;
        private readonly TextBox NameBox;
        private readonly TextBox DescriptionBox;
        private readonly TextBox QuoteBox;
        private readonly TextBox PriceBox;
        private readonly TextBox EffectBox;
        private readonly TextBox[] OBJBox = [];
        private bool changed = true;
        private bool _flipped = false;
        private Rectangle _rect;
        private Rectangle IMG_rect;
        private Rectangle IMG_rect_FG;
        private Rectangle FRACTION_rect;
        private Rectangle PRICE_rect;
        private Rectangle DESCRIPTION_rect;
        private Rectangle[] OBJrects = [];
        private Rectangle[] EffectRects = [];

        public Rectangle Rect
        {
            get => _rect;
            set {
                if (_rect != value) {
                    _rect = value;
                    changed = true;
                }
            }
        }
        public bool FGCentered { get; set; } = false;
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
                    changed = true;
                }
            }
        }
        public bool RenderPrice { get; set; } = false;

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
            SelfDestruct,
            ScrapOwnCard,
            ScrapFromShop,
            AntiShow,
            ShowHand,
            ShowDeck,
            StealCard,
            ScrapEnemyCard,
            DrawCard,
            AttackBonus,
            HealthBonus,
            MoneyBonus,
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

        static Card()
        {
            cardBG = ResourceManager.Textures["PaperBG"][0];
            cardBack = ResourceManager.Textures["CardBG"][0];
            borderEffect = ResourceManager.Textures["CardIMGCorner"][0];
            fractionIcons = [
                ResourceManager.Textures["AllianceIconWB"][0],
                ResourceManager.Textures["CollectorCultIconWB"][0],
                ResourceManager.Textures["EmpireIconWB"][0],
                ResourceManager.Textures["MachinesIconWB"][0],
                ResourceManager.Textures["TheEyeIconWB"][0]
            ];
            otherIcons = [
                ResourceManager.Textures["AttackBGI"][0],
                ResourceManager.Textures["HealthBGI"][0],
                ResourceManager.Textures["MoneyBGI"][0]
            ];
        }

        public Card(Rectangle rect, Texture2D cardIMG, Texture2D? cardIMG_FG, CardDetails details)
        {
            _rect = rect;
            this.cardIMG = cardIMG;
            this.cardIMG_FG = cardIMG_FG;
            this.CardFraction = details.CardFraction;
            FontSystem defFont = ResourceManager.Fonts["FONT_DEF"];
            FontSystem defFontBold = ResourceManager.Fonts["FONT_DEF_B"];
            FontSystem font = CardFraction switch {
                Fraction.Alliance => ResourceManager.Fonts["FONT_A"],
                Fraction.CollectorCult => ResourceManager.Fonts["FONT_C"],
                Fraction.Empire => ResourceManager.Fonts["FONT_E"],
                Fraction.Machines => ResourceManager.Fonts["FONT_M"],
                Fraction.TheEye => ResourceManager.Fonts["FONT_TE"],
                _ => ResourceManager.Fonts["FONT_DEF"]
            };
            this.Attack = details.Attack;
            this.Health = details.Health;
            this.Money = details.Money;
            this.Price = details.Price;
            this.EffectsTerrainType = details.EffectsTerrainType;
            this.EffectsTerrainAmount = details.EffectsTerrainAmount;
            this.CardEffect = details.CardEffect;
            this.EffectAmount = details.EffectAmount;
            this.EffectRequirement = details.EffectRequirement;
            int objcount = 0;
            if (Attack > 0) objcount++;
            if (Health > 0) objcount++;
            if (Money > 0) objcount++;
            OBJBox = new TextBox[objcount];
            for (int i = 0; i < objcount; i++) {
                OBJBox[i] = new TextBox(_rect, defFontBold);
            }
            int index = 0;
            if (Attack > 0) {
                OBJBox[index].Text = GetTrueAttack().ToString();
                index++;
            }
            if (Health > 0) {
                OBJBox[index].Text = details.Health.ToString();
                index++;
            }
            if (Money > 0) {
                OBJBox[index].Text = details.Money.ToString();
            }
            this.NameBox = new TextBox(_rect, font) {
                Text = details.CardName,
                BGColor = new(191, 191, 191)
            };
            this.DescriptionBox = new TextBox(_rect, defFont) {
                Text = details.CardDescription,
                BGColor = new(191, 191, 191)
            };
            this.QuoteBox = new TextBox(_rect, defFontBold) {
                Text = details.CardQuote,
                Color = Color.White,
            };
            this.PriceBox = new TextBox(_rect, defFontBold) {
                Text = details.Price.ToString()
            };
            EffectBox = new TextBox(_rect, defFontBold) {
                Text = details.EffectAmount.ToString()
            };
            CalculateLayout();
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
                this.NameBox.Text,
                this.DescriptionBox.Text,
                this.QuoteBox.Text,
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

        private void CalculateLayout()
        {
            _rect = new Rectangle(_rect.X, _rect.Y, (int)MathF.Round(_rect.Height * CARD_WIDTH_SCALE), _rect.Height);
            if (Flipped) return;
            int FractionSize = (int)Math.Round(_rect.Height * 0.13215859);
            FRACTION_rect = new Rectangle(_rect.X, _rect.Y, FractionSize, FractionSize);
            PRICE_rect = new Rectangle(_rect.Right - FractionSize, _rect.Y, FractionSize, FractionSize);
            int img_size = (int)Math.Round(_rect.Height * 0.618);
            int img_offset = (_rect.Width - img_size) / 2;
            IMG_rect = new Rectangle(
                _rect.X + img_offset,
                _rect.Y + img_offset,
                img_size,
                img_size);
            if (cardIMG_FG != null) {
                IMG_rect_FG = FGCentered ? IMG_rect : DisplayInfo.FitRectBottom(cardIMG_FG.Bounds, IMG_rect);
            }
            PriceBox.Rect = PRICE_rect;
            PriceBox.SizeOffset = FractionSize * 0.2f;
            int nameBoxHeight = (int)Math.Round(_rect.Height * 0.08);
            NameBox.Rect = new Rectangle(
                _rect.X + img_offset,
                _rect.Y + img_offset + img_size,
                img_size,
                nameBoxHeight);
            NameBox.SizeOffset = nameBoxHeight * 0.2f;
            //OBJ
            int objcount = 0;
            if (Attack > 0) objcount++;
            if (Health > 0) objcount++;
            if (Money > 0) objcount++;
            OBJrects = new Rectangle[objcount];
            int objspacing = (int)Math.Round(_rect.Width * 0.1);
            int objsize = (int)MathF.Round(FractionSize * 0.6f);
            int startX = _rect.X + ((_rect.Width - ((objsize * objcount) + (objspacing * (objcount - 1)))) / 2);
            int objY = _rect.Y + img_offset + img_size + nameBoxHeight;
            for (int i = 0; i < objcount; i++) {
                OBJrects[i] = new Rectangle(
                    startX + (i * (objsize + objspacing)),
                    objY,
                    objsize,
                    objsize);
                OBJBox[i].Rect = OBJrects[i];
                OBJBox[i].SizeOffset = objsize * 0.05f;
            }
            //
            int quoteBoxHeight = (int)Math.Round(_rect.Height * 0.07666015625);
            int quoteoffset = (int)Math.Round(_rect.Width * 0.115102639296188 / 2);
            QuoteBox.Rect = new Rectangle(
                _rect.X + quoteoffset,
                _rect.Bottom - quoteBoxHeight,
                _rect.Width - (2 * quoteoffset),
                quoteBoxHeight);
            QuoteBox.SizeOffset = 0f;
            QuoteBox.VerticalSizeOffset = 0f;
            int descriptionBoxHeight = _rect.Height - (img_offset + img_size + nameBoxHeight + objsize) - quoteBoxHeight;
            DESCRIPTION_rect = new(
                    _rect.X,
                    objY + objsize,
                    _rect.Width,
                    descriptionBoxHeight);
            if (EffectRequirement == Fraction.None) {
                if (CardEffect == Effect.AttackBonus ||
                    CardEffect == Effect.HealthBonus ||
                    CardEffect == Effect.MoneyBonus) {
                    EffectRects = new Rectangle[1];
                    EffectRects[0] = new Rectangle(
                        DESCRIPTION_rect.X + ((DESCRIPTION_rect.Width - objsize) / 2),
                        DESCRIPTION_rect.Y + ((DESCRIPTION_rect.Height - objsize) / 2),
                        objsize,
                        objsize);
                    EffectBox.Rect = EffectRects[0];
                    EffectBox.SizeOffset = objsize * 0.05f;
                }
                else {
                    DescriptionBox.Rect = DESCRIPTION_rect;
                    DescriptionBox.SizeOffset = _rect.Width * 0.03f;
                }
            }
            else {
                EffectRects = new Rectangle[2];
                EffectRects[0] = new Rectangle(
                    DESCRIPTION_rect.X + 2,
                    DESCRIPTION_rect.Y + ((DESCRIPTION_rect.Height - objsize) / 2),
                    objsize,
                    objsize);
                if (CardEffect == Effect.AttackBonus ||
                    CardEffect == Effect.HealthBonus ||
                    CardEffect == Effect.MoneyBonus) {
                    EffectRects[1] = new Rectangle(
                        DESCRIPTION_rect.X + ((DESCRIPTION_rect.Width - objsize) / 2),
                        DESCRIPTION_rect.Y + ((DESCRIPTION_rect.Height - objsize) / 2),
                        objsize,
                        objsize);
                    EffectBox.Rect = EffectRects[1];
                    EffectBox.SizeOffset = objsize * 0.05f;
                }
                else {
                    DescriptionBox.Rect = new(
                        DESCRIPTION_rect.X + objsize + 4,
                        DESCRIPTION_rect.Y,
                        DESCRIPTION_rect.Width - objsize - 4,
                        DESCRIPTION_rect.Height);
                    DescriptionBox.SizeOffset = _rect.Width * 0.03f;
                }
            }
        }

        public object Clone()
        {
            return new Card(new(_rect.X, _rect.Y, _rect.Width, _rect.Height), cardIMG, cardIMG_FG, GetCardDetails()) {
                FGCentered = this.FGCentered,
                RenderPrice = this.RenderPrice,
                Flipped = this.Flipped,
                BaseApplied = this.BaseApplied,
                EffectsApplied = this.EffectsApplied
            };
        }

        public void Update(GameTime gameTime)
        {
            if (!changed) return;
            changed = false;
            CalculateLayout();
            if (Flipped) return;
            NameBox.Update(gameTime);
            DescriptionBox.Update(gameTime);
            QuoteBox.Update(gameTime);
            PriceBox.Update(gameTime);
            foreach (var box in OBJBox) {
                box.Update(gameTime);
            }
            EffectBox.Update(gameTime);
        }

        public void Draw(GameTime gameTime, SpriteBatch spriteBatch)
        {
            spriteBatch.Draw(cardBG, _rect, Color.White);
            if (Flipped) {
                spriteBatch.Draw(cardBack, _rect, Color.White);
                return;
            }
            else {
                spriteBatch.Draw(cardIMG, IMG_rect, Color.White);
                if (cardIMG_FG != null) {
                    spriteBatch.Draw(cardIMG_FG, IMG_rect_FG, Color.White);
                }
                spriteBatch.Draw(borderEffect, IMG_rect, Color.White);
                if (CardFraction != Fraction.None)
                    spriteBatch.Draw(fractionIcons[(int)CardFraction], FRACTION_rect, Color.White);
                if (RenderPrice) {
                    spriteBatch.Draw(otherIcons[2], PRICE_rect, Color.White);
                    PriceBox.Draw(gameTime, spriteBatch);
                }
                NameBox.Draw(gameTime, spriteBatch);
                int index = 0;
                if (Attack > 0) {
                    spriteBatch.Draw(otherIcons[0], OBJrects[index], Color.White);
                    OBJBox[index].Draw(gameTime, spriteBatch);
                    index++;
                }
                if (Health > 0) {
                    spriteBatch.Draw(otherIcons[1], OBJrects[index], Color.White);
                    OBJBox[index].Draw(gameTime, spriteBatch);
                    index++;
                }
                if (Money > 0) {
                    spriteBatch.Draw(otherIcons[2], OBJrects[index], Color.White);
                    OBJBox[index].Draw(gameTime, spriteBatch);
                }
                Texture2D rectTexture = ResourceManager.GetColor(new Color(191, 191, 191), spriteBatch);
                spriteBatch.Draw(rectTexture, DESCRIPTION_rect, Color.White);
                int erectIndex = 0;
                if (EffectRequirement != Fraction.None) {
                    spriteBatch.Draw(fractionIcons[(int)EffectRequirement], EffectRects[0], Color.White);
                    erectIndex = 1;
                }
                if (CardEffect == Effect.AttackBonus) {
                    spriteBatch.Draw(otherIcons[0], EffectRects[erectIndex], Color.White);
                    EffectBox.Draw(gameTime, spriteBatch);
                }
                else if (CardEffect == Effect.HealthBonus) {
                    spriteBatch.Draw(otherIcons[1], EffectRects[erectIndex], Color.White);
                    EffectBox.Draw(gameTime, spriteBatch);
                }
                else if (CardEffect == Effect.MoneyBonus) {
                    spriteBatch.Draw(otherIcons[2], EffectRects[erectIndex], Color.White);
                    EffectBox.Draw(gameTime, spriteBatch);
                }
                else {
                    DescriptionBox.Draw(gameTime, spriteBatch);
                }
                QuoteBox.Draw(gameTime, spriteBatch);
            }
        }
    }
}
