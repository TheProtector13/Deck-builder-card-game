using System;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

#nullable enable
namespace CardGame {
    internal class EndingScreen : IDrawable {
        private Texture2D texture;
        private Texture2D? logo;
        private Rectangle logo_Rect;
        private TextBox textBox;

        public string Title
        {
            get => textBox.Text;
            set => textBox.Text = value;
        }

        private EndingScreen() => throw new NotImplementedException();
        public EndingScreen(Texture2D texture, Texture2D? logo, FontSystem font)
        {
            this.texture = texture;
            this.logo = logo;
            int logo_Width = DisplayInfo.GetPXfromHeight(0.189814815);
            int logoCenterY = DisplayInfo.GetPXfromHeight(0.275);
            int logoCenterX = DisplayInfo.ScreenWidth / 2;
            logo_Rect = new(logoCenterX - (logo_Width / 2), logoCenterY - (logo_Width / 2), logo_Width, logo_Width);
            int TboxX = DisplayInfo.GetPXfromWidth(0.346875);
            int TboxY = DisplayInfo.GetPXfromHeight(0.435185185);
            int TboxW = DisplayInfo.GetPXfromWidth(0.307291667);
            int TboxH = DisplayInfo.GetPXfromHeight(0.141666667);
            textBox = new(new(TboxX, TboxY, TboxW, TboxH), font);
        }

        public void Update(GameTime gameTime)
        {
            textBox.Update(gameTime);
        }

        public void Draw(GameTime gameTime, SpriteBatch spriteBatch)
        {
            spriteBatch.Draw(texture, DisplayInfo.ScreenRect, Color.White);
            if (logo != null)
                spriteBatch.Draw(logo, logo_Rect, Color.White);
            textBox.Draw(gameTime, spriteBatch);
        }
    }
}
