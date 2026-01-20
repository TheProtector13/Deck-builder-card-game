using System;
using System.Collections.Generic;
using System.IO;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace CardGame {
    internal class SplashScreen : IDrawable, IDisposable {
        private readonly Texture2D[] loadingcards;
        private readonly Point cardSize = new(64, 64);
        private readonly Point middlepoint;
        private const int cardX = 42;
        private readonly Point origin = new(11, 61);
        private readonly int radius;
        private TimeSpan lastcardspawn = TimeSpan.Zero;
        private readonly TimeSpan cardlifespan = TimeSpan.FromSeconds(4);
        private readonly FontSystem fontSystem = new();
        private readonly TextBox percentageBox;
        // Tuple Item1 = Position, Item2 = angle in radian, Item3 = currentlifespan, Item4 = Texture
        private readonly List<Tuple<Point, double, TimeSpan, Texture2D>> cards = [];

        public double Percentage { get; set; } = 0.0;
        public bool AddNewCards { get; set; } = true;
        public bool Finished { get; private set; } = false;

        private SplashScreen() => throw new NotImplementedException();
        public SplashScreen(ContentManager Content)
        {
            fontSystem.AddFont(File.ReadAllBytes("Content/FONTS/FONT_E.otf"));
            middlepoint = new(DisplayInfo.ScreenWidth / 2, DisplayInfo.ScreenHeight / 2);
            radius = (int)Math.Ceiling(DisplayInfo.ScreenWidth * 0.1f);
            percentageBox = new(
                new(middlepoint.X - radius, middlepoint.Y - (radius / 4),
                    radius * 2, radius / 2), fontSystem) {
                Color = Color.White
            };
            loadingcards = [
                Content.Load<Texture2D>("TEXTURES/LOAD_UI/loadingCards_0"),
                Content.Load<Texture2D>("TEXTURES/LOAD_UI/loadingCards_1"),
                Content.Load<Texture2D>("TEXTURES/LOAD_UI/loadingCards_10"),
                Content.Load<Texture2D>("TEXTURES/LOAD_UI/loadingCards_11"),
                Content.Load<Texture2D>("TEXTURES/LOAD_UI/loadingCards_12"),
                Content.Load<Texture2D>("TEXTURES/LOAD_UI/loadingCards_13"),
                Content.Load<Texture2D>("TEXTURES/LOAD_UI/loadingCards_14"),
                Content.Load<Texture2D>("TEXTURES/LOAD_UI/loadingCards_15"),
                Content.Load<Texture2D>("TEXTURES/LOAD_UI/loadingCards_16"),
                Content.Load<Texture2D>("TEXTURES/LOAD_UI/loadingCards_17"),
                Content.Load<Texture2D>("TEXTURES/LOAD_UI/loadingCards_18"),
                Content.Load<Texture2D>("TEXTURES/LOAD_UI/loadingCards_19"),
                Content.Load<Texture2D>("TEXTURES/LOAD_UI/loadingCards_2"),
                Content.Load<Texture2D>("TEXTURES/LOAD_UI/loadingCards_20"),
                Content.Load<Texture2D>("TEXTURES/LOAD_UI/loadingCards_21"),
                Content.Load<Texture2D>("TEXTURES/LOAD_UI/loadingCards_22"),
                Content.Load<Texture2D>("TEXTURES/LOAD_UI/loadingCards_23"),
                Content.Load<Texture2D>("TEXTURES/LOAD_UI/loadingCards_24"),
                Content.Load<Texture2D>("TEXTURES/LOAD_UI/loadingCards_25"),
                Content.Load<Texture2D>("TEXTURES/LOAD_UI/loadingCards_26"),
                Content.Load<Texture2D>("TEXTURES/LOAD_UI/loadingCards_27"),
                Content.Load<Texture2D>("TEXTURES/LOAD_UI/loadingCards_28"),
                Content.Load<Texture2D>("TEXTURES/LOAD_UI/loadingCards_29"),
                Content.Load<Texture2D>("TEXTURES/LOAD_UI/loadingCards_3"),
                Content.Load<Texture2D>("TEXTURES/LOAD_UI/loadingCards_30"),
                Content.Load<Texture2D>("TEXTURES/LOAD_UI/loadingCards_31"),
                Content.Load<Texture2D>("TEXTURES/LOAD_UI/loadingCards_32"),
                Content.Load<Texture2D>("TEXTURES/LOAD_UI/loadingCards_33"),
                Content.Load<Texture2D>("TEXTURES/LOAD_UI/loadingCards_34"),
                Content.Load<Texture2D>("TEXTURES/LOAD_UI/loadingCards_35"),
                Content.Load<Texture2D>("TEXTURES/LOAD_UI/loadingCards_36"),
                Content.Load<Texture2D>("TEXTURES/LOAD_UI/loadingCards_37"),
                Content.Load<Texture2D>("TEXTURES/LOAD_UI/loadingCards_38"),
                Content.Load<Texture2D>("TEXTURES/LOAD_UI/loadingCards_39"),
                Content.Load<Texture2D>("TEXTURES/LOAD_UI/loadingCards_4"),
                Content.Load<Texture2D>("TEXTURES/LOAD_UI/loadingCards_40"),
                Content.Load<Texture2D>("TEXTURES/LOAD_UI/loadingCards_41"),
                Content.Load<Texture2D>("TEXTURES/LOAD_UI/loadingCards_42"),
                Content.Load<Texture2D>("TEXTURES/LOAD_UI/loadingCards_43"),
                Content.Load<Texture2D>("TEXTURES/LOAD_UI/loadingCards_44"),
                Content.Load<Texture2D>("TEXTURES/LOAD_UI/loadingCards_45"),
                Content.Load<Texture2D>("TEXTURES/LOAD_UI/loadingCards_46"),
                Content.Load<Texture2D>("TEXTURES/LOAD_UI/loadingCards_47"),
                Content.Load<Texture2D>("TEXTURES/LOAD_UI/loadingCards_48"),
                Content.Load<Texture2D>("TEXTURES/LOAD_UI/loadingCards_49"),
                Content.Load<Texture2D>("TEXTURES/LOAD_UI/loadingCards_5"),
                Content.Load<Texture2D>("TEXTURES/LOAD_UI/loadingCards_50"),
                Content.Load<Texture2D>("TEXTURES/LOAD_UI/loadingCards_51"),
                Content.Load<Texture2D>("TEXTURES/LOAD_UI/loadingCards_52"),
                Content.Load<Texture2D>("TEXTURES/LOAD_UI/loadingCards_53"),
                Content.Load<Texture2D>("TEXTURES/LOAD_UI/loadingCards_6"),
                Content.Load<Texture2D>("TEXTURES/LOAD_UI/loadingCards_7"),
                Content.Load<Texture2D>("TEXTURES/LOAD_UI/loadingCards_8"),
                Content.Load<Texture2D>("TEXTURES/LOAD_UI/loadingCards_9")];
        }

        public void Update(GameTime gameTime)
        {
            if (gameTime.TotalGameTime - lastcardspawn > TimeSpan.FromMilliseconds(cardlifespan.TotalMilliseconds / 12) && AddNewCards) {
                lastcardspawn = gameTime.TotalGameTime;
                double angle = Math.PI / 2;
                Point location = new(middlepoint.X + radius, DisplayInfo.ScreenHeight);
                cards.Add(new(location, angle, TimeSpan.Zero,
                    loadingcards[Random.Shared.Next(loadingcards.Length)]));
            }
            if (cards.Count == 0 && !AddNewCards) {
                Finished = true;
                return;
            }
            for (int i = cards.Count - 1; i >= 0; i--) {
                Tuple<Point, double, TimeSpan, Texture2D> card = cards[i];
                TimeSpan newlifespan = card.Item3 + gameTime.ElapsedGameTime;
                if (newlifespan >= cardlifespan) {
                    cards.RemoveAt(i);
                    continue;
                }
                double progress = newlifespan.TotalMilliseconds / cardlifespan.TotalMilliseconds;
                if (progress < 1.0 / 4) {
                    double prog = progress / (1.0 / 4);
                    double angle = Math.PI / 2;
                    Point location = new(card.Item1.X,
                        (int)Math.Round(DisplayInfo.ScreenHeight - ((DisplayInfo.ScreenHeight - middlepoint.Y) * prog)));
                    cards[i] = new(location, angle, newlifespan, card.Item4);
                }
                else if (progress < 3.0 / 4) {
                    double prog = (progress - (1.0 / 4)) / (1.0 / 2);
                    double locationangle = (Math.PI * (1 - prog)) + Math.PI;
                    double angle = locationangle - (3.0 / 2 * Math.PI) - (2 * Math.Asin(cardX / (2 * radius)));
                    Point location = new(
                        (int)Math.Round(middlepoint.X + (radius * Math.Cos(locationangle))),
                        (int)Math.Round(middlepoint.Y + (radius * Math.Sin(locationangle))));
                    cards[i] = new(location, angle, newlifespan, card.Item4);
                }
                else {
                    double prog = (progress - (3.0 / 4)) / (1.0 / 4);
                    double angle = 3.0 / 2 * Math.PI;
                    Point location = new(card.Item1.X,
                        (int)Math.Round(middlepoint.Y + ((DisplayInfo.ScreenHeight - middlepoint.Y + cardSize.X) * prog)));
                    cards[i] = new(location, angle, newlifespan, card.Item4);
                }
            }
            percentageBox.Text = Percentage.ToString("P2");
            percentageBox.Update(gameTime);
        }

        public void Draw(GameTime gameTime, SpriteBatch spriteBatch)
        {
            foreach (var card in cards) {
                float rotation = (float)card.Item2;
                Vector2 position = new(card.Item1.X, card.Item1.Y);
                Vector2 originVec = new(origin.X, origin.Y);
                spriteBatch.Draw(card.Item4, position, null, Color.White, rotation,
                    originVec, 1.0f, SpriteEffects.None, 0.0f);
            }
            percentageBox.Draw(gameTime, spriteBatch);
        }

        public void Dispose()
        {
            foreach (var tex in loadingcards) {
                tex.Dispose();
            }
            cards.Clear();
            fontSystem.Dispose();
        }

    }
}
