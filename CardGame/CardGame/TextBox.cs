using System;
using System.Collections.Generic;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

#nullable enable
namespace CardGame {
    internal class TextBox(Rectangle rect, FontSystem fontSystem) : IDrawable {
        private const float minFontSize = 2f;
        private readonly FontSystem _fontSystem = fontSystem ?? throw new ArgumentNullException(nameof(fontSystem));
        private Vector2[] _positions = [];
        private List<string> _lines = [];
        private DynamicSpriteFont? _font;
        private bool _changed = true;
        private bool _locChanged = false;
        private Rectangle _rect = rect;
        private float _sizeoffset = 0f;
        private float _verticalsizeoffset = 2f;
        private string _text = string.Empty;
        private Color _color = Color.Black;
        private TextAlignment _alignment = TextAlignment.Center;
        private int _strokeSize = 0;
        private float? _forcedFontSize = null;

        public enum TextAlignment {
            Left,
            Center,
            Right
        }

        public Rectangle Rect
        {
            get => _rect;
            set {
                if (value == _rect) return;
                if (_rect.Width == value.Width && _rect.Height == value.Height) {
                    _locChanged = true;
                }
                else {
                    _changed = true;
                }
                _rect = value;
            }
        }

        public string Text
        {
            get => _text;
            set {
                if (value == _text) return;
                _text = value;
                _changed = true;
            }
        }

        public Color Color
        {
            get => _color;
            set {
                if (value == _color) return;
                _color = value;
            }
        }

        public TextAlignment Alignment
        {
            get => _alignment;
            set {
                if (value == _alignment) return;
                _alignment = value;
                _locChanged = true;
            }
        }

        public int StrokeSize
        {
            get => _strokeSize;
            set {
                var clamped = Math.Max(0, value);
                if (clamped == _strokeSize) return;
                _strokeSize = clamped;
            }
        }

        public float? ForcedFontSize
        {
            get => _forcedFontSize;
            set {
                if (_forcedFontSize == value) return;
                _forcedFontSize = value;
                _changed = true;
            }
        }

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
        public bool VerticalCentering { get; set; } = true;

        private void RecalculateFontAndLayout()
        {
            _lines = [.. _text.Replace("\r", "").Split('\n')];
            if (_lines.Count == 0) _lines.Add(string.Empty);
            if (_lines.Count != 1 && _lines[^1] == string.Empty) {
                _lines.RemoveAt(_lines.Count - 1);
            }

            if (_forcedFontSize.HasValue) {
                float forced = MathF.Max(minFontSize, _forcedFontSize.Value);
                _font = _fontSystem.GetFont(forced);
                return;
            }

            float low = minFontSize;
            float high = MathF.Max(low, _rect.Height);
            float best = low;
            float prevBest = 0f;

            if (MathF.Abs(low - high) > 0.3f) {
                while (MathF.Abs(best - prevBest) > 0.3f && low != high) {
                    float mid = MathF.Floor((low + high) / 2f * 10) / 10;
                    var font = _fontSystem.GetFont(mid);
                    Vector2 m = font.MeasureString("Aj");
                    m = new Vector2(m.X + _sizeoffset, m.Y + _verticalsizeoffset);
                    float lineHeight = m.Y;
                    bool fitsHeight = lineHeight * _lines.Count <= _rect.Height;
                    bool fitsWidth = true;
                    if (fitsHeight) {
                        foreach (var line in _lines) {
                            m = font.MeasureString(line);
                            m = new Vector2(m.X + _sizeoffset, m.Y + _verticalsizeoffset);
                            if (m.X > _rect.Width) {
                                fitsWidth = false;
                                break;
                            }
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

            _font = _fontSystem.GetFont(best);
        }

        public void Update(GameTime gameTime)
        {
            if (!_changed && !_locChanged) return;
            if (_changed) {
                RecalculateFontAndLayout();
            }
            if (_changed || _locChanged) {
                List<Vector2> positions = [];
                float y = VerticalCentering ? _rect.Y + MathF.Max(0, (_rect.Height - (_font.MeasureString("Aj").Y * _lines.Count)) / 2f) : _rect.Y;
                foreach (var line in _lines) {
                    Vector2 measured = _font.MeasureString(line);
                    float x = _rect.X;
                    switch (_alignment) {
                        case TextAlignment.Left:
                            x = _rect.X;
                            break;
                        case TextAlignment.Center:
                            x = _rect.X + MathF.Max(0, (_rect.Width - measured.X) / 2f);
                            break;
                        case TextAlignment.Right:
                            x = _rect.Right - measured.X;
                            break;
                    }
                    positions.Add(new(x, y));
                    y += measured.Y;
                }
                _positions = positions.ToArray();
            }
            _changed = false;
            _locChanged = false;
        }

        public void Draw(GameTime gameTime, SpriteBatch spriteBatch)
        {
            if (_font == null) return;
            if (_lines.Count == 0) return;

            if (BGColor.A != 0) {
                Texture2D rectTexture = ResourceManager.GetColor(BGColor, spriteBatch);
                spriteBatch.Draw(rectTexture, _rect, Color.White);
            }

            for (int i = 0; i < _lines.Count; i++) {
                if (_strokeSize != 0) {
                    _font.DrawText(spriteBatch, _lines[i], _positions[i], _color, effect: FontSystemEffect.Stroked, effectAmount: _strokeSize);
                }
                else {
                    _font.DrawText(spriteBatch, _lines[i], _positions[i], _color, effect: FontSystemEffect.None);
                }
            }
        }
    }
}
