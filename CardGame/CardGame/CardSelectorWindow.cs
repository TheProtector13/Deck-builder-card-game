using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

#nullable enable
namespace CardGame {
    internal class CardSelectorWindow : IDrawable {
        private static readonly float CARD_WIDTH_SCALE = 2f / 3f;
        private Texture2D _texture;
        private Rectangle _rect;
        private TextBox _titleBox;
        private TimeSpan _warning_start = TimeSpan.FromSeconds(1);
        private TextBox _warningBox;
        private Button _cancelButton;
        private Button _okButton;
        private Tuple<Card, Card>[] cardList;
        private int selectionCount;
        private bool forcedSelection;
        private MouseInfo _mouse;
        private bool hasCancelButton = true;
        private bool hasOkButton = true;
        private float _prevSliderValue = 0f;
        private Slider? _slider = null;

        //cards
        private int startX, startY, padding, cardWidth, cardHeight;
        private float offsetStep;

        public event EventHandler? SelectionConfirmed;
        public event EventHandler? SelectionCancelled;
        public int SelectedCount { get; private set; } = 0;
        public string Title
        {
            get => _titleBox.Text;
            set => _titleBox.Text = value;
        }
        public SelectionResult Result { get; private set; } = SelectionResult.None;
        public bool HasCancelButton
        {
            get => hasCancelButton;
            set {
                hasCancelButton = value;
                _cancelButton.Enabled = value;
                if (!HasCancelButton || !HasOkButton) {
                    _cancelButton.Location = new(_rect.X + (_rect.Width / 2) - (_cancelButton.Size.X / 2),
                                                 _rect.Bottom - (_cancelButton.Size.Y / 2));
                    _okButton.Location = new(_rect.X + (_rect.Width / 2) - (_okButton.Size.X / 2),
                                             _rect.Bottom - (_okButton.Size.Y / 2));
                }
                else {
                    Point buttonSize = _cancelButton.Size;
                    _cancelButton.Location = new(_rect.Right - (buttonSize.X * 2), _rect.Bottom - (buttonSize.Y / 2));
                    _okButton.Location = new(_rect.X + (buttonSize.X * 2), _rect.Bottom - (buttonSize.Y / 2));
                }
            }
        }
        public bool HasOkButton
        {
            get => hasOkButton;
            set {
                hasOkButton = value;
                _okButton.Enabled = hasOkButton;
                if (!HasCancelButton || !HasOkButton) {
                    _cancelButton.Location = new(_rect.X + (_rect.Width / 2) - (_cancelButton.Size.X / 2),
                                                 _rect.Bottom - (_cancelButton.Size.Y / 2));
                    _okButton.Location = new(_rect.X + (_rect.Width / 2) - (_okButton.Size.X / 2),
                                             _rect.Bottom - (_okButton.Size.Y / 2));
                }
                else {
                    Point buttonSize = _cancelButton.Size;
                    _cancelButton.Location = new(_rect.X + buttonSize.X, _rect.Bottom - (buttonSize.Y / 2));
                    _okButton.Location = new(_rect.Right - (buttonSize.X * 2), _rect.Bottom - (buttonSize.Y / 2));
                }
            }
        }

        public enum SelectionResult {
            None,
            Confirmed,
            Cancelled
        }

        private CardSelectorWindow() => throw new NotImplementedException();
        public CardSelectorWindow(List<Card> cards, int selectionCount, MouseInfo mouse, bool forced = false)
        {
            _texture = ResourceManager.Textures["SelectWindow"][0];
            _mouse = mouse;
            int height = DisplayInfo.GetPXfromHeight(0.8);
            int width = height / 3 * 4;
            _rect = DisplayInfo.CenterRect(new(0, 0, width, height), DisplayInfo.ScreenRect);
            int boxheight = DisplayInfo.GetPXfromHeight(0.05);
            _titleBox = new TextBox(new(_rect.X, _rect.Top - boxheight, _rect.Width, boxheight), ResourceManager.Fonts["FONT_DEF_B"]) {
                BGColor = Color.GhostWhite
            };
            _warningBox = new TextBox(new(_rect.X, _rect.Center.Y - (boxheight / 2), _rect.Width, boxheight), ResourceManager.Fonts["FONT_DEF_B"]) {
                BGColor = Color.Red,
                Color = Color.White,
                Text = "Túl kevés kártyát választottál!"
            };
            Point buttonSize = new((int)Math.Round(height * 0.148148148));
            _cancelButton = new(ResourceManager.Textures["NOK_Button"], mouse) {
                Location = new(_rect.X + buttonSize.X, _rect.Bottom - (buttonSize.Y / 2)),
                Size = buttonSize
            };
            _okButton = new(ResourceManager.Textures["OK_Button"], mouse) {
                Location = new(_rect.Right - (buttonSize.X * 2), _rect.Bottom - (buttonSize.Y / 2)),
                Size = buttonSize
            };
            this.selectionCount = selectionCount;
            cardList = new Tuple<Card, Card>[cards.Count];
            for (int i = 0; i < cards.Count; i++) {
                cardList[i] = new Tuple<Card, Card>((Card)cards[i].Clone(), cards[i]);
            }
            forcedSelection = forced;
            //Cards
            startX = _rect.X + (buttonSize.X / 2);
            startY = _rect.Y + (buttonSize.Y / 2);
            padding = (int)MathF.Round((width - buttonSize.X) * 0.05f / 6);
            cardWidth = (int)MathF.Round(((width - buttonSize.X) / 5f) - (padding * 1.2f));
            cardHeight = (int)MathF.Round(cardWidth / CARD_WIDTH_SCALE);
            for (int i = 0; i < cardList.Length; i++) {
                int row = i / 5;
                int col = i % 5;
                cardList[i].Item1.Rect = new Rectangle(
                    startX + padding + (col * (cardWidth + padding)),
                    startY + padding + (row * (cardHeight + padding)),
                    cardWidth,
                    cardHeight
                );
                cardList[i].Item1.Flipped = false;
            }
            if (cardList.Length > 10) {
                _slider = new(mouse) {
                    IsVertical = true,
                    Location = new(_rect.Right - (buttonSize.X / 2), _rect.Y + (buttonSize.Y / 2)),
                    Size = new(_rect.Height - buttonSize.Y, buttonSize.X / 4),
                    Value = 0f
                };
                _slider.OnChange += OnSliderValueChanged;
            }
            _cancelButton.Click += OnCancelButtonClicked;
            _okButton.Click += OnOkButtonClicked;
            int totalRows = (int)Math.Ceiling(cardList.Length / 5f);
            int visibleRows = 2;
            offsetStep = 1f / (totalRows - (visibleRows - 1));
        }

        private void OnCancelButtonClicked(object? sender, EventArgs e)
        {
            Result = SelectionResult.Cancelled;
            SelectionCancelled?.Invoke(this, EventArgs.Empty);
        }

        private void OnOkButtonClicked(object? sender, EventArgs e)
        {
            if (forcedSelection && SelectedCount != selectionCount) {
                _warning_start = TimeSpan.Zero;
                return;
            }
            Result = SelectionResult.Confirmed;
            SelectionConfirmed?.Invoke(this, EventArgs.Empty);
        }

        public Card[] GetSelectedCards()
        {
            List<Card> selectedCards = [];
            foreach (var cardPair in cardList) {
                var card = cardPair.Item1;
                var originalCard = cardPair.Item2;
                if (card.Flipped) {
                    selectedCards.Add(originalCard);
                }
            }
            return selectedCards.ToArray();
        }

        private void OnSliderValueChanged(object? sender, EventArgs e)
        {
            _slider!.Value = MathF.Round(_slider.Value / offsetStep) * offsetStep;
        }

        public void Update(GameTime gameTime)
        {
            //Slider
            if (_slider != null) {
                if (_prevSliderValue != _slider.Value) {
                    _prevSliderValue = _slider.Value;
                }
                _slider.Value += _mouse.WheelDelta * -offsetStep;
                _slider.Update(gameTime);
            }
            //Cards
            foreach (var cardPair in cardList) {
                var card = cardPair.Item1;
                if (_slider != null && _prevSliderValue != _slider.Value) {
                    int yOffset = (int)(MathF.Round(_slider.Value / offsetStep) * (cardHeight + padding));
                    int row = Array.IndexOf(cardList, cardPair) / 5;
                    card.Rect = new Rectangle(
                        card.Rect.X,
                        startY + padding + (row * (cardHeight + padding)) - yOffset,
                        cardWidth,
                        cardHeight
                    );
                }
                card.Update(gameTime);
            }
            //Buttons
            if (HasCancelButton)
                _cancelButton.Update(gameTime);
            if (HasOkButton)
                _okButton.Update(gameTime);
            //Mouse Events on Cards
            if (_mouse.Current.LeftButton == ButtonState.Pressed &&
                _mouse.Previous.LeftButton == ButtonState.Released) {
                foreach (var cardPair in cardList) {
                    var card = cardPair.Item1;
                    if (card.Rect.Contains(_mouse.GetMousePosition()) &&
                        _rect.Contains(card.Rect.Right, card.Rect.Bottom) &&
                        _rect.Contains(card.Rect.X, card.Rect.Y)) {
                        card.Flipped = !card.Flipped;
                        if (card.Flipped) {
                            SelectedCount++;
                            if (SelectedCount > selectionCount) {
                                SelectedCount--;
                                card.Flipped = false;
                            }
                        }
                        else {
                            SelectedCount--;
                        }
                        break;
                    }
                }
            }
            _titleBox.Update(gameTime);
            _warningBox.Update(gameTime);
            if (_warning_start == TimeSpan.Zero)
                _warning_start = gameTime.TotalGameTime;
        }

        public void Draw(GameTime gameTime, SpriteBatch spriteBatch)
        {
            spriteBatch.Draw(_texture, _rect, Color.White);
            foreach (var cardPair in cardList) {
                var card = cardPair.Item1;
                if (_rect.Contains(card.Rect.Right, card.Rect.Bottom) &&
                    _rect.Contains(card.Rect.X, card.Rect.Y))
                    card.Draw(gameTime, spriteBatch);
            }
            _titleBox.Draw(gameTime, spriteBatch);
            if (forcedSelection && gameTime.TotalGameTime - _warning_start < TimeSpan.FromSeconds(2)) {
                _warningBox.Draw(gameTime, spriteBatch);
            }
            if (HasCancelButton)
                _cancelButton.Draw(gameTime, spriteBatch);
            if (HasOkButton)
                _okButton.Draw(gameTime, spriteBatch);
            _slider?.Draw(gameTime, spriteBatch);
        }
    }
}
