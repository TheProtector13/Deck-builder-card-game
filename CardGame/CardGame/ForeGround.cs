using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Tensorflow;

#nullable enable
namespace CardGame {
    internal class ForeGround : IDrawable {
        private static readonly float CARD_WIDTH_SCALE = 2f / 3f;
        private static readonly int MAX_OFFSET_CARDS = 4;
        private readonly BackGround BG;
        private readonly List<Card> GameDeck;
        private readonly Card?[] Shop;
        private Rectangle ShopLoc;
        private readonly ObjectTransform[] ShopTarget;
        private readonly List<Card> PlayerDeck;
        private Rectangle PlayerDeckLoc;
        private readonly List<Card> PlayerHand;
        private Rectangle PlayerHandLoc;
        private readonly List<ObjectTransform> PlayerHandTarget;
        private readonly List<Card> PlayerScrap;
        private Rectangle PlayerScrapLoc;
        private readonly List<Card> EnemyDeck;
        private Rectangle EnemyDeckLoc;
        private readonly List<Card> EnemyHand;
        private Rectangle EnemyHandLoc;
        private readonly List<ObjectTransform> EnemyHandTarget;
        private readonly List<Card> EnemyScrap;
        private Rectangle EnemyScrapLoc;
        private readonly List<Card> PlayedPile;
        private Rectangle PlayedPileLoc;
        private readonly List<ObjectTransform> PlayedPileTarget;
        private readonly TextBox[] PIconboxes;
        private readonly Texture2D[] Icons;
        private readonly Rectangle[] PIconsLoc;
        private readonly TextBox[] EIconboxes;
        private readonly Rectangle[] EIconsLoc;
        private readonly Button EndTurnButton;
        private CardSelectorWindow? cardSelector = null;
        private EndingScreen? endingScreen = null;
        private bool changed = true;

        //variables for layout calculation
        private int groupingOffset = 2;
        private Rectangle globalCardStart = Rectangle.Empty;
        private TimeSpan LastGC = TimeSpan.Zero;

        //Game variables
        private int PlayerAttack = 0;
        private int PlayerMoney = 0;
        private int PlayerHealth = 90;
        private int EnemyAttack = 0;
        private int EnemyMoney = 0;
        private int EnemyHealth = 90;
        public string PlayerName { get; set; } = "Játékos";
        public string EnemyName { get; set; } = "AI";
        private bool playerTurn = true;
        private int PlayerCard2ScrapThisTurn = 0;
        private int EnemyCard2ScrapThisTurn = 0;
        private readonly List<Card> stolenCards = [];
        public bool RandomAI { get; set; } = false;
        public GameWinner WINNER { get; private set; } = GameWinner.InProgress;


        public enum GameWinner {
            InProgress,
            Player,
            Enemy
        }


        //Mouse Control variables
        private readonly MouseInfo mouse;
        private Card? selectedCard = null;
        private readonly List<Tuple<Card, ObjectTransform>> previewThis = [];
        private ObjectTransform? previewThisCard = null;
        private Rectangle previewThisCardOTarget = Rectangle.Empty;
        private Rectangle previewRect = Rectangle.Empty;
        private Texture2D? overlay = null;
        private bool cardMouseControlEnabled = true;
        private bool ShowPlayedPile = false;
        private bool ShowPlayerHand = false;

        private ForeGround() => throw new NotImplementedException();
        public ForeGround(BackGround bg)
        {
            mouse = new(Mouse.GetState());
            BG = bg;
            GameDeck = DeckGenerator.GenDeck(BG.Type);
            foreach (var card in GameDeck) {
                card.Flipped = true;
            }
            DeckGenerator.ShuffleDeck(GameDeck);
            Shop = new Card?[6];
            PlayerDeck = DeckGenerator.GenStartDeck();
            foreach (var card in PlayerDeck) {
                card.Flipped = true;
            }
            EnemyDeck = DeckGenerator.GenStartDeck();
            foreach (var card in EnemyDeck) {
                card.Flipped = true;
            }
            PlayerHand = [];
            PlayerScrap = [];
            EnemyHand = [];
            EnemyScrap = [];
            PlayedPile = [];
            FontSystem font = ResourceManager.Fonts["FONT_DEF_B"];
            Rectangle rect = new(0, 0, 100, 50);
            PIconboxes = [
                new TextBox(rect, font),
                new TextBox(rect, font),
                new TextBox(rect, font),
                new TextBox(rect, font)];
            PIconboxes[3].Color = Color.White;
            PIconboxes[3].StrokeSize = 2;
            EIconboxes = [
                new TextBox(rect, font),
                new TextBox(rect, font),
                new TextBox(rect, font),
                new TextBox(rect, font)];
            EIconboxes[3].Color = Color.White;
            EIconboxes[3].StrokeSize = 2;
            Icons = [
                ResourceManager.Textures["AttackBGI"][0],
                ResourceManager.Textures["TargetIcon"][0],
                ResourceManager.Textures["MoneyBGI"][0],
                ResourceManager.Textures["MoneyFGI"][0],
                ResourceManager.Textures["HPIcon"][0]];
            EIconsLoc = new Rectangle[Icons.Length];
            PIconsLoc = new Rectangle[Icons.Length];
            ShopTarget = new ObjectTransform[6];
            EnemyHandTarget = [];
            PlayedPileTarget = [];
            PlayerHandTarget = [];
            EndTurnButton = new Button(ResourceManager.Textures["BUTTON"], mouse) {
                Text = "Kör vége",
                Enabled = false
            };
            EndTurnButton.Click += EndTurnEventHandler;
            CalculateLayout();
            RefillShop();
            RefillPlayerHand();
            RefillEnemyHand();
            //ITT NEM UPDATEOLJUK A BG-T, AZT A GAME1 CSINÁLJA
        }

        private void AddToPlayedPile(Card card, ObjectTransform cardTrans, bool player = true)
        {
            int pileCardWidth = (int)MathF.Round(PlayedPileLoc.Height * CARD_WIDTH_SCALE);
            int divnum = PlayedPile.Count + 1;
            int pileWidthOffset = (PlayedPileLoc.Width - (divnum * pileCardWidth)) / divnum;
            PlayedPile.Add(card);
            PlayedPileTarget.Add(cardTrans);
            if (player) {
                PlayerHand.Remove(card);
                PlayerHandTarget.Remove(cardTrans);
            }
            else {
                card.Flipped = false;
                EnemyHand.Remove(card);
                EnemyHandTarget.Remove(cardTrans);
            }
            for (int i = 0; i < PlayedPile.Count; i++) {
                PlayedPileTarget[i].MoveTarget = new Rectangle(
                    PlayedPileLoc.X + Math.Abs(pileWidthOffset) + (i * pileCardWidth) + (i * pileWidthOffset),
                    PlayedPileLoc.Y,
                    pileCardWidth,
                    PlayedPileLoc.Height);
            }
        }

        private void PlayNext(bool player = true)
        {
            if (player) {
                for (int i = 0; i <= (int)Card.Effect.None; i++) {
                    foreach (var card in PlayerHand.Where(card => card.CardEffect == (Card.Effect)i)) {
                        AddToPlayedPile(card, PlayerHandTarget[PlayerHand.IndexOf(card)], true);
                        break;
                    }
                }
            }
            else {
                for (int i = 0; i <= (int)Card.Effect.None; i++) {
                    foreach (var card in EnemyHand.Where(card => card.CardEffect == (Card.Effect)i)) {
                        AddToPlayedPile(card, EnemyHandTarget[EnemyHand.IndexOf(card)], false);
                        break;
                    }
                }
            }
        }

        private void PlayCard(Card card, bool player = true)
        {
            if (!card.BaseApplied) {
                if (player) {
                    PlayerAttack += card.GetTrueAttack();
                    PlayerMoney += card.Money;
                    PlayerHealth += card.Health;
                    card.BaseApplied = true;
                }
                else {
                    EnemyAttack += card.GetTrueAttack();
                    EnemyMoney += card.Money;
                    EnemyHealth += card.Health;
                    card.BaseApplied = true;
                }
            }
            if (!card.EffectsApplied) {
                if (card.EffectRequirement != Card.Fraction.None) {
                    if (!PlayedPile.Any(x => x != card && x.CardFraction == card.EffectRequirement)) {
                        return;
                    }
                }
                switch (card.CardEffect) {
                    case Card.Effect.SelfDestruct:
                        PlayedPileTarget.RemoveAt(PlayedPile.IndexOf(card));
                        PlayedPile.Remove(card);
                        break;
                    case Card.Effect.ScrapOwnCard:
                        // optionally destroy a card from scrap pile
                        if (player) {
                            if (PlayerScrap.Count != 0 && PlayerHand.Count + PlayerDeck.Count + PlayerScrap.Count - card.EffectAmount > 6) {
                                cardSelector = new(PlayerScrap, card.EffectAmount, mouse) {
                                    Title = $"Válaszd ki a paklidból kidobandó lapokat! (Max. {card.EffectAmount} db)"
                                };
                                cardSelector.SelectionConfirmed += ScrapOwnCardEventHandler;
                            }
                        }
                        else {
                            if (EnemyScrap.Count != 0 && EnemyHand.Count + EnemyDeck.Count + EnemyScrap.Count - card.EffectAmount > 6) {
                                int scrapped = 0;
                                while (scrapped < card.EffectAmount && EnemyScrap.Count > 0) {
                                    int scrapindex = 0;
                                    if (!RandomAI) {
                                        if (EnemyScrap.Count > 1) {
                                            List<Card> tempScrap = EnemyScrap.Where(card => card.CardFraction == Card.Fraction.None).ToList();
                                            ModelOutput5 strategyoutput = MLController.StrategyEngine.Predict(new ModelInput5() { Features = GetStrategyDistribution(true) });
                                            List<float> minValues = strategyoutput.Prediction.ToList();
                                            minValues.Sort();
                                            for (int i = 0; i < 2; i++) {
                                                tempScrap.AddRange(EnemyScrap.Where(card => card.CardFraction == (Card.Fraction)Array.IndexOf(strategyoutput.Prediction, minValues[i])).ToList());
                                            }
                                            if (tempScrap.Count == 0)
                                                tempScrap = EnemyScrap;
                                            for (int i = 1; i < tempScrap.Count; i++) {
                                                List<float> inputs = [];
                                                //scrapindexed card
                                                inputs.Add(1);
                                                for (int j = 0; j < (int)Card.Effect.None; j++) {
                                                    if (tempScrap[scrapindex].CardEffect == (Card.Effect)j) {
                                                        inputs.Add(1);
                                                    }
                                                    else {
                                                        inputs.Add(0);
                                                    }
                                                }
                                                //current card
                                                inputs.Add(1);
                                                for (int j = 0; j < (int)Card.Effect.None; j++) {
                                                    if (tempScrap[i].CardEffect == (Card.Effect)j) {
                                                        inputs.Add(1);
                                                    }
                                                    else {
                                                        inputs.Add(0);
                                                    }
                                                }
                                                ModelOutput2 output = MLController.DiscardEngine.Predict(new ModelInput26() { Features = inputs.ToArray() });
                                                if (output.Prediction[0] < output.Prediction[1]) {
                                                    scrapindex = i;
                                                }
                                            }
                                            scrapindex = EnemyScrap.IndexOf(tempScrap[scrapindex]);
                                        }
                                        else {
                                            if (EnemyScrap[scrapindex].CardFraction != Card.Fraction.None)
                                                break;
                                        }
                                    }
                                    if (EnemyScrap[scrapindex].CardFraction != Card.Fraction.None)
                                        GameDeck.Add(EnemyScrap[scrapindex]);
                                    EnemyScrap.RemoveAt(scrapindex);
                                    scrapped++;
                                    changed = true;
                                    //AI LOGIC KELL IDE !!! (done)
                                }
                            }
                        }
                        break;
                    case Card.Effect.ScrapFromShop:
                        if (player) {
                            cardSelector = new(Shop.ToList()!, card.EffectAmount, mouse, true) {
                                HasCancelButton = false,
                                Title = $"Válaszd ki a boltból eltávolítandó lapokat! (Pontosan {card.EffectAmount} db)"
                            };
                            cardSelector.SelectionConfirmed += ScrapFromShopEventHandler;
                        }
                        else {
                            for (int i = 0; i < card.EffectAmount; i++) {
                                int scrapindex = 0;
                                if (RandomAI) {
                                    scrapindex = Random.Shared.Next(0, Shop.Length);
                                }
                                else {
                                    float[] distribution = GetStrategyDistribution(false);
                                    ModelInput5 input = new() { Features = distribution };
                                    ModelOutput5 output = MLController.StrategyEngine.Predict(input);
                                    List<float> maxValues = output.Prediction.ToList();
                                    maxValues.Sort();
                                    maxValues.Reverse();
                                    int maxValueIndex = 0;
                                    List<int> foundCardIndexes = [];
                                    while (foundCardIndexes.Count == 0 && maxValueIndex < maxValues.Count) {
                                        //If there is more than one max value, select one randomly
                                        List<int> maxIndices = output.Prediction
                                            .Select((value, index) => new { value, index })
                                            .Where(x => x.value == maxValues[maxValueIndex])
                                            .Select(x => x.index)
                                            .ToList();
                                        while (maxIndices.Count > 0) {
                                            int maxIndex = maxIndices[Random.Shared.Next(0, maxIndices.Count)];
                                            maxIndices.Remove(maxIndex);
                                            foundCardIndexes = Shop.Select((card, index) => new { card, index })
                                                .Where(x => x.card != null && x.card.CardFraction == (Card.Fraction)maxIndex)
                                                .Select(x => x.index)
                                                .ToList();
                                            if (foundCardIndexes.Count > 0)
                                                break;
                                        }
                                        maxValueIndex++;
                                    }
                                    if (foundCardIndexes.Count > 0) {
                                        ModelInput78 input78 = new();
                                        List<float> inputs = [];
                                        for (int j = 0; j < Shop.Length; j++) {
                                            if (foundCardIndexes.Contains(j)) {
                                                inputs.Add(1);
                                                for (int k = 0; k < (int)Card.Effect.None; k++) {
                                                    if (Shop[j]!.CardEffect == (Card.Effect)k) {
                                                        inputs.Add(1);
                                                    }
                                                    else {
                                                        inputs.Add(0);
                                                    }
                                                }
                                            }
                                            else {
                                                inputs.AddRange(new float[13]);
                                            }
                                        }
                                        input78.Features = inputs.ToArray();
                                        ModelOutput6 output6 = MLController.ShoppingEngine.Predict(input78);
                                        scrapindex = output6.Prediction.ToList().IndexOf(output6.Prediction.Max());
                                    }
                                }
                                if (Shop[scrapindex] != null) {
                                    if (Shop[scrapindex]!.CardFraction != Card.Fraction.None)
                                        GameDeck.Add(Shop[scrapindex]!);
                                    Shop[scrapindex] = null;
                                }
                                RefillShop();
                            }
                            //AI LOGIC KELL IDE !!! (done)
                        }
                        break;
                    case Card.Effect.ShowHand:
                        List<Card> cards = player ? EnemyHand : PlayerHand;
                        if (cards.Any(tcard => tcard.CardEffect == Card.Effect.AntiShow)) {
                            if (player)
                                cards.Where(tcard => tcard.CardEffect == Card.Effect.AntiShow).ToList().ForEach(tcard => tcard.Flipped = true);
                            break;
                        }
                        else {
                            int j_offset = 0;
                            for (int i = 0; i < card.EffectAmount + j_offset; i++) {
                                if (cards.Count <= i) {
                                    break;
                                }
                                if (cards[i].Flipped) {
                                    cards[i].Flipped = false;
                                }
                                else {
                                    j_offset++;
                                }
                            }
                        }
                        break;
                    case Card.Effect.ShowDeck:
                        List<Card> cards2 = player ? EnemyHand : PlayerHand;
                        List<Card> deckcards = player ? EnemyDeck : PlayerDeck;
                        if (cards2.Any(tcard => tcard.CardEffect == Card.Effect.AntiShow)) {
                            if (player)
                                cards2.Where(tcard => tcard.CardEffect == Card.Effect.AntiShow).ToList().ForEach(tcard => tcard.Flipped = true);
                            break;
                        }
                        if (deckcards.Count == 0 || !player) {
                            break;
                        }
                        cardSelector = new(deckcards.TakeLast(card.EffectAmount).ToList(), 0, mouse) {
                            HasOkButton = false,
                            Title = $"Az ellenfél pakliának következő {card.EffectAmount} lapja."
                        };
                        break;
                    case Card.Effect.StealCard:
                        List<Card> cards3 = player ? EnemyHand : PlayerHand;
                        if (cards3.Any(tcard => tcard.CardEffect == Card.Effect.AntiShow)) {
                            if (player)
                                cards3.Where(tcard => tcard.CardEffect == Card.Effect.AntiShow).ToList().ForEach(tcard => tcard.Flipped = true);
                            break;
                        }
                        for (int i = 0; i < card.EffectAmount; i++) {
                            if (cards3.Count == 0) {
                                break;
                            }
                            Card stolen = DeckGenerator.GetCard(cards3);
                            stolenCards.Add(stolen);
                            AddToPlayedPile(stolen, player ? EnemyHandTarget[EnemyHand.IndexOf(stolen)] : PlayerHandTarget[PlayerHand.IndexOf(stolen)], !player);
                        }
                        break;
                    case Card.Effect.ScrapEnemyCard:
                        if (player)
                            EnemyCard2ScrapThisTurn += card.EffectAmount;
                        else
                            PlayerCard2ScrapThisTurn += card.EffectAmount;
                        break;
                    case Card.Effect.DrawCard:
                        if (player)
                            for (int i = 0; i < card.EffectAmount; i++) {
                                PlayerDrawCard();
                            }
                        else
                            for (int i = 0; i < card.EffectAmount; i++) {
                                EnemyDrawCard();
                            }
                        break;
                    case Card.Effect.MoneyBonus:
                        if (player)
                            PlayerMoney += card.EffectAmount;
                        else
                            EnemyMoney += card.EffectAmount;
                        break;
                    case Card.Effect.AttackBonus:
                        if (player)
                            PlayerAttack += card.EffectAmount;
                        else
                            EnemyAttack += card.EffectAmount;
                        break;
                    case Card.Effect.HealthBonus:
                        if (player)
                            PlayerHealth += card.EffectAmount;
                        else
                            EnemyHealth += card.EffectAmount;
                        break;
                    default:
                        break;
                }
                card.EffectsApplied = true;
            }
        }

        //######## HANDLING METHODS #######

        private void ScrapOwnCardEventHandler(object? sender, EventArgs e)
        {
            Card[] selectedCards = cardSelector!.GetSelectedCards();
            foreach (Card card in selectedCards) {
                if (card.CardFraction != Card.Fraction.None)
                    GameDeck.Add(card);
                PlayerScrap.Remove(card);
            }
            changed = true;
        }

        private void ScrapOwnCardFromHandEventHandler(object? sender, EventArgs e)
        {
            Card[] selectedCards = cardSelector!.GetSelectedCards();
            foreach (Card card in selectedCards) {
                PlayerScrap.Add(card);
                card.Flipped = true;
                PlayerHandTarget.RemoveAt(PlayerHand.IndexOf(card));
                PlayerHand.Remove(card);
            }
            changed = true;
        }

        private void ScrapFromShopEventHandler(object? sender, EventArgs e)
        {
            Card[] selectedCards = cardSelector!.GetSelectedCards();
            foreach (Card card in selectedCards) {
                for (int i = 0; i < Shop.Length; i++) {
                    if (Shop[i] == card) {
                        if (card.CardFraction != Card.Fraction.None)
                            GameDeck.Add(Shop[i]!);
                        Shop[i] = null;
                        break;
                    }
                }
            }
            RefillShop();
        }

        private void EndTurnEventHandler(object? sender, EventArgs e)
        {
            ClearPlayedPile(true);
            RefillPlayerHand();
            PlayerMoney = 0;
            EnemyHealth -= PlayerAttack;
            PlayerAttack = 0;
            playerTurn = false;
            EndTurnButton.Enabled = false;
        }

        //######## END ####################

        private float[] GetStrategyDistribution(bool player = true)
        {
            int allcards = 0;
            int[] cards = new int[5];
            float[] distribution = new float[5];
            if (player) {
                foreach (var card in PlayerHand) {
                    if (card.CardFraction != Card.Fraction.None) {
                        cards[(int)card.CardFraction]++;
                        allcards++;
                    }
                }
                foreach (var card in PlayerDeck) {
                    if (card.CardFraction != Card.Fraction.None) {
                        cards[(int)card.CardFraction]++;
                        allcards++;
                    }
                }
                foreach (var card in PlayerScrap) {
                    if (card.CardFraction != Card.Fraction.None) {
                        cards[(int)card.CardFraction]++;
                        allcards++;
                    }
                }
                if (allcards > 0) {
                    for (int i = 0; i < distribution.Length; i++) {
                        distribution[i] = (float)cards[i] / allcards;
                    }
                }
                else {
                    for (int i = 0; i < distribution.Length; i++) {
                        distribution[i] = 0.2f;
                    }
                }
            }
            else {
                foreach (var card in EnemyHand) {
                    if (card.CardFraction != Card.Fraction.None) {
                        cards[(int)card.CardFraction]++;
                        allcards++;
                    }
                }
                foreach (var card in EnemyDeck) {
                    if (card.CardFraction != Card.Fraction.None) {
                        cards[(int)card.CardFraction]++;
                        allcards++;
                    }
                }
                foreach (var card in EnemyScrap) {
                    if (card.CardFraction != Card.Fraction.None) {
                        cards[(int)card.CardFraction]++;
                        allcards++;
                    }
                }
                if (allcards > 0) {
                    for (int i = 0; i < distribution.Length; i++) {
                        distribution[i] = (float)cards[i] / allcards;
                    }
                }
                else {
                    for (int i = 0; i < distribution.Length; i++) {
                        distribution[i] = 0.2f;
                    }
                }
            }
            return distribution;
        }

        private void BuyFromShop(Card card, bool player = true)
        {
            for (int i = 0; i < Shop.Length; i++) {
                if (Shop[i] == card) {
                    if (player) {
                        PlayerScrap.Add(Shop[i]!);
                    }
                    else {
                        EnemyScrap.Add(Shop[i]!);
                    }
                    Shop[i] = null;
                    break;
                }
            }
            card.Flipped = true;
            card.RenderPrice = false;
            RefillShop();
        }

        private void RefillPlayerHand()
        {
            int handCardWidth = (int)MathF.Round(PlayerHandLoc.Height * CARD_WIDTH_SCALE);
            int handWidthOffset = (int)MathF.Round((PlayerHandLoc.Width - (5f * handCardWidth)) / 6);
            while (PlayerHand.Count < 5) {
                if (PlayerDeck.Count == 0) {
                    PlayerDeck.AddRange(PlayerScrap);
                    PlayerScrap.Clear();
                    DeckGenerator.ShuffleDeck(PlayerDeck);
                }
                PlayerHand.Add(PlayerDeck[^1]);
                PlayerHand[^1].Flipped = false;
                PlayerDeck.RemoveAt(PlayerDeck.Count - 1);
                PlayerHandTarget.Add(new(
                    PlayerDeckLoc,
                    new Rectangle(
                        PlayerHandLoc.X + Math.Abs(handWidthOffset) + ((PlayerHand.Count - 1) * handCardWidth) + ((PlayerHand.Count - 1) * handWidthOffset),
                        PlayerHandLoc.Y,
                        handCardWidth,
                        PlayerHandLoc.Height)));
            }
            changed = true;
        }

        private void PlayerDrawCard()
        {
            if (PlayerDeck.Count == 0) {
                PlayerDeck.AddRange(PlayerScrap);
                PlayerScrap.Clear();
                DeckGenerator.ShuffleDeck(PlayerDeck);
            }
            PlayerHand.Add(PlayerDeck[^1]);
            PlayerHand[^1].Flipped = false;
            PlayerDeck.RemoveAt(PlayerDeck.Count - 1);
            int handCardWidth = (int)MathF.Round(PlayerHandLoc.Height * CARD_WIDTH_SCALE);
            int handWidthOffset = (int)MathF.Round((PlayerHandLoc.Width - (5f * handCardWidth)) / 6);
            PlayerHandTarget.Add(new(
                PlayerDeckLoc,
                new Rectangle(
                    PlayerHandLoc.X + Math.Abs(handWidthOffset) + ((PlayerHand.Count - 1) * handCardWidth) + ((PlayerHand.Count - 1) * handWidthOffset),
                    PlayerHandLoc.Y,
                    handCardWidth,
                    PlayerHandLoc.Height)));
            for (int i = 0; i < PlayerHandTarget.Count - 1; i++) {
                PlayerHandTarget[i].MoveTarget = new Rectangle(
                    PlayerHandLoc.X + Math.Abs(handWidthOffset) + (i * handCardWidth) + (i * handWidthOffset),
                    PlayerHandLoc.Y,
                    handCardWidth,
                    PlayerHandLoc.Height);
            }
            changed = true;
        }

        private void RefillEnemyHand()
        {
            int handCardWidth = (int)MathF.Round(EnemyHandLoc.Height * CARD_WIDTH_SCALE);
            int handWidthOffset = (int)MathF.Round((EnemyHandLoc.Width - (5f * handCardWidth)) / 6);
            while (EnemyHand.Count < 5) {
                if (EnemyDeck.Count == 0) {
                    EnemyDeck.AddRange(EnemyScrap);
                    EnemyScrap.Clear();
                    DeckGenerator.ShuffleDeck(EnemyDeck);
                }
                EnemyHand.Add(EnemyDeck[^1]);
                EnemyDeck.RemoveAt(EnemyDeck.Count - 1);
                EnemyHandTarget.Add(new(
                    EnemyDeckLoc,
                    new Rectangle(
                        EnemyHandLoc.X + Math.Abs(handWidthOffset) + ((EnemyHand.Count - 1) * handCardWidth) + ((EnemyHand.Count - 1) * handWidthOffset),
                        EnemyHandLoc.Y,
                        handCardWidth,
                        EnemyHandLoc.Height)));
            }
            changed = true;
        }

        private void EnemyDrawCard()
        {
            if (EnemyDeck.Count == 0) {
                EnemyDeck.AddRange(EnemyScrap);
                EnemyScrap.Clear();
                DeckGenerator.ShuffleDeck(EnemyDeck);
            }
            EnemyHand.Add(EnemyDeck[^1]);
            EnemyDeck.RemoveAt(EnemyDeck.Count - 1);
            int handCardWidth = (int)MathF.Round(EnemyHandLoc.Height * CARD_WIDTH_SCALE);
            int handWidthOffset = (int)MathF.Round((EnemyHandLoc.Width - (5f * handCardWidth)) / 6);
            EnemyHandTarget.Add(new(
                EnemyDeckLoc,
                new Rectangle(
                    EnemyHandLoc.X + Math.Abs(handWidthOffset) + ((EnemyHand.Count - 1) * handCardWidth) + ((EnemyHand.Count - 1) * handWidthOffset),
                    EnemyHandLoc.Y,
                    handCardWidth,
                    EnemyHandLoc.Height)));
            for (int i = 0; i < EnemyHandTarget.Count - 1; i++) {
                EnemyHandTarget[i].MoveTarget = new Rectangle(
                    EnemyHandLoc.X + Math.Abs(handWidthOffset) + (i * handCardWidth) + (i * handWidthOffset),
                    EnemyHandLoc.Y,
                    handCardWidth,
                    EnemyHandLoc.Height);
            }
            changed = true;
        }

        private void RefillShop()
        {
            if (Shop[0] == null) {
                Shop[0] = DeckGenerator.GetMoneyCard();
                Shop[0]!.Flipped = false;
                Shop[0]!.RenderPrice = true;
                ShopTarget[0].StartLocation = globalCardStart;
            }
            for (int i = 1; i < Shop.Length; i++) {
                if (Shop[i] == null) {
                    if (GameDeck.Count > 0) {
                        Shop[i] = DeckGenerator.GetCard(GameDeck);
                        GameDeck.Remove(Shop[i]!);
                        Shop[i]!.Flipped = false;
                        Shop[i]!.RenderPrice = true;
                        ShopTarget[i].StartLocation = globalCardStart;
                    }
                    else {
                        Shop[i] = DeckGenerator.GetMoneyCard();
                        Shop[i]!.Flipped = false;
                        Shop[i]!.RenderPrice = true;
                        ShopTarget[i].StartLocation = globalCardStart;
                    }
                }
            }
        }

        private void ClearPlayedPile(bool player = true)
        {
            if (player) {
                foreach (var card in PlayedPile) {
                    card.ResetPlayedStatus();
                    card.Flipped = true;
                    if (stolenCards.Contains(card))
                        EnemyScrap.Add(card);
                    else
                        PlayerScrap.Add(card);
                }
            }
            else {
                foreach (var card in PlayedPile) {
                    card.ResetPlayedStatus();
                    card.Flipped = true;
                    if (stolenCards.Contains(card))
                        PlayerScrap.Add(card);
                    else
                        EnemyScrap.Add(card);
                }
            }
            PlayedPile.Clear();
            PlayedPileTarget.Clear();
            stolenCards.Clear();
        }

        //csak egyszer kell meghívni, amikor a képernyő mérete változik
        private void CalculateLayout()
        {
            int startX = DisplayInfo.GetPXfromHeight(0.01666666667);
            int startY = startX;
            int hpiconHeight = DisplayInfo.GetPXfromHeight(0.111111112);
            int shopendingX = (int)Math.Round(DisplayInfo.GetPXfromHeight(0.71111111) * 0.73828125);
            int regularendingX = shopendingX + (int)MathF.Round(hpiconHeight * 3.4746f); //2.75f
            int enemydeckHeight = DisplayInfo.GetPXfromHeight(0.1041666667);
            int enemydeckWidth = (int)MathF.Round(enemydeckHeight * CARD_WIDTH_SCALE);
            int previewHeight = DisplayInfo.ScreenHeight - (2 * startX);
            int previewWidth = (int)MathF.Round(previewHeight * CARD_WIDTH_SCALE);
            previewRect = new((int)MathF.Round((DisplayInfo.ScreenWidth - previewWidth) / 2f),
                              startX,
                              previewWidth,
                              previewHeight);
            groupingOffset = (int)MathF.Round(enemydeckHeight * 0.0138696255f);
            int startEDeckX = startX + (groupingOffset * 4);
            int startEDeckY = startY + (groupingOffset * 4);
            EnemyDeckLoc = new(startEDeckX, startEDeckY, enemydeckWidth, enemydeckHeight);
            EnemyScrapLoc = new(startEDeckX + enemydeckWidth + (groupingOffset * 6), startEDeckY, enemydeckWidth, enemydeckHeight);
            int enemyHandHeight = DisplayInfo.GetPXfromHeight(0.166666667);
            EnemyHandLoc = new(EnemyScrapLoc.Right + (2 * groupingOffset), startY,
                DisplayInfo.ScreenWidth - EnemyScrapLoc.Right - (4 * groupingOffset) - regularendingX,
                enemyHandHeight);
            startY *= 2;
            startY += enemyHandHeight;
            ShopLoc = new(0, startY,
                DisplayInfo.ScreenWidth - shopendingX,
                (DisplayInfo.ScreenHeight / 2) - startY);
            PlayerDeckLoc = new(DisplayInfo.ScreenWidth - startX - enemydeckWidth,
                DisplayInfo.ScreenHeight - startX - enemydeckHeight,
                enemydeckWidth, enemydeckHeight);
            PlayerScrapLoc = new(PlayerDeckLoc.X - enemydeckWidth - (groupingOffset * 6),
                DisplayInfo.ScreenHeight - startX - enemydeckHeight,
                enemydeckWidth, enemydeckHeight);
            PlayerHandLoc = new(EnemyHandLoc.X,
                DisplayInfo.ScreenHeight - startX - ShopLoc.Height,
                DisplayInfo.ScreenWidth - (EnemyHandLoc.X * 3),
                ShopLoc.Height);
            PlayedPileLoc = new(0, ShopLoc.Bottom + startX,
                DisplayInfo.ScreenWidth - regularendingX,
                DisplayInfo.ScreenHeight - ShopLoc.Bottom - (startX * 3) - PlayerHandLoc.Height);
            //icons
            int hpiconWidth = (int)Math.Round(hpiconHeight * 1.8026315789474);
            int iconheight = DisplayInfo.GetPXfromHeight(0.0833333333);
            int iconheighthalf = iconheight / 2;
            int startIconX = DisplayInfo.ScreenWidth - regularendingX + (2 * groupingOffset);
            int startEIconY = EnemyHandLoc.Y + (enemyHandHeight / 2) - iconheighthalf;
            EIconsLoc[0] = new(startIconX, startEIconY, iconheight, iconheight);
            EIconsLoc[1] = new(startIconX - 5, startEIconY - 5, iconheight + 10, iconheight + 10);
            EIconsLoc[2] = new(startIconX + iconheight + 5 + groupingOffset, startEIconY, iconheight, iconheight);
            EIconsLoc[3] = new(EIconsLoc[2].X - 5, startEIconY + iconheighthalf, iconheighthalf + 5, iconheighthalf + 5);
            EIconsLoc[4] = new(EIconsLoc[2].Right + groupingOffset, startEIconY + iconheighthalf - (hpiconHeight / 2), hpiconWidth, hpiconHeight);
            int startPIconY = PlayedPileLoc.Y + (PlayedPileLoc.Height / 2) - iconheighthalf;
            PIconsLoc[0] = new(startIconX, startPIconY, iconheight, iconheight);
            PIconsLoc[1] = new(startIconX - 5, startPIconY - 5, iconheight + 10, iconheight + 10);
            PIconsLoc[2] = new(startIconX + iconheight + 5 + groupingOffset, startPIconY, iconheight, iconheight);
            PIconsLoc[3] = new(PIconsLoc[2].X - 5, startPIconY + iconheighthalf, iconheighthalf + 5, iconheighthalf + 5);
            PIconsLoc[4] = new(PIconsLoc[2].Right + groupingOffset, startPIconY + iconheighthalf - (hpiconHeight / 2), hpiconWidth, hpiconHeight);
            //textboxes
            int hpiconHOffset = (int)MathF.Round(hpiconHeight * 0.105263158f);
            int offset = (int)MathF.Round(iconheight * 0.5f);
            EIconboxes[0].Rect = EIconsLoc[0];
            EIconboxes[1].Rect = EIconsLoc[2];
            EIconboxes[2].Rect = new(EIconsLoc[4].X, EIconsLoc[4].Y, EIconsLoc[4].Width, EIconsLoc[4].Height - hpiconHOffset);
            EIconboxes[3].Rect = new(EIconsLoc[4].X, EIconsLoc[4].Bottom - hpiconHOffset, EIconsLoc[4].Width, hpiconHOffset * 2);
            PIconboxes[0].Rect = PIconsLoc[0];
            PIconboxes[1].Rect = PIconsLoc[2];
            PIconboxes[2].Rect = new(PIconsLoc[4].X, PIconsLoc[4].Y, PIconsLoc[4].Width, PIconsLoc[4].Height - hpiconHOffset);
            PIconboxes[3].Rect = new(PIconsLoc[4].X, PIconsLoc[4].Bottom - hpiconHOffset, PIconsLoc[4].Width, hpiconHOffset * 2);
            //
            EIconboxes[0].SizeOffset = offset;
            EIconboxes[1].SizeOffset = offset;
            EIconboxes[2].SizeOffset = offset;
            PIconboxes[0].SizeOffset = offset;
            PIconboxes[1].SizeOffset = offset;
            PIconboxes[2].SizeOffset = offset;
            EIconboxes[0].VerticalSizeOffset = offset;
            EIconboxes[1].VerticalSizeOffset = offset;
            EIconboxes[2].VerticalSizeOffset = offset;
            PIconboxes[0].VerticalSizeOffset = offset;
            PIconboxes[1].VerticalSizeOffset = offset;
            PIconboxes[2].VerticalSizeOffset = offset;
            //Shop, because its fixed
            int shopCardWidth = (int)MathF.Round(ShopLoc.Height * CARD_WIDTH_SCALE);
            int shopWidthOffset = (ShopLoc.Width - (6 * shopCardWidth)) / 6;
            globalCardStart = new Rectangle(
                        (DisplayInfo.ScreenWidth + shopCardWidth) / 2,
                        DisplayInfo.ScreenHeight + shopCardWidth,
                        shopCardWidth / 2,
                        ShopLoc.Height / 2);
            for (int i = 0; i < ShopTarget.Length; i++) {
                ShopTarget[i] = new ObjectTransform(
                    globalCardStart,
                    new Rectangle(
                        ShopLoc.X + Math.Abs(shopWidthOffset) + (i * shopCardWidth) + (i * shopWidthOffset),
                        ShopLoc.Y,
                        shopCardWidth,
                        ShopLoc.Height));
            }
            //Buttons
            int ETWidth = PlayerDeckLoc.Right - PlayerScrapLoc.X + (groupingOffset * 4);
            int ETHeight = (int)MathF.Round(ETWidth * (EndTurnButton.Size.Y / (float)EndTurnButton.Size.X));
            EndTurnButton.Size = new(ETWidth, ETHeight);
            EndTurnButton.Location = new(PlayerScrapLoc.X - (groupingOffset * 4), PlayerScrapLoc.Y - ETHeight - (groupingOffset * 18));
        }

        public void Update(GameTime gameTime)
        {
            mouse.Update(Mouse.GetState());
            if (endingScreen != null) {
                cardMouseControlEnabled = false;
                endingScreen.Update(gameTime);
                if ((mouse.Previous.LeftButton == ButtonState.Released && mouse.Current.LeftButton == ButtonState.Pressed) ||
                    (mouse.Previous.RightButton == ButtonState.Released && mouse.Current.RightButton == ButtonState.Pressed) ||
                    (mouse.Previous.MiddleButton == ButtonState.Released && mouse.Current.MiddleButton == ButtonState.Pressed) ||
                    Keyboard.GetState().IsKeyDown(Keys.Enter)) {
                    WINNER = PlayerHealth <= 0 ? GameWinner.Enemy : GameWinner.Player;
                }
            }
            else if (cardSelector != null) {
                cardMouseControlEnabled = false;
                cardSelector.Update(gameTime);
                if (cardSelector.Result != CardSelectorWindow.SelectionResult.None) {
                    cardSelector = null;
                    cardMouseControlEnabled = true;
                }
            }
            //GamePlay Logic Here
            else {
                if (PlayedPileTarget.All(i => i.IsTransforming == false) &&
                    ShopTarget.All(i => i.IsTransforming == false) &&
                    EnemyHandTarget.All(i => i.IsTransforming == false) &&
                    PlayerHandTarget.All(i => i.IsTransforming == false)) {
                    //GC
                    if (gameTime.TotalGameTime - LastGC > TimeSpan.FromSeconds(5) && selectedCard is null) {
                        ResourceManager.ResetFonts();
                        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, false, true);
                        LastGC = gameTime.TotalGameTime;
                    }
                    //EndingCheck
                    if (PlayerHealth <= 0) {
                        endingScreen = new(ResourceManager.Textures["Defeat"][0], null, ResourceManager.Fonts["FONT_C"]) {
                            Title = "Vereség!"
                        };
                        endingScreen.Update(gameTime);
                    }
                    else if (EnemyHealth <= 0) {
                        Dictionary<Card.Fraction, int> card_amount = [];
                        for (int i = 0; i < (int)Card.Fraction.None; i++) {
                            card_amount[(Card.Fraction)i] = 0;
                        }
                        PlayerHand.ForEach(i => { if (i.CardFraction != Card.Fraction.None) card_amount[i.CardFraction]++; });
                        PlayerScrap.ForEach(i => { if (i.CardFraction != Card.Fraction.None) card_amount[i.CardFraction]++; });
                        PlayerDeck.ForEach(i => { if (i.CardFraction != Card.Fraction.None) card_amount[i.CardFraction]++; });
                        Card.Fraction winnerFraction = card_amount.OrderByDescending(i => i.Value).First().Key;
                        endingScreen = winnerFraction switch {
                            Card.Fraction.Alliance => new(ResourceManager.Textures["Victory_A"][0], ResourceManager.Textures["AllianceIcon"][0], ResourceManager.Fonts["FONT_A"]),
                            Card.Fraction.CollectorCult => new(ResourceManager.Textures["Victory_C"][0], ResourceManager.Textures["CollectorCultIcon"][0], ResourceManager.Fonts["FONT_C"]),
                            Card.Fraction.Empire => new(ResourceManager.Textures["Victory_E"][0], ResourceManager.Textures["EmpireIcon"][0], ResourceManager.Fonts["FONT_E"]),
                            Card.Fraction.Machines => new(ResourceManager.Textures["Victory_M"][0], ResourceManager.Textures["MachinesIcon"][0], ResourceManager.Fonts["FONT_M"]),
                            Card.Fraction.TheEye => new(ResourceManager.Textures["Victory_T"][0], ResourceManager.Textures["TheEyeIcon"][0], ResourceManager.Fonts["FONT_TE"]),
                            _ => new(ResourceManager.Textures["Victory_C"][0], null, ResourceManager.Fonts["FONT_C"]),
                        };
                        endingScreen.Title = "Győzelem!";
                        endingScreen.Update(gameTime);
                    }
                    //
                    if (playerTurn) {
                        cardMouseControlEnabled = true;
                        //ScrapCards
                        if (PlayerCard2ScrapThisTurn != 0) {
                            PlayerCard2ScrapThisTurn = Math.Clamp(PlayerCard2ScrapThisTurn, 0, PlayerHand.Count - 1);
                            cardSelector = new(PlayerHand, PlayerCard2ScrapThisTurn, mouse, true) {
                                Title = $"Válaszd ki a kezedből kidobandó lapokat! (Pontosan {PlayerCard2ScrapThisTurn} db)"
                            };
                            cardSelector.SelectionConfirmed += ScrapOwnCardFromHandEventHandler;
                            cardSelector.HasCancelButton = false;
                            PlayerCard2ScrapThisTurn = 0;
                        }
                        //PlayCards
                        if (PlayedPile.Any(card => !card.BaseApplied)) {
                            for (int i = 0; i < PlayedPile.Count; i++) {
                                PlayCard(PlayedPile[i], true);
                                if (cardSelector is not null)
                                    break;
                            }
                        }
                        else if (PlayerHand.Count == 0) {
                            EndTurnButton.Enabled = true;
                        }
                    }
                    else {
                        cardMouseControlEnabled = false;
                        //ScrapCards
                        //AI LOGIC KELL IDE !!! (done)
                        if (EnemyCard2ScrapThisTurn != 0) {
                            EnemyCard2ScrapThisTurn = Math.Clamp(EnemyCard2ScrapThisTurn, 0, EnemyHand.Count - 1);
                            for (int i = 0; i < EnemyCard2ScrapThisTurn; i++) {
                                Card scrapped;
                                if (RandomAI)
                                    scrapped = DeckGenerator.GetCard(EnemyHand);
                                else {
                                    int scrappedIndex = 0;
                                    for (int j = 1; j < EnemyHand.Count; j++) {
                                        List<float> input = [];
                                        input.add(1);
                                        for (int k = 0; k < (int)Card.Effect.None; k++) {
                                            if (EnemyHand[scrappedIndex].CardEffect == (Card.Effect)k)
                                                input.Add(1);
                                            else
                                                input.Add(0);
                                        }
                                        input.add(1);
                                        for (int k = 0; k < (int)Card.Fraction.None; k++) {
                                            if (EnemyHand[j].CardFraction == (Card.Fraction)k)
                                                input.Add(1);
                                            else
                                                input.Add(0);
                                        }
                                        ModelOutput2 output = MLController.DiscardEngine.Predict(new ModelInput26() { Features = input.ToArray() });
                                        if (output.Prediction[0] < output.Prediction[1]) {
                                            scrappedIndex = j;
                                        }
                                    }
                                    scrapped = EnemyHand[scrappedIndex];
                                }
                                EnemyScrap.Add(scrapped);
                                EnemyHandTarget.RemoveAt(EnemyHand.IndexOf(scrapped));
                                EnemyHand.Remove(scrapped);
                            }
                            changed = true;
                            EnemyCard2ScrapThisTurn = 0;
                        }
                        //PlayCards
                        if (PlayedPile.Any(card => !card.BaseApplied)) {
                            for (int i = 0; i < PlayedPile.Count; i++) {
                                PlayCard(PlayedPile[i], false);
                            }
                        }
                        else if (EnemyHand.Count == 0) {
                            //Buy something from shop
                            List<Card> affordableCards = Shop.Where(card => card!.Price <= EnemyMoney).ToList()!;
                            ModelOutput5? strategy = null;
                            if (!RandomAI)
                                strategy = MLController.StrategyEngine.Predict(new ModelInput5() { Features = GetStrategyDistribution(true) });
                            while (affordableCards.Count > 0) {
                                Card toBuy;
                                if (RandomAI)
                                    toBuy = affordableCards[Random.Shared.Next(0, affordableCards.Count)];
                                else {
                                    List<float> maxIndexes = strategy!.Prediction.ToList();
                                    maxIndexes.Sort();
                                    maxIndexes.Reverse();
                                    int maxIndex = 0;
                                    List<Card> TBuy = [];
                                    while (TBuy.Count == 0 && maxIndex < 3) {
                                        List<int> maxIndices = maxIndexes.Select((value, index) => new { value, index })
                                            .Where(pair => pair.value == maxIndexes[maxIndex])
                                            .Select(pair => pair.index)
                                            .ToList();
                                        foreach (int index in maxIndices) {
                                            TBuy = affordableCards.Where(card => card!.CardFraction == (Card.Fraction)index).ToList()!;
                                            if (TBuy.Count > 0)
                                                break;
                                        }
                                        maxIndex++;
                                    }
                                    if (TBuy.Count == 0) {
                                        TBuy = affordableCards.Where(card => card!.CardFraction == Card.Fraction.None).ToList()!;
                                        if (TBuy.Count == 0)
                                            break;
                                    }
                                    List<float> input = [];
                                    foreach (Card card in TBuy) {
                                        input.Add(1);
                                        for (int i = 0; i < (int)Card.Effect.None; i++) {
                                            if (card.CardEffect == (Card.Effect)i)
                                                input.Add(1);
                                            else
                                                input.Add(0);
                                        }
                                    }
                                    input.AddRange(new float[78 - input.Count]);
                                    ModelOutput6 choosen = MLController.ShoppingEngine.Predict(new ModelInput78() { Features = input.ToArray() });
                                    int chosenIndex = Array.IndexOf(choosen.Prediction, choosen.Prediction.Max());
                                    toBuy = TBuy[chosenIndex];
                                }
                                BuyFromShop(toBuy, false);
                                EnemyMoney -= toBuy.Price;
                                affordableCards = Shop.Where(card => card!.Price <= EnemyMoney).ToList()!;
                            }
                            //
                            //AI LOGIC KELL IDE !!! (done)
                            ClearPlayedPile(false);
                            RefillEnemyHand();
                            EnemyMoney = 0;
                            PlayerHealth -= EnemyAttack;
                            EnemyAttack = 0;
                            playerTurn = true;
                        }
                        else {
                            PlayNext(false);
                        }
                        //
                    }
                }
            }
            //END of GamePlay Logic
            int Pdeckshowcount = PlayerDeck.Count > MAX_OFFSET_CARDS ? MAX_OFFSET_CARDS : PlayerDeck.Count;
            int Pscrapshowcount = PlayerScrap.Count > MAX_OFFSET_CARDS ? MAX_OFFSET_CARDS : PlayerScrap.Count;
            int Edeckshowcount = EnemyDeck.Count > MAX_OFFSET_CARDS ? MAX_OFFSET_CARDS : EnemyDeck.Count;
            int Escrapshowcount = EnemyScrap.Count > MAX_OFFSET_CARDS ? MAX_OFFSET_CARDS : EnemyScrap.Count;
            //deck and scrap grouping
            if (changed) {
                //set whole deck to same location without offset
                for (int i = 0; i < PlayerDeck.Count; i++) {
                    PlayerDeck[i].Rect = PlayerDeckLoc;
                }
                //set offset for first X cards
                for (int i = 0; i < Pdeckshowcount; i++) {
                    PlayerDeck[i].Rect = new Rectangle(
                        PlayerDeck[i].Rect.X - (groupingOffset * i),
                        PlayerDeck[i].Rect.Y - (groupingOffset * i),
                        PlayerDeck[i].Rect.Width,
                        PlayerDeck[i].Rect.Height);
                }
                //P_Scrap
                for (int i = 0; i < PlayerScrap.Count; i++) {
                    PlayerScrap[i].Rect = PlayerScrapLoc;
                }
                for (int i = 0; i < Pscrapshowcount; i++) {
                    PlayerScrap[i].Rect = new Rectangle(
                        PlayerScrap[i].Rect.X - (groupingOffset * i),
                        PlayerScrap[i].Rect.Y - (groupingOffset * i),
                        PlayerScrap[i].Rect.Width,
                        PlayerScrap[i].Rect.Height);
                }
                //E_Deck
                for (int i = 0; i < EnemyDeck.Count; i++) {
                    EnemyDeck[i].Rect = EnemyDeckLoc;
                }
                for (int i = 0; i < Edeckshowcount; i++) {
                    EnemyDeck[i].Rect = new Rectangle(
                        EnemyDeck[i].Rect.X - (groupingOffset * i),
                        EnemyDeck[i].Rect.Y - (groupingOffset * i),
                        EnemyDeck[i].Rect.Width,
                        EnemyDeck[i].Rect.Height);
                }
                //E_Scrap
                for (int i = 0; i < EnemyScrap.Count; i++) {
                    EnemyScrap[i].Rect = EnemyScrapLoc;
                }
                for (int i = 0; i < Escrapshowcount; i++) {
                    EnemyScrap[i].Rect = new Rectangle(
                        EnemyScrap[i].Rect.X - (groupingOffset * i),
                        EnemyScrap[i].Rect.Y - (groupingOffset * i),
                        EnemyScrap[i].Rect.Width,
                        EnemyScrap[i].Rect.Height);
                }
                changed = false;
            }
            //regular updates
            for (int i = 0; i < Pdeckshowcount; i++) {
                PlayerDeck[i].Update(gameTime);
            }
            for (int i = 0; i < Pscrapshowcount; i++) {
                PlayerScrap[i].Update(gameTime);
            }
            for (int i = 0; i < Edeckshowcount; i++) {
                EnemyDeck[i].Update(gameTime);
            }
            for (int i = 0; i < Escrapshowcount; i++) {
                EnemyScrap[i].Update(gameTime);
            }
            //icons update
            PIconboxes[0].Text = PlayerAttack.ToString();
            PIconboxes[1].Text = PlayerMoney.ToString();
            PIconboxes[2].Text = PlayerHealth.ToString();
            PIconboxes[3].Text = PlayerName;
            EIconboxes[0].Text = EnemyAttack.ToString();
            EIconboxes[1].Text = EnemyMoney.ToString();
            EIconboxes[2].Text = EnemyHealth.ToString();
            EIconboxes[3].Text = EnemyName;
            for (int i = 0; i < PIconboxes.Length; i++) {
                PIconboxes[i].Update(gameTime);
            }
            for (int i = 0; i < EIconboxes.Length; i++) {
                EIconboxes[i].Update(gameTime);
            }
            //MouseControls
            if (cardMouseControlEnabled) {
                if (previewThisCard == null && selectedCard == null) {
                    //preview
                    if (mouse.Current.RightButton == ButtonState.Pressed &&
                        mouse.Previous.RightButton == ButtonState.Released) {
                        for (int i = 0; i < PlayedPile.Count; i++) {
                            if (PlayedPile[i].Rect.Contains(mouse.GetMousePosition())) {
                                previewThis.Add(new(PlayedPile[i], PlayedPileTarget[i]));
                                previewThisCard = PlayedPileTarget[i];
                                previewThisCardOTarget = previewThisCard.MoveTarget;
                                previewThisCard.StartLocation = previewThisCard.CurrentLocation;
                                break;
                            }
                        }
                        if (previewThisCard == null) {
                            for (int i = 0; i < Shop.Length; i++) {
                                Card? item = Shop[i];
                                if (item != null && item.Rect.Contains(mouse.GetMousePosition())) {
                                    previewThis.Add(new(item, ShopTarget[i]));
                                    previewThisCard = ShopTarget[i];
                                    previewThisCardOTarget = previewThisCard.MoveTarget;
                                    previewThisCard.StartLocation = previewThisCard.CurrentLocation;
                                    break;
                                }
                            }
                        }
                        if (previewThisCard == null) {
                            for (int i = 0; i < PlayerHand.Count; i++) {
                                if (PlayerHand[i].Rect.Contains(mouse.GetMousePosition())) {
                                    previewThis.Add(new(PlayerHand[i], PlayerHandTarget[i]));
                                    previewThisCard = PlayerHandTarget[i];
                                    previewThisCardOTarget = previewThisCard.MoveTarget;
                                    previewThisCard.StartLocation = previewThisCard.CurrentLocation;
                                    break;
                                }
                            }
                        }
                        if (previewThisCard == null) {
                            for (int i = 0; i < EnemyHand.Count; i++) {
                                if (EnemyHand[i].Rect.Contains(mouse.GetMousePosition())) {
                                    previewThis.Add(new(EnemyHand[i], EnemyHandTarget[i]));
                                    previewThisCard = EnemyHandTarget[i];
                                    previewThisCardOTarget = previewThisCard.MoveTarget;
                                    previewThisCard.StartLocation = previewThisCard.CurrentLocation;
                                    break;
                                }
                            }
                        }
                        if (previewThisCard == null) {
                            if (PlayerScrapLoc.Contains(mouse.GetMousePosition()) && PlayerScrap.Count != 0) {
                                cardSelector = new(PlayerScrap, 0, mouse) {
                                    Title = "Játékos eldobott kártyái",
                                    HasOkButton = false
                                };
                                cardSelector.Update(gameTime);
                            }
                            else if (EnemyScrapLoc.Contains(mouse.GetMousePosition()) && EnemyScrap.Count != 0) {
                                cardSelector = new(EnemyScrap, 0, mouse) {
                                    Title = "Ellenfél eldobott kártyái",
                                    HasOkButton = false
                                };
                                cardSelector.Update(gameTime);
                            }
                            else if (PlayerDeckLoc.Contains(mouse.GetMousePosition()) && PlayerDeck.Count != 0) {
                                List<Card> Localcards = [];
                                foreach (Card card in PlayerDeck) {
                                    Localcards.Add((Card)card.Clone());
                                }
                                DeckGenerator.ShuffleDeck(Localcards);
                                cardSelector = new(Localcards, 0, mouse) {
                                    Title = "Játékos pakliának kártyái (keverve)",
                                    HasOkButton = false
                                };
                                cardSelector.Update(gameTime);
                            }
                        }
                    }
                    //Select
                    else if (mouse.Current.LeftButton == ButtonState.Pressed &&
                             mouse.Previous.LeftButton == ButtonState.Released) {
                        for (int i = 0; i < Shop.Length; i++) {
                            if (Shop[i] != null && Shop[i]!.Rect.Contains(mouse.GetMousePosition())) {
                                selectedCard = Shop[i];
                                previewThis.Add(new(Shop[i]!, ShopTarget[i]));
                                ShowPlayerHand = true;
                                break;
                            }
                        }
                        if (selectedCard == null) {
                            for (int i = 0; i < PlayerHand.Count; i++) {
                                if (PlayerHand[i].Rect.Contains(mouse.GetMousePosition())) {
                                    selectedCard = PlayerHand[i];
                                    previewThis.Add(new(PlayerHand[i], PlayerHandTarget[i]));
                                    ShowPlayedPile = true;
                                    break;
                                }
                            }
                        }
                    }
                }
                else {
                    //deselect preview
                    if (previewThisCard != null) {
                        if ((mouse.Current.RightButton == ButtonState.Pressed &&
                            mouse.Previous.RightButton == ButtonState.Released) ||
                            (mouse.Current.LeftButton == ButtonState.Pressed &&
                            mouse.Previous.LeftButton == ButtonState.Released)) {
                            previewThisCard.StartLocation = previewThisCard.CurrentLocation;
                            previewThisCard.MoveTarget = previewThisCardOTarget;
                            previewThisCard = null;
                        }
                        else {
                            if (previewThisCard.MoveTarget != previewRect) {
                                previewThisCard.MoveTarget = previewRect;
                            }
                        }
                    }
                    //deselect selected
                    if (selectedCard != null) {
                        if (mouse.Current.LeftButton == ButtonState.Released &&
                            mouse.Previous.LeftButton == ButtonState.Pressed) {
                            previewThis[^1].Item2.StartLocation = selectedCard.Rect;
                            if (PlayerHand.Contains(selectedCard)) {
                                if (PlayedPileLoc.Contains(mouse.GetMousePosition()))
                                    AddToPlayedPile(selectedCard, PlayerHandTarget[PlayerHand.IndexOf(selectedCard)], true);
                            }
                            else {
                                if (PlayerHandLoc.Contains(mouse.GetMousePosition())) {
                                    if (PlayerMoney >= selectedCard.Price) {
                                        PlayerMoney -= selectedCard.Price;
                                        BuyFromShop(selectedCard, true);
                                        previewThis.RemoveAt(previewThis.Count - 1);
                                        changed = true;
                                    }
                                }
                            }
                            selectedCard = null;
                            ShowPlayedPile = false;
                            ShowPlayerHand = false;
                        }
                        else {
                            //move card with mouse
                            Rectangle mouseRect = new(
                                mouse.GetMousePosition().X - (selectedCard.Rect.Width / 2),
                                mouse.GetMousePosition().Y - (selectedCard.Rect.Height / 2),
                                selectedCard.Rect.Width,
                                selectedCard.Rect.Height);
                            selectedCard.Rect = mouseRect;
                        }
                    }
                }
                EndTurnButton.Update(gameTime);
            }
            //CARDS with targets
            //TARGETS
            foreach (ObjectTransform item in ShopTarget) {
                item.NextStep(gameTime);
            }
            foreach (ObjectTransform item in PlayerHandTarget) {
                item.NextStep(gameTime);
            }
            foreach (ObjectTransform item in EnemyHandTarget) {
                item.NextStep(gameTime);
            }
            foreach (ObjectTransform item in PlayedPileTarget) {
                item.NextStep(gameTime);
            }
            //CARDS
            //hand cards
            for (int i = 0; i < PlayerHand.Count; i++) {
                if (selectedCard != PlayerHand[i])
                    PlayerHand[i].Rect = PlayerHandTarget[i].CurrentLocation;
            }
            for (int i = 0; i < EnemyHand.Count; i++) {
                EnemyHand[i].Rect = EnemyHandTarget[i].CurrentLocation;
            }
            //shop cards
            for (int i = 0; i < Shop.Length; i++) {
                if (Shop[i] != null && selectedCard != Shop[i]) {
                    Shop[i]!.Rect = ShopTarget[i].CurrentLocation;
                }
            }
            //played pile
            for (int i = 0; i < PlayedPile.Count; i++) {
                PlayedPile[i].Rect = PlayedPileTarget[i].CurrentLocation;
            }
            //preview
            if (previewThis.Count > 0 && selectedCard == null) {
                for (int i = 0; i < previewThis.Count; i++) {
                    if (!previewThis[i].Item2.IsTransforming &&
                        previewThis[i].Item2.MoveTarget != previewRect) {
                        previewThis.RemoveAt(i);
                    }
                }
            }
            //CARDS UPDATE
            //hand cards
            for (int i = 0; i < PlayerHand.Count; i++) {
                PlayerHand[i].Update(gameTime);
            }
            for (int i = 0; i < EnemyHand.Count; i++) {
                EnemyHand[i].Update(gameTime);
            }
            //shop cards
            for (int i = 0; i < Shop.Length; i++) {
                if (Shop[i] != null) {
                    Shop[i]!.Update(gameTime);
                }
            }
            //played pile
            for (int i = 0; i < PlayedPile.Count; i++) {
                PlayedPile[i].Update(gameTime);
            }
        }

        public void Draw(GameTime gameTime, SpriteBatch spriteBatch)
        {
            int Pdeckshowcount = PlayerDeck.Count > MAX_OFFSET_CARDS ? MAX_OFFSET_CARDS : PlayerDeck.Count;
            int Pscrapshowcount = PlayerScrap.Count > MAX_OFFSET_CARDS ? MAX_OFFSET_CARDS : PlayerScrap.Count;
            int Edeckshowcount = EnemyDeck.Count > MAX_OFFSET_CARDS ? MAX_OFFSET_CARDS : EnemyDeck.Count;
            int Escrapshowcount = EnemyScrap.Count > MAX_OFFSET_CARDS ? MAX_OFFSET_CARDS : EnemyScrap.Count;
            overlay ??= ResourceManager.GetColor(Color.Orange, spriteBatch);
            //player icons
            spriteBatch.Draw(Icons[0], PIconsLoc[0], Color.White);
            spriteBatch.Draw(Icons[1], PIconsLoc[1], Color.White);
            PIconboxes[0].Draw(gameTime, spriteBatch);
            spriteBatch.Draw(Icons[2], PIconsLoc[2], Color.White);
            spriteBatch.Draw(Icons[3], PIconsLoc[3], Color.White);
            PIconboxes[1].Draw(gameTime, spriteBatch);
            spriteBatch.Draw(Icons[4], PIconsLoc[4], Color.White);
            PIconboxes[2].Draw(gameTime, spriteBatch);
            PIconboxes[3].Draw(gameTime, spriteBatch);
            //enemy icons
            spriteBatch.Draw(Icons[0], EIconsLoc[0], Color.White);
            spriteBatch.Draw(Icons[1], EIconsLoc[1], Color.White);
            EIconboxes[0].Draw(gameTime, spriteBatch);
            spriteBatch.Draw(Icons[2], EIconsLoc[2], Color.White);
            spriteBatch.Draw(Icons[3], EIconsLoc[3], Color.White);
            EIconboxes[1].Draw(gameTime, spriteBatch);
            spriteBatch.Draw(Icons[4], EIconsLoc[4], Color.White);
            EIconboxes[2].Draw(gameTime, spriteBatch);
            EIconboxes[3].Draw(gameTime, spriteBatch);
            //deck and scrap
            for (int i = 0; i < Pdeckshowcount; i++) {
                PlayerDeck[i].Draw(gameTime, spriteBatch);
            }
            for (int i = 0; i < Pscrapshowcount; i++) {
                PlayerScrap[i].Draw(gameTime, spriteBatch);
            }
            for (int i = 0; i < Edeckshowcount; i++) {
                EnemyDeck[i].Draw(gameTime, spriteBatch);
            }
            for (int i = 0; i < Escrapshowcount; i++) {
                EnemyScrap[i].Draw(gameTime, spriteBatch);
            }
            EndTurnButton.Draw(gameTime, spriteBatch);
            //hand cards
            if (ShowPlayerHand) {
                spriteBatch.Draw(overlay, PlayerHandLoc, new(255, 255, 255, 64));
            }
            for (int i = 0; i < PlayerHand.Count; i++) {
                if (previewThis.All(item => item.Item1 != PlayerHand[i]))
                    PlayerHand[i].Draw(gameTime, spriteBatch);
            }
            for (int i = 0; i < EnemyHand.Count; i++) {
                if (previewThis.All(item => item.Item1 != EnemyHand[i]))
                    EnemyHand[i].Draw(gameTime, spriteBatch);
            }
            //shop cards
            for (int i = 0; i < Shop.Length; i++) {
                if (Shop[i] != null) {
                    if (previewThis.All(item => item.Item1 != Shop[i]))
                        Shop[i]!.Draw(gameTime, spriteBatch);
                }
            }
            //played pile
            if (ShowPlayedPile) {
                spriteBatch.Draw(overlay, PlayedPileLoc, new(255, 255, 255, 64));
            }
            for (int i = 0; i < PlayedPile.Count; i++) {
                if (previewThis.All(item => item.Item1 != PlayedPile[i]))
                    PlayedPile[i].Draw(gameTime, spriteBatch);
            }
            //preview
            if (previewThis.Count > 0) {
                foreach (var item in previewThis) {
                    item.Item1.Draw(gameTime, spriteBatch);
                }
            }
            //selector
            if (endingScreen is null)
                cardSelector?.Draw(gameTime, spriteBatch);
            //ending
            endingScreen?.Draw(gameTime, spriteBatch);
        }
    }
}
