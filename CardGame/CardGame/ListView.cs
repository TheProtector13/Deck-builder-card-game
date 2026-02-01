using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

#nullable enable
namespace CardGame {
    internal class ListView : IDrawable {
        private Rectangle _rect;
        private readonly List<Tuple<ListOption, object>> options = [];
        private readonly Slider slider;
        private readonly MouseInfo mouseInfo;
        private int optionHeight;

        public Tuple<string, object>? Selected { get; private set; } = null;

        public void ResetSelected() => Selected = null;

        public Rectangle Rect
        {
            get => _rect;
            set {
                if (_rect == value) return;
                _rect = value;
                slider.Location = new(_rect.Right, _rect.Y);
                slider.Size = new(_rect.Height, 32);
                optionHeight = (int)MathF.Round(_rect.Height / 4f);
            }
        }

        public ListView(Rectangle rect, MouseInfo mouseInfo)
        {
            _rect = rect;
            this.mouseInfo = mouseInfo;
            slider = new(mouseInfo) {
                Location = new(rect.Right, rect.Y),
                Size = new(rect.Height, 32),
                IsVertical = true,
                Value = 0.0f
            };
            optionHeight = (int)MathF.Round(_rect.Height / 4f);
        }

        private void SelectionEventHandler(object? sender, EventArgs e)
        {
            if (Selected is not null) return;
            if (sender is not ListOption listOpt) return;
            var pair = options.Find(opt => ReferenceEquals(opt.Item1, listOpt));
            if (pair == null) return;
            Selected = new Tuple<string, object>(pair.Item1.Text, pair.Item2);
        }

        public void AddOrUpdateOption(string str, object obj)
        {
            foreach (Tuple<ListOption, object> option in options) {
                if (option.Item2 == obj) {
                    option.Item1.Text = str;
                    return;
                }
            }
            ListOption opt = new(new(_rect.X, _rect.Y, _rect.Width, optionHeight), mouseInfo, str);
            opt.OnSelect += SelectionEventHandler;
            options.Add(new(opt, obj));
        }

        public void RemoveOption(string str, object obj)
        {
            for (int i = 0; i < options.Count; i++) {
                if (options[i].Item2 == obj && options[i].Item1.Text == str) {
                    options[i].Item1.OnSelect -= SelectionEventHandler;
                    options.RemoveAt(i);
                    break;
                }
            }
        }

        public void ClearOptions()
        {
            foreach (var opt in options) {
                opt.Item1.OnSelect -= SelectionEventHandler;
            }
            options.Clear();
        }

        public void ReplaceOptions(Tuple<string, object>[] newoptions)
        {
            foreach (var opt in options) {
                opt.Item1.OnSelect -= SelectionEventHandler;
            }
            options.Clear();
            foreach ((string str, object obj) in newoptions) {
                ListOption opt = new(new(_rect.X, _rect.Y, _rect.Width, optionHeight), mouseInfo, str);
                opt.OnSelect += SelectionEventHandler;
                options.Add(new(opt, obj));
            }
        }

        public void Update(GameTime gameTime)
        {
            slider.Update(gameTime);
            int offset = (int)MathF.Round(optionHeight * (options.Count - 1) * slider.Value);
            for (int i = 0; i < options.Count; i++) {
                options[i].Item1.Rect = new(
                    _rect.X, _rect.Y + (optionHeight * i) - offset,
                    _rect.Width, optionHeight);
                options[i].Item1.Update(gameTime);
            }
        }

        public void Draw(GameTime gameTime, SpriteBatch spriteBatch)
        {
            slider.Draw(gameTime, spriteBatch);
            foreach (var item in options) {
                if (_rect.Contains(item.Item1.Rect))
                    item.Item1.Draw(gameTime, spriteBatch);
            }
        }

        //
        private class ListOption : IDrawable {
            private readonly TextBox textbox;
            private readonly Button selectbutton;
            private Rectangle _rect;
            public event EventHandler? OnSelect;

            public Rectangle Rect
            {
                get => _rect;
                set {
                    if (value == _rect) return;
                    _rect = value;
                    selectbutton.Location = new(_rect.Right - selectbutton.Size.X, _rect.Y);
                    float ratio = (float)selectbutton.Size.X / selectbutton.Size.Y;
                    Point buttonSize = new((int)MathF.Round(_rect.Height * ratio), _rect.Height);
                    selectbutton.Size = buttonSize;
                    textbox.Rect = new(_rect.X, _rect.Y, _rect.Width - selectbutton.Size.X, _rect.Height);
                }
            }

            public string Text
            {
                get => textbox.Text;
                set => textbox.Text = value;
            }

            public void SelectEvent(object? sender, EventArgs e) => OnSelect?.Invoke(this, e);

            public ListOption(Rectangle rect, MouseInfo mouseInfo, string text)
            {
                _rect = rect;
                selectbutton = new([ResourceManager.Textures["SelectButton"][0], ResourceManager.Textures["SelectButton"][1]], mouseInfo) {
                    Text = "Kiválaszt"
                };
                selectbutton.Click += SelectEvent;
                float ratio = (float)selectbutton.Size.X / selectbutton.Size.Y;
                Point buttonSize = new((int)MathF.Round(rect.Height * ratio), rect.Height);
                selectbutton.Location = new(_rect.Right - buttonSize.X, _rect.Y);
                selectbutton.Size = buttonSize;
                textbox = new TextBox(new(rect.X, rect.Y, rect.Width - buttonSize.X, rect.Height), ResourceManager.Fonts["FONT_DEF_B"]) {
                    Text = text,
                    Alignment = TextBox.TextAlignment.Left
                };
            }

            public void Update(GameTime gameTime)
            {
                textbox.Update(gameTime);
                selectbutton.Update(gameTime);
            }

            public void Draw(GameTime gameTime, SpriteBatch spriteBatch)
            {
                textbox.Draw(gameTime, spriteBatch);
                selectbutton.Draw(gameTime, spriteBatch);
            }
        }
    }
}
