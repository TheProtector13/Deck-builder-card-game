using System.Drawing;
using Microsoft.Xna.Framework.Graphics;

namespace CardGame {
    internal class MainMenu : IDrawable {
        private readonly Texture2D backgroundTexture;
        private readonly Rectangle backgroundRectangle;
        private readonly Texture2D menubackground;
        private readonly Rectangle menubackgroundRectangle;
        private readonly Button[] mainmenubuttons;
        private readonly Button[] settingsmenubuttons;
        private readonly TextBox menuTitle;
        private readonly TextBox[] settingsmenuLabels;

        public void Draw(Microsoft.Xna.Framework.GameTime gameTime, SpriteBatch spriteBatch) => throw new System.NotImplementedException();
        public void Update(Microsoft.Xna.Framework.GameTime gameTime) => throw new System.NotImplementedException();
    }
}
