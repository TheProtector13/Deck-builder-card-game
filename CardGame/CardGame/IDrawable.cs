using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CardGame {
    internal interface IDrawable {
        public void Update(GameTime gameTime);
        public void Draw(GameTime gameTime, SpriteBatch spriteBatch);
    }
}
