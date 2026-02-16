using System;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

#nullable enable
namespace CardGame {
    internal class TextInput(Rectangle rect, FontSystem fontSystem, MouseInfo mouseInfo, int maxLength = 8) : IDrawable {
        private static object _threadLock = new();
        private const float minFontSize = 2f;
        private float _sizeoffset = 0f;
        private float _verticalsizeoffset = 2f;
        private readonly FontSystem _fontSystem = fontSystem ?? throw new ArgumentNullException(nameof(fontSystem));

        private Rectangle _rect = rect;
        private DynamicSpriteFont? _font;
        private string _inputText = string.Empty;
        private bool _changed = true;

        public bool IsFocused { get; private set; } = false;
        private KeyboardState _previousKeyboardState = Keyboard.GetState();
        private readonly MouseInfo mouseinfo = mouseInfo ?? throw new ArgumentNullException(nameof(mouseInfo));

        private double _cursorBlinkTimer = 0.0;
        private bool _showCursor = true;

        public float SizeOffset
        {
            get => _sizeoffset;
            set {
                if (MathF.Abs(_sizeoffset - value) < 0.1f) return;
                _sizeoffset = value;
                _changed = true;
            }
        }

        public float VerticalSizeOffset
        {
            get => _verticalsizeoffset;
            set {
                if (MathF.Abs(_verticalsizeoffset - value) < 0.1f) return;
                _verticalsizeoffset = value;
                _changed = true;
            }
        }

        public Color BGColor { get; set; } = Color.Transparent;
        public Color TextColor { get; set; } = Color.Black;
        public Color FocusedBGOverride { get; set; } = new Color(250, 245, 200);
        public int MaxLength { get; set; } = Math.Max(1, maxLength);
        public float? ForcedFontSize { get; set; } = null;

        public event EventHandler? OnChange;

        public Rectangle Rect
        {
            get => _rect;
            set {
                if (_rect == value) return;
                _rect = value;
                _changed = true;
            }
        }

        public string Text
        {
            get => _inputText;
            set {
                if (value == _inputText) return;
                _inputText = value ?? string.Empty;
                if (_inputText.Length > MaxLength) _inputText = _inputText.Substring(0, MaxLength);
                _changed = true;
            }
        }

        private void Commit()
        {
            OnChange?.Invoke(this, EventArgs.Empty);
            _changed = true;
        }

        private static bool TryKeyToChar(Keys key, bool shift, out char c)
        {
            c = '\0';
            if (key >= Keys.A && key <= Keys.Z) {
                int offset = key - Keys.A;
                char baseChar = (char)('a' + offset);
                c = shift ? char.ToUpperInvariant(baseChar) : baseChar;
                return true;
            }

            if (key >= Keys.D0 && key <= Keys.D9) {
                int n = key - Keys.D0;
                if (shift) {
                    switch (n) {
                        case 1: c = '!'; return true;
                        case 2: c = '@'; return true;
                        case 3: c = '#'; return true;
                        case 4: c = '$'; return true;
                        case 5: c = '%'; return true;
                        case 6: c = '^'; return true;
                        case 7: c = '&'; return true;
                        case 8: c = '*'; return true;
                        case 9: c = '('; return true;
                        case 0: c = ')'; return true;
                    }
                }
                c = (char)('0' + n);
                return true;
            }

            if (key >= Keys.NumPad0 && key <= Keys.NumPad9) {
                int n = key - Keys.NumPad0;
                c = (char)('0' + n);
                return true;
            }

            if (key == Keys.Space) { c = ' '; return true; }

            switch (key) {
                case Keys.OemMinus: c = shift ? '_' : '-'; return true;
                case Keys.OemPlus: c = shift ? '+' : '='; return true;
                case Keys.OemComma: c = shift ? '<' : ','; return true;
                case Keys.OemPeriod: c = shift ? '>' : '.'; return true;
                case Keys.OemQuestion: c = shift ? '?' : '/'; return true;
                case Keys.OemSemicolon: c = shift ? ':' : ';'; return true;
                case Keys.OemQuotes: c = shift ? '"' : '\''; return true;
                case Keys.OemOpenBrackets: c = shift ? '{' : '['; return true;
                case Keys.OemCloseBrackets: c = shift ? '}' : ']'; return true;
                case Keys.OemPipe: c = shift ? '|' : '\\'; return true;
                case Keys.OemTilde: c = shift ? '~' : '`'; return true;
                case Keys.OemBackslash: c = shift ? '|' : '\\'; return true;
            }

            return false;
        }

        private void RecalculateFontAndLayout()
        {
            if (ForcedFontSize.HasValue) {
                float f = MathF.Max(minFontSize, ForcedFontSize.Value);
                lock (_threadLock) {
                    _font = _fontSystem.GetFont(f);
                }
                return;
            }

            float low = minFontSize;
            float high = MathF.Max(low, _rect.Height);
            float best = low;
            float prevBest = 0f;

            if (MathF.Abs(low - high) > 0.3f) {
                while (MathF.Abs(best - prevBest) > 0.3f && low != high) {
                    float mid = MathF.Floor((low + high) / 2f * 10) / 10;
                    DynamicSpriteFont font; Vector2 m;
                    lock (_threadLock) {
                        font = _fontSystem.GetFont(mid);
                        m = font.MeasureString("Aj");
                    }
                    m = new Vector2(m.X + _sizeoffset, m.Y + _verticalsizeoffset);
                    float lineHeight = m.Y;
                    bool fitsHeight = lineHeight <= _rect.Height;
                    bool fitsWidth = true;
                    if (fitsHeight) {
                        lock (_threadLock) {
                            m = font.MeasureString(_inputText);
                        }
                        m = new Vector2(m.X + _sizeoffset, m.Y + _verticalsizeoffset);
                        if (m.X > _rect.Width) {
                            fitsWidth = false;
                        }
                    }

                    if (fitsHeight && fitsWidth) {
                        prevBest = best;
                        best = mid;
                        low = mid;
                    }
                    else {
                        high = mid;
                    }
                }
            }
            lock (_threadLock) {
                _font = _fontSystem.GetFont(best);
            }
        }

        public void Update(GameTime gameTime)
        {
            var keyboard = Keyboard.GetState();

            bool leftPressedNow = mouseinfo.Current.LeftButton == ButtonState.Pressed;
            bool leftPressedPrev = mouseinfo.Previous.LeftButton == ButtonState.Pressed;

            bool mouseOver = _rect.Contains(mouseinfo.GetMousePosition());

            if (leftPressedNow && !leftPressedPrev) {
                if (!IsFocused && mouseOver) {
                    IsFocused = true;
                    _showCursor = true;
                    _cursorBlinkTimer = 0.0;
                }
                else if (IsFocused) {
                    Commit();
                    IsFocused = false;
                }
            }

            if (!mouseOver && leftPressedNow && !leftPressedPrev && IsFocused) {
                Commit();
                IsFocused = false;
            }

            if (IsFocused) {
                double dt = gameTime.ElapsedGameTime.TotalSeconds;
                _cursorBlinkTimer += dt;
                if (_cursorBlinkTimer >= 0.5) {
                    _showCursor = !_showCursor;
                    _cursorBlinkTimer = 0.0;
                }

                foreach (Keys key in keyboard.GetPressedKeys()) {
                    if (!_previousKeyboardState.IsKeyDown(key)) {
                        if (key == Keys.Back) {
                            if (_inputText.Length > 0) {
                                _inputText = _inputText.Substring(0, _inputText.Length - 1);
                                _changed = true;
                            }
                        }
                        else if (key == Keys.Enter) {
                            Commit();
                            IsFocused = false;
                        }
                        else {
                            bool shift = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);
                            if (TryKeyToChar(key, shift, out char cc)) {
                                if (_inputText.Length < MaxLength) {
                                    _inputText += cc;
                                    _changed = true;
                                }
                            }
                        }
                    }
                }

                _previousKeyboardState = keyboard;
            }
            else {
                _showCursor = false;
                _previousKeyboardState = keyboard;
            }

            if (_changed) {
                RecalculateFontAndLayout();
            }
        }

        public void Draw(GameTime gameTime, SpriteBatch spriteBatch)
        {
            if (_font == null) return;

            Color bg = IsFocused ? FocusedBGOverride : BGColor;
            if (bg.A != 0) {
                Texture2D box = ResourceManager.GetColor(bg, spriteBatch);
                spriteBatch.Draw(box, _rect, Color.White);
            }

            string display = _inputText + (IsFocused && _showCursor ? "|" : "");
            Vector2 measured = _font.MeasureString(display);
            Vector2 pos = new(_rect.X + MathF.Max(2, (_rect.Width - measured.X) / 2f),
                              _rect.Y + MathF.Max(0, (_rect.Height - _font.MeasureString("Aj").Y) / 2f));

            _font.DrawText(spriteBatch, display, pos, TextColor, effect: FontSystemEffect.None);
        }
    }
}
