using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

#nullable enable
namespace CardGame {
    internal class Button : IDrawable {
        private readonly Texture2D[] textures;
        private readonly TextBox textbox;
        private Vector4 textoffset = Vector4.Zero;
        private readonly MouseInfo mouseInfo;
        private bool IsHovered = false;
        public Point Location { get; set; } = Point.Zero;
        public Point Size { get; set; } = new(128, 128);
        public event EventHandler? Click;
        public bool IsClicked { get; private set; } = false;
        public bool Enabled { get; set; } = true;
        public string Text { get; set; } = string.Empty;
        public Color PenColour { get; set; } = Color.Black;
        public Vector4 TextOffset => textoffset;
        public float SetTextOffsetX { set => textoffset.X = value; }
        public float SetTextOffsetY { set => textoffset.Y = value; }
        public float SetTextOffsetZ { set => textoffset.Z = value; }
        public float SetTextOffsetW { set => textoffset.W = value; }

        private Button() => throw new NotImplementedException();
        public Button(Texture2D[] textures, MouseInfo mouseInfo)
        {
            if (textures.Length != 2) {
                throw new ArgumentException("Button requires exactly 2 textures: normal and hovered.");
            }
            this.textures = textures;
            this.textbox = new(Rectangle.Empty, ResourceManager.Fonts["FONT_DEF_B"]);
            this.mouseInfo = mouseInfo;
            this.Size = new Point(textures[0].Width, textures[0].Height);
            textbox.Rect = new Rectangle(Location, Size);
            textbox.SizeOffset = Size.X * 0.2f;
            textbox.VerticalSizeOffset = Size.Y * 0.2f;
        }

        public void Update(GameTime gameTime)
        {
            if (Enabled) {
                if (!string.IsNullOrEmpty(Text)) {
                    textbox.Text = Text;
                    textbox.Color = PenColour;
                    textbox.Rect = new Rectangle(Location.X + (int)TextOffset.X, Location.Y + (int)TextOffset.Y, Size.X - (int)(TextOffset.Z + TextOffset.X), Size.Y - (int)(TextOffset.W + TextOffset.Y));
                    textbox.Update(gameTime);
                }
                Rectangle bounds = new(Location, Size);
                if (bounds.Contains(mouseInfo.GetMousePosition())) {
                    IsHovered = true;
                    if (mouseInfo.Current.LeftButton == ButtonState.Pressed && mouseInfo.Previous.LeftButton == ButtonState.Released) {
                        Click?.Invoke(this, EventArgs.Empty);
                        IsClicked = true;
                    }
                }
                else {
                    IsHovered = false;
                }
            }
            else {
                IsHovered = false;
            }
        }

        public void Draw(GameTime gameTime, SpriteBatch spriteBatch)
        {
            if (IsHovered) {
                spriteBatch.Draw(textures[1], new Rectangle(Location, Size), Color.White);
            }
            else {
                spriteBatch.Draw(textures[0], new Rectangle(Location, Size), Color.White);
            }

            if (!string.IsNullOrEmpty(Text)) {
                textbox.Draw(gameTime, spriteBatch);
            }
        }
    }
}
