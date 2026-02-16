using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

#nullable enable
namespace CardGame {
    internal class Slider : IDrawable {
        private readonly Texture2D textureSlider;
        private readonly Texture2D textureBall;
        private readonly MouseInfo mouseInfo;
        private bool isHovered;
        private bool isDragging;
        private Point ballLocation = Point.Zero;
        private Point size = new(192, 32);
        private Point ballSize;
        private float _value = 1.0f;

        public Point Location { get; set; } = Point.Zero;
        public Point Size
        {
            get => size;
            set {
                size = value;
                ballSize = new Point(value.Y + 10);
            }
        }
        public event EventHandler? OnChange;
        public float Value
        {
            get => _value;
            set => _value = Math.Clamp(value, 0f, 1f);
        }
        public bool IsVertical { get; set; } = false;

        public Slider(MouseInfo mouseInfo)
        {
            this.mouseInfo = mouseInfo;
            textureSlider = ResourceManager.Textures["Slider"][0];
            textureBall = ResourceManager.Textures["Slider"][1];
            Size = new Point(textureSlider.Width, textureSlider.Height);
        }

        public void Update(GameTime gameTime)
        {
            Point mousePos = mouseInfo.GetMousePosition();

            if (isDragging) {
                int half = ballSize.X / 2;
                int newCoord;
                if (IsVertical) {
                    newCoord = Math.Clamp(mousePos.Y - half, Location.Y, Location.Y + Size.X - ballSize.X);
                    ballLocation = new Point(Location.X - 5, newCoord);
                    Value = (float)(newCoord - Location.Y) / (Size.X - ballSize.X);
                }
                else {
                    // Az X koordinátát clamp-ezzuk a megfelelő tartományban.
                    newCoord = Math.Clamp(mousePos.X - half, Location.X, Location.X + Size.X - ballSize.X);
                    ballLocation = new Point(newCoord, Location.Y - 5);
                    Value = (float)(newCoord - Location.X) / (Size.X - ballSize.X);
                }
                if (mouseInfo.Current.LeftButton == ButtonState.Released) {
                    isDragging = false;
                    OnChange?.Invoke(this, EventArgs.Empty);
                }
            }
            else {
                Rectangle ballRect = new(ballLocation, ballSize);
                isHovered = ballRect.Contains(mousePos);

                if (isHovered && mouseInfo.Current.LeftButton == ButtonState.Pressed && mouseInfo.Previous.LeftButton == ButtonState.Released) {
                    isDragging = true;
                }

                if (IsVertical) {
                    int newY = (int)(Location.Y + (Value * (Size.X - ballSize.X)));
                    ballLocation = new Point(Location.X - 5, newY);
                }
                else {
                    int newX = (int)(Location.X + (Value * (Size.X - ballSize.X)));
                    ballLocation = new Point(newX, Location.Y - 5);
                }
            }
        }

        public void Draw(GameTime gameTime, SpriteBatch spriteBatch)
        {
            if (IsVertical) {
                spriteBatch.Draw(
                    textureSlider,
                    new Rectangle(new(Location.X + size.Y, Location.Y), size),
                    null,
                    Color.Gray,
                    MathHelper.ToRadians(90.0f),
                    Vector2.Zero,
                    SpriteEffects.None,
                    0f
                );
            }
            else {
                spriteBatch.Draw(textureSlider, new Rectangle(Location, size), Color.Gray);
            }

            Color ballColor = isHovered ? Color.DarkCyan : Color.White;
            spriteBatch.Draw(textureBall, new Rectangle(ballLocation, ballSize), ballColor);
        }
    }
}
