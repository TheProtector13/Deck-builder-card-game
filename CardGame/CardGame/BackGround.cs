using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CardGame {
    internal class BackGround : IDrawable {
        public BackGroundType Type { get; init; }
        private readonly Texture2D[] planet;
        private readonly Tuple<Texture2D, bool>[] objects;
        private readonly Texture2D background;
        private readonly Rectangle planetLoc;
        private readonly Tuple<Rectangle, float>[] objLoc;
        private readonly Rectangle bgRect;
        private TimeSpan lastFrameChange = TimeSpan.Zero;
        private TimeSpan frameTime = TimeSpan.FromSeconds(1d / 25d);
        private int currentFrame = 0;
        private static readonly float rotRad = MathHelper.ToRadians(0.05f);

        public enum BackGroundType {
            Forest,
            Ice,
            Desert
        }

        public BackGround(BackGroundType? planetType = null)
        {
            Type = planetType == null ? (BackGroundType)Random.Shared.Next(0, 3) : (BackGroundType)planetType;
            planet = Type switch {
                BackGroundType.Forest => planet = ResourceManager.Textures["ForestPlanet"],
                BackGroundType.Ice => planet = ResourceManager.Textures["IcePlanet"],
                BackGroundType.Desert => planet = ResourceManager.Textures["DesertPlanet"],
                _ => throw new ArgumentOutOfRangeException()
            };
            byte objCount = (byte)Random.Shared.Next(5, 9);
            Texture2D[] objs = ResourceManager.Textures["OBJ"];
            List<Tuple<Texture2D, bool>> objList = [];
            for (int i = 0; i < objCount; i++) {
                byte num = Random.Shared.Next(0, 5) < 2 ? (byte)0 : (byte)Random.Shared.Next(1, objs.Length);
                bool rotate = num == 0 || num == 4 ? true : false;
                objList.Add(new(objs[num], rotate));
            }
            objects = objList.ToArray();
            background = ResourceManager.Textures["Space"][Random.Shared.Next(0, 5)];
            int pheight = DisplayInfo.GetPXfromHeight(0.71111111);
            planetLoc = new Rectangle(
                DisplayInfo.ScreenWidth - (int)(pheight * 0.73828125),
                DisplayInfo.GetPXfromHeight(0.03055555),
                pheight,
                pheight
                );
            List<Tuple<Rectangle, float>> objlocs = [];
            for (int i = 0; i < objects.Length; i++) {
                Rectangle rect;
                int size = DisplayInfo.GetPXfromHeight((Random.Shared.NextDouble() * 0.08) + 0.03);
                int x = Random.Shared.Next(0, DisplayInfo.ScreenWidth - size);
                int y = Random.Shared.Next(0, DisplayInfo.ScreenHeight - size);
                float rot = (float)Random.Shared.NextDouble() * MathHelper.TwoPi;
                rect = new Rectangle(x, y, size, size);
                while (rect.Intersects(planetLoc) || objlocs.Any((element) => rect.Intersects(element.Item1))) {
                    x = Random.Shared.Next(0, DisplayInfo.ScreenWidth - size);
                    y = Random.Shared.Next(0, DisplayInfo.ScreenHeight - size);
                    rect = new Rectangle(x, y, size, size);
                }
                objlocs.Add(new(rect, rot));
            }
            objLoc = objlocs.ToArray();
            bgRect = DisplayInfo.FillRect(new Rectangle(0, 0, background.Width, background.Height));
        }

        public void Update(GameTime gameTime)
        {
            if (gameTime.TotalGameTime - lastFrameChange > frameTime) {
                lastFrameChange = gameTime.TotalGameTime;
                currentFrame++;
                if (currentFrame >= planet.Length) {
                    currentFrame = 0;
                }
            }
            for (int i = 0; i < objLoc.Length; i++) {
                if (objects[i].Item2) {
                    var (rect, rot) = objLoc[i];
                    rot += rotRad;
                    if (rot > MathHelper.TwoPi) {
                        rot -= MathHelper.TwoPi;
                    }
                    objLoc[i] = new(rect, rot);
                }
            }
        }

        public void Draw(GameTime gameTime, SpriteBatch spriteBatch)
        {
            spriteBatch.Draw(background, DisplayInfo.ScreenRect, bgRect, Color.White);
            for (int i = 0; i < objects.Length; i++) {
                var (rect, rot) = objLoc[i];
                Texture2D tex = objects[i].Item1;
                spriteBatch.Draw(tex, rect, null, Color.White, rot, new Vector2(tex.Width / 2f, tex.Height / 2f), SpriteEffects.None, 0f);
            }
            spriteBatch.Draw(planet[currentFrame], planetLoc, Color.White);
        }
    }
}
