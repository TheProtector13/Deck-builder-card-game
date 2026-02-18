using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime;
using System.Threading.Tasks;
using CardGame.TCP;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using static CardGame.TCP.MessagePackHelper;

#nullable enable
namespace CardGame {
    internal class ForeGround_Multi : IDrawable, IDisposable {
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
        private readonly Button PlayAllCardsButton;
        private readonly TextBox ToolTipsBox;
        private Card.HoveredObject? PrevHObject = null;
        private Texture2D? TurnIndicatorColor;
        private readonly Rectangle[] TurnIndicatorLocs;
        private CardSelectorWindow? cardSelector = null;
        private EndingScreen? endingScreen = null;
        private bool changed = true;

        private readonly ParallelOptions ParallelOptions = new() { MaxDegreeOfParallelism = Environment.ProcessorCount };

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
        private bool playAllCards = false;
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

        //SFX
        private readonly SoundEffect flipSFX, shuffleSFX, explodeSFX, explodeEXT_SFX, defeatSFX, victorySFX;
        private readonly SoundEffect buySFX, scrapSFX, stealSFX, dropcardSFX;

        //TCP
        private readonly TcpTlsPeer peer;
        private bool enemyendturn = false;
        private readonly List<Card> EnemyUnknownDeck = [];
        private readonly List<Card> PlayerUnknownDeck = [];

        private ForeGround_Multi() => throw new NotImplementedException();
        public ForeGround_Multi(BackGround bg, TcpTlsPeer peer)
        {
            this.peer = peer;
            playerTurn = peer.IsHost;
            PlayerName = UDP_Broadcast_Helper.UserName;
            EnemyName = UDP_Broadcast_Helper.ClientName;
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
            TurnIndicatorLocs = new Rectangle[2];
            ToolTipsBox = new(rect, font) { BGColor = new(191, 191, 191, 192) };
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
            PlayAllCardsButton = new Button(ResourceManager.Textures["PlayAll_"], mouse) {
                Enabled = false
            };
            PlayAllCardsButton.Click += PlayAllEventHandler;
            //SFX load
            flipSFX = ResourceManager.SoundEffects["Flip"];
            shuffleSFX = ResourceManager.SoundEffects["Shuffle"];
            explodeSFX = ResourceManager.SoundEffects["Explode"];
            explodeEXT_SFX = ResourceManager.SoundEffects["BGCrack"];
            defeatSFX = ResourceManager.SoundEffects["Defeat"];
            victorySFX = ResourceManager.SoundEffects["Victory"];
            buySFX = ResourceManager.SoundEffects["Buy"];
            scrapSFX = ResourceManager.SoundEffects["Scrap"];
            stealSFX = ResourceManager.SoundEffects["Steal"];
            dropcardSFX = ResourceManager.SoundEffects["Dropcard"];
            CalculateLayout();
            if (peer.IsHost) {
                RefillShop();
                RefillPlayerHand();
                RefillEnemyHand();
            }
            //ITT NEM UPDATEOLJUK A BG-T, AZT A GAME1 CSINÁLJA
        }

        private void AddToPlayedPile(Card card, ObjectTransform cardTrans, bool player = true)
        {
            if (!player)
                flipSFX.Play(GameSettings.SFXVolume, 0, 0);
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
                        peer.SendAsync(new ActionPayload(ActionType.Play, card.GetCardDetails(), CryptographyHelper.NowMs()));
                        return;
                    }
                }
            }
            return;
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
                        scrapSFX.Play(GameSettings.SFXVolume, 0, 0);
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
                        } //NO OP
                        break;
                    case Card.Effect.ScrapFromShop:
                        if (player) {
                            cardSelector = new(Shop.ToList()!, card.EffectAmount, mouse, true) {
                                HasCancelButton = false,
                                Title = $"Válaszd ki a boltból eltávolítandó lapokat! (Pontosan {card.EffectAmount} db)"
                            };
                            cardSelector.SelectionConfirmed += ScrapFromShopEventHandler;
                        } //NO OP
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
                        if (peer.IsHost) {
                            List<Card> cards3 = player ? EnemyHand : PlayerHand;
                            if (cards3.Any(tcard => tcard.CardEffect == Card.Effect.AntiShow)) {
                                if (player)
                                    cards3.Where(tcard => tcard.CardEffect == Card.Effect.AntiShow).ToList().ForEach(tcard => tcard.Flipped = true);
                                else {
                                    List<Card> toflip = cards3.Where(tcard => tcard.CardEffect == Card.Effect.AntiShow).ToList();
                                    // SEND
                                    peer.SendAsync(new ActionPayload(ActionType.Flip, toflip[0].GetCardDetails(), CryptographyHelper.NowMs()));
                                }
                                break;
                            }
                            for (int i = 0; i < card.EffectAmount; i++) {
                                if (cards3.Count == 0) {
                                    break;
                                }
                                Card stolen = DeckGenerator.GetCard(cards3);
                                stolenCards.Add(stolen);
                                stealSFX.Play(GameSettings.SFXVolume, 0, 0);
                                AddToPlayedPile(stolen, player ? EnemyHandTarget[EnemyHand.IndexOf(stolen)] : PlayerHandTarget[PlayerHand.IndexOf(stolen)], !player);
                                // SEND
                                peer.SendAsync(new ActionPayload(ActionType.Steal, stolen.GetCardDetails(), CryptographyHelper.NowMs()));
                            }
                        }
                        break;
                    case Card.Effect.ScrapEnemyCard:
                        if (player)
                            EnemyCard2ScrapThisTurn += card.EffectAmount;
                        else
                            PlayerCard2ScrapThisTurn += card.EffectAmount;
                        dropcardSFX.Play(GameSettings.SFXVolume, 0, 0);
                        break;
                    case Card.Effect.DrawCard:
                        if (peer.IsHost) {
                            if (player)
                                for (int i = 0; i < card.EffectAmount; i++) {
                                    PlayerDrawCard();
                                }
                            else
                                for (int i = 0; i < card.EffectAmount; i++) {
                                    EnemyDrawCard();
                                }
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
                scrapSFX.Play(GameSettings.SFXVolume, 0, 0);
                PlayerScrap.Remove(card);
                // SEND
                peer.SendAsync(new ActionPayload(ActionType.Scrap, card.GetCardDetails(), CryptographyHelper.NowMs()));
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
                flipSFX.Play(GameSettings.SFXVolume, 0, 0);
                // SEND
                peer.SendAsync(new ActionPayload(ActionType.ScrapFromHand, card.GetCardDetails(), CryptographyHelper.NowMs()));
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
                        scrapSFX.Play(GameSettings.SFXVolume, 0, 0);
                        if (!peer.IsHost)
                            // SEND
                            peer.SendAsync(new ActionPayload(ActionType.ScrapFromShop, card.GetCardDetails(), CryptographyHelper.NowMs()));
                        break;
                    }
                }
            }
            if (peer.IsHost)
                RefillShop();
        }

        private void EndTurnEventHandler(object? sender, EventArgs e)
        {
            ClearPlayedPile(true);
            // SEND
            peer.SendAsync(new ActionPayload(ActionType.EndTurn, null, CryptographyHelper.NowMs()));
            if (peer.IsHost)
                RefillPlayerHand();
            PlayerMoney = 0;
            EnemyHealth -= PlayerAttack;
            if (PlayerAttack > 0)
                explodeSFX.Play(GameSettings.SFXVolume, 0, 0);
            PlayerAttack = 0;
            playerTurn = false;
            EndTurnButton.Enabled = false;
        }

        private void PlayAllEventHandler(object? sender, EventArgs e)
        {
            playAllCards = true;
            PlayAllCardsButton.Enabled = false;
        }

        //######## END ####################

        private Tuple<float[], int[]> GetFractionDistribution()
        {
            int allcards = 0;
            int[] cards = new int[6];
            float[] distribution = new float[6];
            foreach (var card in PlayerHand) {
                cards[(int)card.CardFraction]++;
                allcards++;
            }
            foreach (var card in PlayerDeck) {
                cards[(int)card.CardFraction]++;
                allcards++;
            }
            foreach (var card in PlayerScrap) {
                cards[(int)card.CardFraction]++;
                allcards++;
            }
            foreach (var card in PlayedPile) {
                if (stolenCards.Contains(card))
                    continue;
                cards[(int)card.CardFraction]++;
                allcards++;
            }
            if (allcards > 0) {
                for (int i = 0; i < distribution.Length; i++) {
                    distribution[i] = (float)cards[i] / allcards;
                }
            }
            else {
                for (int i = 0; i < distribution.Length; i++) {
                    distribution[i] = 0f;
                }
            }
            return new(distribution, cards);
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
            buySFX.Play(GameSettings.SFXVolume, 0, 0);
            // SEND
            peer.SendAsync(new ActionPayload(ActionType.Buy, card.GetCardDetails(), CryptographyHelper.NowMs()));
            if (peer.IsHost)
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
            // SEND
            CardDetails[] deck = PlayerDeck.Select((card) => card.GetCardDetails()).ToArray();
            peer.SendAsync(new CardListPayload(true, CardType.Deck, deck, CryptographyHelper.NowMs()));
            CardDetails[] scrap = PlayerScrap.Select((card) => card.GetCardDetails()).ToArray();
            peer.SendAsync(new CardListPayload(true, CardType.Scrap, scrap, CryptographyHelper.NowMs()));
            CardDetails[] hand = PlayerHand.Select((card) => card.GetCardDetails()).ToArray();
            peer.SendAsync(new CardListPayload(true, CardType.Hand, hand, CryptographyHelper.NowMs()));
            //
            shuffleSFX.Play(GameSettings.SFXVolume, 0, 0);
            changed = true;
        }

        private void PlayerDrawCard()
        {
            if (PlayerDeck.Count == 0) {
                PlayerDeck.AddRange(PlayerScrap);
                PlayerScrap.Clear();
                DeckGenerator.ShuffleDeck(PlayerDeck);
                // SEND
                CardDetails[] deck = PlayerDeck.Select((card) => card.GetCardDetails()).ToArray();
                peer.SendAsync(new CardListPayload(true, CardType.Deck, deck, CryptographyHelper.NowMs()));
                CardDetails[] scrap = PlayerScrap.Select((card) => card.GetCardDetails()).ToArray();
                peer.SendAsync(new CardListPayload(true, CardType.Scrap, scrap, CryptographyHelper.NowMs()));
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
            flipSFX.Play(GameSettings.SFXVolume, 0, 0);
            // SEND
            peer.SendAsync(new ActionPayload(ActionType.Draw, PlayerHand[^1].GetCardDetails(), CryptographyHelper.NowMs()));
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
            // SEND
            CardDetails[] deck = EnemyDeck.Select((card) => card.GetCardDetails()).ToArray();
            peer.SendAsync(new CardListPayload(false, CardType.Deck, deck, CryptographyHelper.NowMs()));
            CardDetails[] scrap = EnemyScrap.Select((card) => card.GetCardDetails()).ToArray();
            peer.SendAsync(new CardListPayload(false, CardType.Scrap, scrap, CryptographyHelper.NowMs()));
            CardDetails[] hand = EnemyHand.Select((card) => card.GetCardDetails()).ToArray();
            peer.SendAsync(new CardListPayload(false, CardType.Hand, hand, CryptographyHelper.NowMs()));
            //
            shuffleSFX.Play(GameSettings.SFXVolume, 0, 0);
            changed = true;
        }

        private void EnemyDrawCard()
        {
            if (EnemyDeck.Count == 0) {
                EnemyDeck.AddRange(EnemyScrap);
                EnemyScrap.Clear();
                DeckGenerator.ShuffleDeck(EnemyDeck);
                // SEND
                CardDetails[] deck = EnemyDeck.Select((card) => card.GetCardDetails()).ToArray();
                peer.SendAsync(new CardListPayload(false, CardType.Deck, deck, CryptographyHelper.NowMs()));
                CardDetails[] scrap = EnemyScrap.Select((card) => card.GetCardDetails()).ToArray();
                peer.SendAsync(new CardListPayload(false, CardType.Scrap, scrap, CryptographyHelper.NowMs()));
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
            flipSFX.Play(GameSettings.SFXVolume, 0, 0);
            // SEND
            peer.SendAsync(new ActionPayload(ActionType.Draw, EnemyHand[^1].GetCardDetails(), CryptographyHelper.NowMs()));
            changed = true;
        }

        private void RefillShop()
        {
            if (Shop[0] == null) {
                Shop[0] = DeckGenerator.GetMoneyCard();
                Shop[0]!.Flipped = false;
                Shop[0]!.RenderPrice = true;
                ShopTarget[0].StartLocation = globalCardStart;
                previewThis.Add(new(Shop[0]!, ShopTarget[0]));
            }
            for (int i = 1; i < Shop.Length; i++) {
                if (Shop[i] == null) {
                    if (GameDeck.Count > 0) {
                        Shop[i] = DeckGenerator.GetCard(GameDeck);
                        GameDeck.Remove(Shop[i]!);
                        Shop[i]!.Flipped = false;
                        Shop[i]!.RenderPrice = true;
                        ShopTarget[i].StartLocation = globalCardStart;
                        previewThis.Add(new(Shop[i]!, ShopTarget[i]));
                    }
                    else {
                        Shop[i] = DeckGenerator.GetMoneyCard();
                        Shop[i]!.Flipped = false;
                        Shop[i]!.RenderPrice = true;
                        ShopTarget[i].StartLocation = globalCardStart;
                        previewThis.Add(new(Shop[i]!, ShopTarget[i]));
                    }
                }
            }
            // SEND
            CardDetails[] shop = Shop.Select((card) => card!.GetCardDetails()).ToArray();
            peer.SendAsync(new CardListPayload(true, CardType.Shop, shop, CryptographyHelper.NowMs()));
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

        private bool RefillPlayerHand(CardDetails[] cards)
        {
            int handCardWidth = (int)MathF.Round(PlayerHandLoc.Height * CARD_WIDTH_SCALE);
            int handWidthOffset = (int)MathF.Round((PlayerHandLoc.Width - (5f * handCardWidth)) / 6);
            if (PlayerHand.Count == 0) {
                foreach (CardDetails card in cards) {
                    Card[] deck = PlayerDeck.Where(c => c.GetCardDetails().Equals(card)).ToArray();
                    Card[] deck2 = PlayerUnknownDeck.Where(c => c.GetCardDetails().Equals(card)).ToArray();
                    Card[] deck3 = PlayerScrap.Where(c => c.GetCardDetails().Equals(card)).ToArray();
                    if (deck2.Length != 0) {
                        PlayerHand.Add(deck2[0]);
                        PlayerUnknownDeck.Remove(deck2[0]);
                    }
                    else if (deck.Length != 0) {
                        PlayerHand.Add(deck[0]);
                        PlayerDeck.Remove(deck[0]);
                    }
                    else if (deck3.Length != 0) {
                        PlayerHand.Add(deck3[0]);
                        PlayerScrap.Remove(deck3[0]);
                    }
                    else {
                        return true;
                    }
                    PlayerHand[^1].Flipped = false;
                    PlayerHandTarget.Add(new(
                        PlayerDeckLoc,
                        new Rectangle(
                            PlayerHandLoc.X + Math.Abs(handWidthOffset) + ((PlayerHand.Count - 1) * handCardWidth) + ((PlayerHand.Count - 1) * handWidthOffset),
                            PlayerHandLoc.Y,
                            handCardWidth,
                            PlayerHandLoc.Height)));
                }
            }
            else {
                Card[] deck2 = PlayerUnknownDeck.Where(c => c.GetCardDetails().Equals(cards[^1])).ToArray();
                if (deck2.Length != 0) {
                    PlayerHand.Add(deck2[0]);
                    PlayerUnknownDeck.Remove(deck2[0]);
                }
                else if (PlayerDeck[^1].GetCardDetails().Equals(cards[^1])) {
                    PlayerHand.Add(PlayerDeck[^1]);
                    PlayerDeck.RemoveAt(PlayerDeck.Count - 1);
                }
                else {
                    return true;
                }
                PlayerHand[^1].Flipped = false;
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
            }
            shuffleSFX.Play(GameSettings.SFXVolume, 0, 0);
            changed = true;
            return false;
        }

        private bool RefillEnemyHand(CardDetails[] cards)
        {
            int handCardWidth = (int)MathF.Round(EnemyHandLoc.Height * CARD_WIDTH_SCALE);
            int handWidthOffset = (int)MathF.Round((EnemyHandLoc.Width - (5f * handCardWidth)) / 6);
            if (EnemyHand.Count == 0) {
                foreach (CardDetails card in cards) {
                    Card[] deck = EnemyDeck.Where(c => c.GetCardDetails().Equals(card)).ToArray();
                    Card[] deck2 = EnemyUnknownDeck.Where(c => c.GetCardDetails().Equals(card)).ToArray();
                    Card[] deck3 = EnemyScrap.Where(c => c.GetCardDetails().Equals(card)).ToArray();
                    if (deck2.Length != 0) {
                        EnemyHand.Add(deck2[0]);
                        EnemyUnknownDeck.Remove(deck2[0]);
                    }
                    else if (deck.Length != 0) {
                        EnemyHand.Add(deck[0]);
                        EnemyDeck.Remove(deck[0]);
                    }
                    else if (deck3.Length != 0) {
                        EnemyHand.Add(deck3[0]);
                        EnemyScrap.Remove(deck3[0]);
                    }
                    else {
                        return true;
                    }
                    EnemyHand[^1].Flipped = true;
                    EnemyHandTarget.Add(new(
                    EnemyDeckLoc,
                    new Rectangle(
                        EnemyHandLoc.X + Math.Abs(handWidthOffset) + ((EnemyHand.Count - 1) * handCardWidth) + ((EnemyHand.Count - 1) * handWidthOffset),
                        EnemyHandLoc.Y,
                        handCardWidth,
                        EnemyHandLoc.Height)));
                }
            }
            else {
                Card[] deck2 = EnemyUnknownDeck.Where(c => c.GetCardDetails().Equals(cards[^1])).ToArray();
                if (deck2.Length != 0) {
                    EnemyHand.Add(deck2[0]);
                    EnemyUnknownDeck.Remove(deck2[0]);
                }
                else if (EnemyDeck[^1].GetCardDetails().Equals(cards[^1])) {
                    EnemyHand.Add(EnemyDeck[^1]);
                    EnemyDeck.RemoveAt(EnemyDeck.Count - 1);
                }
                else {
                    return true;
                }
                EnemyHand[^1].Flipped = true;
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
            }
            shuffleSFX.Play(GameSettings.SFXVolume, 0, 0);
            changed = true;
            return false;
        }

        private void RefillShop(CardDetails[] cards)
        {
            if (Shop[0] == null || !Shop[0]!.GetCardDetails().Equals(cards[0])) {
                Shop[0] = DeckGenerator.GetMoneyCards().Where(card => card.GetCardDetails().Equals(cards[0])).ToArray()[0];
                Shop[0]!.Flipped = false;
                Shop[0]!.RenderPrice = true;
                ShopTarget[0].StartLocation = globalCardStart;
                previewThis.Add(new(Shop[0]!, ShopTarget[0]));
            }
            for (int i = 1; i < Shop.Length; i++) {
                Card[] Deck1 = DeckGenerator.GetMoneyCards().Where(card => card.GetCardDetails().Equals(cards[i])).ToArray();
                if (Shop[i] == null) {
                    Shop[i] = Deck1.Length != 0 ? Deck1[0] : GameDeck.Where(card => card.GetCardDetails().Equals(cards[i])).ToArray()[0];
                    GameDeck.Remove(Shop[i]!);
                    Shop[i]!.Flipped = false;
                    Shop[i]!.RenderPrice = true;
                    ShopTarget[i].StartLocation = globalCardStart;
                    previewThis.Add(new(Shop[i]!, ShopTarget[i]));
                }
                else if (!Shop[i]!.GetCardDetails().Equals(cards[i])) {
                    if (Shop[i]!.CardFraction != Card.Fraction.None)
                        GameDeck.Add(Shop[i]!);
                    Shop[i] = Deck1.Length != 0 ? Deck1[0] : GameDeck.Where(card => card.GetCardDetails().Equals(cards[i])).ToArray()[0];
                    GameDeck.Remove(Shop[i]!);
                    Shop[i]!.Flipped = false;
                    Shop[i]!.RenderPrice = true;
                    ShopTarget[i].StartLocation = globalCardStart;
                    previewThis.Add(new(Shop[i]!, ShopTarget[i]));
                }
            }
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
            ToolTipsBox.Rect = new(previewRect.Right + startX,
                              startX,
                              DisplayInfo.ScreenWidth - previewRect.Right - (2 * startX),
                              previewHeight / 2);
            ToolTipsBox.SizeOffset = (DisplayInfo.ScreenWidth - previewRect.Right - (2f * startX)) * 0.05f;
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
            TurnIndicatorLocs[1] = new(EIconsLoc[4].Right + 5, EIconsLoc[4].Y, 10, EIconsLoc[4].Height);
            int startPIconY = PlayedPileLoc.Y + (PlayedPileLoc.Height / 2) - iconheighthalf;
            PIconsLoc[0] = new(startIconX, startPIconY, iconheight, iconheight);
            PIconsLoc[1] = new(startIconX - 5, startPIconY - 5, iconheight + 10, iconheight + 10);
            PIconsLoc[2] = new(startIconX + iconheight + 5 + groupingOffset, startPIconY, iconheight, iconheight);
            PIconsLoc[3] = new(PIconsLoc[2].X - 5, startPIconY + iconheighthalf, iconheighthalf + 5, iconheighthalf + 5);
            PIconsLoc[4] = new(PIconsLoc[2].Right + groupingOffset, startPIconY + iconheighthalf - (hpiconHeight / 2), hpiconWidth, hpiconHeight);
            TurnIndicatorLocs[0] = new(PIconsLoc[4].Right + 5, PIconsLoc[4].Y, 10, PIconsLoc[4].Height);
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
            PlayAllCardsButton.Size = new(ETHeight);
            PlayAllCardsButton.Location = new(EndTurnButton.Location.X - groupingOffset - ETHeight, EndTurnButton.Location.Y);
        }

        public void Update(GameTime gameTime)
        {
            mouse.Update(Mouse.GetState());
            if (endingScreen != null) {
                cardMouseControlEnabled = false;
                endingScreen.Update(gameTime);
                KeyboardState keyboardState = Keyboard.GetState();
                if ((mouse.Previous.LeftButton == ButtonState.Released && mouse.Current.LeftButton == ButtonState.Pressed) ||
                    (mouse.Previous.RightButton == ButtonState.Released && mouse.Current.RightButton == ButtonState.Pressed) ||
                    (mouse.Previous.MiddleButton == ButtonState.Released && mouse.Current.MiddleButton == ButtonState.Pressed) ||
                    keyboardState.IsKeyDown(Keys.Enter) || keyboardState.IsKeyDown(Keys.Escape)) {
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
                if (PlayedPileTarget.TrueForAll(i => i.IsTransforming == false) &&
                    Array.TrueForAll(ShopTarget, i => i.IsTransforming == false) &&
                    EnemyHandTarget.TrueForAll(i => i.IsTransforming == false) &&
                    PlayerHandTarget.TrueForAll(i => i.IsTransforming == false)) {
                    //GC
                    if (gameTime.TotalGameTime - LastGC > TimeSpan.FromSeconds(5) && selectedCard is null) {
                        ResourceManager.ResetFonts();
                        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, false, true);
                        LastGC = gameTime.TotalGameTime;
                    }
                    //ESC Check
                    if (Keyboard.GetState().IsKeyDown(Keys.Escape)) {
                        WINNER = GameWinner.Enemy;
                    }
                    //Disconnection Check
                    if (!peer.IsConnected) {
                        WINNER = GameWinner.Player;
                    }
                    //EndingCheck
                    if (PlayerHealth <= 0) {
                        endingScreen = new(ResourceManager.Textures["Defeat"][0], null, ResourceManager.Fonts["FONT_C"]) {
                            Title = "Vereség!"
                        };
                        MusicPlayer.Mute();
                        defeatSFX.Play(GameSettings.SFXVolume, 0, 0);
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
                        MusicPlayer.Mute();
                        victorySFX.Play(GameSettings.SFXVolume, 0, 0);
                        endingScreen.Update(gameTime);
                    }
                    // TCP
                    while (peer.TryDequeueOldest() is ReceivedPacket packet) { //ha nincs csomag null
                        object p = packet.Payload;
                        if (peer.IsHost) {
                            //HOST
                            if (p is ActionPayload action) {
                                bool cheating = false;
                                switch (action.Action) {
                                    case ActionType.Play:
                                        Card[] cardtoplay = EnemyHand.Where((card) => card.GetCardDetails().Equals(action.Card)).ToArray();
                                        if (cardtoplay.Length == 0) {
                                            cheating = true;
                                            break;
                                        }
                                        AddToPlayedPile(cardtoplay[0], EnemyHandTarget[EnemyHand.IndexOf(cardtoplay[0])], false);
                                        break;
                                    case ActionType.Draw:
                                        cheating = RefillEnemyHand([(CardDetails)action.Card!]);
                                        break;
                                    case ActionType.Scrap:
                                        Card[] cardtoscrap = EnemyScrap.Where((card) => card.GetCardDetails().Equals(action.Card)).ToArray();
                                        if (cardtoscrap.Length == 0) {
                                            cheating = true;
                                            break;
                                        }
                                        int scrapindex = EnemyScrap.IndexOf(cardtoscrap[0]);
                                        if (EnemyScrap[scrapindex].CardFraction != Card.Fraction.None)
                                            GameDeck.Add(EnemyScrap[scrapindex]);
                                        EnemyScrap.RemoveAt(scrapindex);
                                        scrapSFX.Play(GameSettings.SFXVolume, 0, 0);
                                        changed = true;
                                        break;
                                    case ActionType.ScrapFromHand:
                                        Card[] cardtoscrap3 = EnemyHand.Where((card) => card.GetCardDetails().Equals(action.Card)).ToArray();
                                        if (cardtoscrap3.Length == 0) {
                                            cheating = true;
                                            break;
                                        }
                                        EnemyScrap.Add(cardtoscrap3[0]);
                                        EnemyHandTarget.RemoveAt(EnemyHand.IndexOf(cardtoscrap3[0]));
                                        EnemyHand.Remove(cardtoscrap3[0]);
                                        changed = true;
                                        break;
                                    case ActionType.ScrapFromShop:
                                        Card[] cardtoscrap2 = Shop.Where((card) => card.GetCardDetails().Equals(action.Card)).ToArray();
                                        if (cardtoscrap2.Length == 0) {
                                            cheating = true;
                                            break;
                                        }
                                        int scrapindex2 = Array.IndexOf(Shop, cardtoscrap2[0]);
                                        if (Shop[scrapindex2] != null) {
                                            if (Shop[scrapindex2]!.CardFraction != Card.Fraction.None)
                                                GameDeck.Add(Shop[scrapindex2]!);
                                            scrapSFX.Play(GameSettings.SFXVolume, 0, 0);
                                            Shop[scrapindex2] = null;
                                        }
                                        RefillShop();
                                        break;
                                    case ActionType.Buy:
                                        Card[] cards = Shop.Where((card) => card.GetCardDetails().Equals(action.Card)).ToArray();
                                        if (cards.Length == 0) {
                                            cheating = true;
                                            break;
                                        }
                                        Card card = cards[0];
                                        if (card.Price > EnemyMoney) {
                                            cheating = true;
                                            break;
                                        }
                                        for (int i = 0; i < Shop.Length; i++) {
                                            if (Shop[i] == card) {
                                                EnemyScrap.Add(Shop[i]!);
                                                Shop[i] = null;
                                                break;
                                            }
                                        }
                                        card.Flipped = true;
                                        card.RenderPrice = false;
                                        EnemyMoney -= card.Price;
                                        buySFX.Play(GameSettings.SFXVolume, 0, 0);
                                        RefillShop();
                                        changed = true;
                                        break;
                                    case ActionType.EndTurn:
                                        ClearPlayedPile(false);
                                        RefillEnemyHand();
                                        enemyendturn = true;
                                        changed = true;
                                        break;
                                    default:
                                        cheating = true;
                                        break;
                                }
                                if (cheating) {
                                    WINNER = GameWinner.Player; //kilép, mert ilyet nem kaphat
                                    break;
                                }
                            }
                            else {
                                WINNER = GameWinner.Player; //kilép, mert ilyet nem kaphat
                                break;
                            }
                        }
                        else {
                            //CLIENT
                            if (p is ActionPayload action) {
                                bool cheating = false;
                                switch (action.Action) {
                                    case ActionType.Play:
                                        Card[] cardtoplay = EnemyHand.Where((card) => card.GetCardDetails().Equals(action.Card)).ToArray();
                                        if (cardtoplay.Length == 0) {
                                            cheating = true;
                                            break;
                                        }
                                        AddToPlayedPile(cardtoplay[0], EnemyHandTarget[EnemyHand.IndexOf(cardtoplay[0])], false);
                                        break;
                                    case ActionType.Draw:
                                        cheating = RefillEnemyHand([(CardDetails)action.Card!]);
                                        break;
                                    case ActionType.Steal:
                                        //Ez játék közben is lehet
                                        List<Card> cards3 = playerTurn ? EnemyHand : PlayerHand;
                                        Card[] cardtosteal = cards3.Where((card) => card.GetCardDetails().Equals(action.Card)).ToArray();
                                        if (cardtosteal.Length == 0) {
                                            cheating = true;
                                            break;
                                        }
                                        stolenCards.Add(cardtosteal[0]);
                                        stealSFX.Play(GameSettings.SFXVolume, 0, 0);
                                        AddToPlayedPile(cardtosteal[0], playerTurn ? EnemyHandTarget[EnemyHand.IndexOf(cardtosteal[0])] : PlayerHandTarget[PlayerHand.IndexOf(cardtosteal[0])], !playerTurn);
                                        break;
                                    case ActionType.Flip:
                                        Card[] cardtoflip = EnemyHand.Where((card) => card.GetCardDetails().Equals(action.Card)).ToArray();
                                        if (cardtoflip.Length == 0) {
                                            cheating = true;
                                            break;
                                        }
                                        cardtoflip[0].Flipped = false;
                                        break;
                                    case ActionType.Scrap:
                                        Card[] cardtoscrap = EnemyScrap.Where((card) => card.GetCardDetails().Equals(action.Card)).ToArray();
                                        if (cardtoscrap.Length == 0) {
                                            cheating = true;
                                            break;
                                        }
                                        int scrapindex = EnemyScrap.IndexOf(cardtoscrap[0]);
                                        if (EnemyScrap[scrapindex].CardFraction != Card.Fraction.None)
                                            GameDeck.Add(EnemyScrap[scrapindex]);
                                        EnemyScrap.RemoveAt(scrapindex);
                                        scrapSFX.Play(GameSettings.SFXVolume, 0, 0);
                                        changed = true;
                                        break;
                                    case ActionType.ScrapFromHand:
                                        Card[] cardtoscrap3 = EnemyHand.Where((card) => card.GetCardDetails().Equals(action.Card)).ToArray();
                                        if (cardtoscrap3.Length == 0) {
                                            cheating = true;
                                            break;
                                        }
                                        EnemyScrap.Add(cardtoscrap3[0]);
                                        EnemyHandTarget.RemoveAt(EnemyHand.IndexOf(cardtoscrap3[0]));
                                        EnemyHand.Remove(cardtoscrap3[0]);
                                        changed = true;
                                        break;
                                    case ActionType.Buy:
                                        Card[] cards = Shop.Where((card) => card.GetCardDetails().Equals(action.Card)).ToArray();
                                        if (cards.Length == 0) {
                                            cheating = true;
                                            break;
                                        }
                                        Card card = cards[0];
                                        if (card.Price > EnemyMoney) {
                                            cheating = true;
                                            break;
                                        }
                                        for (int i = 0; i < Shop.Length; i++) {
                                            if (Shop[i] == card) {
                                                EnemyScrap.Add(Shop[i]!);
                                                Shop[i] = null;
                                                break;
                                            }
                                        }
                                        card.Flipped = true;
                                        card.RenderPrice = false;
                                        EnemyMoney -= card.Price;
                                        buySFX.Play(GameSettings.SFXVolume, 0, 0);
                                        changed = true;
                                        break;
                                    case ActionType.EndTurn:
                                        ClearPlayedPile(false);
                                        enemyendturn = true;
                                        break;
                                    default:
                                        cheating = true;
                                        break;
                                }
                                if (cheating) {
                                    WINNER = GameWinner.Player; //kilép, mert ilyet nem kaphat
                                    break;
                                }
                            }
                            else {
                                bool cheating = false;
                                CardListPayload cardlist = (CardListPayload)p;
                                switch (cardlist.CardType) {
                                    case CardType.Deck:
                                        if (cardlist.HostCards) {
                                            List<Card> oldDeck = new(EnemyDeck);
                                            EnemyDeck.Clear();
                                            foreach (CardDetails card in cardlist.Cards) {
                                                Card[] deck = oldDeck.Where((c) => c.GetCardDetails().Equals(card)).ToArray();
                                                Card[] deck2 = EnemyScrap.Where((c) => c.GetCardDetails().Equals(card)).ToArray();
                                                Card[] deck3 = EnemyUnknownDeck.Where((c) => c.GetCardDetails().Equals(card)).ToArray();
                                                if (deck3.Length != 0) {
                                                    EnemyUnknownDeck.Remove(deck3[0]);
                                                    EnemyDeck.Add(deck3[0]);
                                                }
                                                else if (deck.Length != 0) {
                                                    oldDeck.Remove(deck[0]);
                                                    EnemyDeck.Add(deck[0]);
                                                }
                                                else if (deck2.Length != 0) {
                                                    EnemyScrap.Remove(deck2[0]);
                                                    EnemyDeck.Add(deck2[0]);
                                                }
                                                else {
                                                    cheating = true;
                                                    break;
                                                }
                                            }
                                            EnemyUnknownDeck.AddRange(oldDeck);
                                        }
                                        else {
                                            List<Card> oldDeck = new(PlayerDeck);
                                            PlayerDeck.Clear();
                                            foreach (CardDetails card in cardlist.Cards) {
                                                Card[] deck = oldDeck.Where((c) => c.GetCardDetails().Equals(card)).ToArray();
                                                Card[] deck2 = PlayerScrap.Where((c) => c.GetCardDetails().Equals(card)).ToArray();
                                                Card[] deck3 = PlayerUnknownDeck.Where((c) => c.GetCardDetails().Equals(card)).ToArray();
                                                if (deck3.Length != 0) {
                                                    PlayerUnknownDeck.Remove(deck3[0]);
                                                    PlayerDeck.Add(deck3[0]);
                                                }
                                                else if (deck.Length != 0) {
                                                    oldDeck.Remove(deck[0]);
                                                    PlayerDeck.Add(deck[0]);
                                                }
                                                else if (deck2.Length != 0) {
                                                    PlayerScrap.Remove(deck2[0]);
                                                    PlayerDeck.Add(deck2[0]);
                                                }
                                                else {
                                                    cheating = true;
                                                    break;
                                                }
                                            }
                                            PlayerUnknownDeck.AddRange(oldDeck);
                                        }
                                        changed = true;
                                        break;
                                    case CardType.Scrap:
                                        if (cardlist.HostCards) {
                                            List<Card> oldScrap = new(EnemyScrap);
                                            EnemyScrap.Clear();
                                            foreach (CardDetails card in cardlist.Cards) {
                                                Card[] deck = oldScrap.Where((c) => c.GetCardDetails().Equals(card)).ToArray();
                                                Card[] deck2 = EnemyUnknownDeck.Where((c) => c.GetCardDetails().Equals(card)).ToArray();
                                                Card[] deck3 = EnemyDeck.Where((c) => c.GetCardDetails().Equals(card)).ToArray();
                                                if (deck2.Length != 0) {
                                                    EnemyUnknownDeck.Remove(deck2[0]);
                                                    EnemyScrap.Add(deck2[0]);
                                                }
                                                else if (deck.Length != 0) {
                                                    oldScrap.Remove(deck[0]);
                                                    EnemyScrap.Add(deck[0]);
                                                }
                                                else if (deck3.Length != 0) {
                                                    EnemyDeck.Remove(deck3[0]);
                                                    EnemyScrap.Add(deck3[0]);
                                                }
                                                else {
                                                    cheating = true;
                                                    break;
                                                }
                                            }
                                            EnemyUnknownDeck.AddRange(oldScrap);
                                        }
                                        else {
                                            List<Card> oldScrap = new(PlayerScrap);
                                            PlayerScrap.Clear();
                                            foreach (CardDetails card in cardlist.Cards) {
                                                Card[] deck = oldScrap.Where((c) => c.GetCardDetails().Equals(card)).ToArray();
                                                Card[] deck2 = PlayerUnknownDeck.Where((c) => c.GetCardDetails().Equals(card)).ToArray();
                                                Card[] deck3 = PlayerDeck.Where((c) => c.GetCardDetails().Equals(card)).ToArray();
                                                if (deck2.Length != 0) {
                                                    PlayerUnknownDeck.Remove(deck2[0]);
                                                    PlayerScrap.Add(deck2[0]);
                                                }
                                                else if (deck.Length != 0) {
                                                    oldScrap.Remove(deck[0]);
                                                    PlayerScrap.Add(deck[0]);
                                                }
                                                else if (deck3.Length != 0) {
                                                    PlayerDeck.Remove(deck3[0]);
                                                    PlayerScrap.Add(deck3[0]);
                                                }
                                                else {
                                                    cheating = true;
                                                    break;
                                                }
                                            }
                                            PlayerUnknownDeck.AddRange(oldScrap);
                                        }
                                        changed = true;
                                        break;
                                    case CardType.Hand:
                                        if (cardlist.HostCards) {
                                            cheating = RefillEnemyHand(cardlist.Cards);
                                        }
                                        else {
                                            cheating = RefillPlayerHand(cardlist.Cards);
                                        }
                                        break;
                                    case CardType.Shop:
                                        RefillShop(cardlist.Cards);
                                        break;
                                    default:
                                        cheating = true;
                                        break;
                                }
                                if (cheating) {
                                    WINNER = GameWinner.Player; //kilép, mert ilyet nem kaphat
                                    break;
                                }
                            }
                        }
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
                        //PlayAllCards
                        if (PlayerHand.Count != 0) {
                            if (playAllCards) {
                                cardMouseControlEnabled = false;
                                flipSFX.Play(GameSettings.SFXVolume, 0, 0);
                                PlayNext(true);
                            }
                            else {
                                PlayAllCardsButton.Enabled = true;
                            }
                        }
                        else {
                            playAllCards = false;
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
                        //PlayCards
                        if (PlayedPile.Any(card => !card.BaseApplied)) {
                            for (int i = 0; i < PlayedPile.Count; i++) {
                                PlayCard(PlayedPile[i], false);
                            }
                        }
                        if (enemyendturn) {
                            //ClearPlayedPile(false);
                            //RefillEnemyHand();
                            EnemyMoney = 0;
                            PlayerHealth -= EnemyAttack;
                            if (EnemyAttack > 0) {
                                explodeSFX.Play(GameSettings.SFXVolume, 0, 0);
                                if (EnemyAttack > 10)
                                    explodeEXT_SFX.Play(GameSettings.SFXVolume, 0, 0);
                            }
                            EnemyAttack = 0;
                            playerTurn = true;
                            enemyendturn = false;
                        }
                    }
                    //
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
                Parallel.For(0, PlayerDeck.Count, ParallelOptions, i => {
                    PlayerDeck[i].Rect = PlayerDeckLoc;
                });
                //set offset for first X cards
                Parallel.For(0, Pdeckshowcount, ParallelOptions, i => {
                    PlayerDeck[i].Rect = new Rectangle(
                        PlayerDeck[i].Rect.X - (groupingOffset * i),
                        PlayerDeck[i].Rect.Y - (groupingOffset * i),
                        PlayerDeck[i].Rect.Width,
                        PlayerDeck[i].Rect.Height);
                });
                //P_Scrap
                Parallel.For(0, PlayerScrap.Count, ParallelOptions, i => {
                    PlayerScrap[i].Rect = PlayerScrapLoc;
                });
                Parallel.For(0, Pscrapshowcount, ParallelOptions, i => {
                    PlayerScrap[i].Rect = new Rectangle(
                        PlayerScrap[i].Rect.X - (groupingOffset * i),
                        PlayerScrap[i].Rect.Y - (groupingOffset * i),
                        PlayerScrap[i].Rect.Width,
                        PlayerScrap[i].Rect.Height);
                });
                //E_Deck
                Parallel.For(0, EnemyDeck.Count, ParallelOptions, i => {
                    EnemyDeck[i].Rect = EnemyDeckLoc;
                });
                Parallel.For(0, Edeckshowcount, ParallelOptions, i => {
                    EnemyDeck[i].Rect = new Rectangle(
                        EnemyDeck[i].Rect.X - (groupingOffset * i),
                        EnemyDeck[i].Rect.Y - (groupingOffset * i),
                        EnemyDeck[i].Rect.Width,
                        EnemyDeck[i].Rect.Height);
                });
                //E_Scrap
                Parallel.For(0, EnemyScrap.Count, ParallelOptions, i => {
                    EnemyScrap[i].Rect = EnemyScrapLoc;
                });
                Parallel.For(0, Escrapshowcount, ParallelOptions, i => {
                    EnemyScrap[i].Rect = new Rectangle(
                        EnemyScrap[i].Rect.X - (groupingOffset * i),
                        EnemyScrap[i].Rect.Y - (groupingOffset * i),
                        EnemyScrap[i].Rect.Width,
                        EnemyScrap[i].Rect.Height);
                });
                changed = false;
            }
            //regular updates
            Parallel.For(0, Pdeckshowcount, ParallelOptions, i => {
                PlayerDeck[i].Update(gameTime);
            });
            Parallel.For(0, Pscrapshowcount, ParallelOptions, i => {
                PlayerScrap[i].Update(gameTime);
            });
            Parallel.For(0, Edeckshowcount, ParallelOptions, i => {
                EnemyDeck[i].Update(gameTime);
            });
            Parallel.For(0, Escrapshowcount, ParallelOptions, i => {
                EnemyScrap[i].Update(gameTime);
            });
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
                                flipSFX.Play(GameSettings.SFXVolume, 0, 0);
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
                                    flipSFX.Play(GameSettings.SFXVolume, 0, 0);
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
                                    flipSFX.Play(GameSettings.SFXVolume, 0, 0);
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
                                    flipSFX.Play(GameSettings.SFXVolume, 0, 0);
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
                                flipSFX.Play(GameSettings.SFXVolume, 0, 0);
                                break;
                            }
                        }
                        if (selectedCard == null) {
                            for (int i = 0; i < PlayerHand.Count; i++) {
                                if (PlayerHand[i].Rect.Contains(mouse.GetMousePosition())) {
                                    selectedCard = PlayerHand[i];
                                    previewThis.Add(new(PlayerHand[i], PlayerHandTarget[i]));
                                    ShowPlayedPile = true;
                                    flipSFX.Play(GameSettings.SFXVolume, 0, 0);
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
                            PrevHObject = null;
                            flipSFX.Play(GameSettings.SFXVolume, 0, 0);
                        }
                        else {
                            if (previewThisCard.MoveTarget != previewRect) {
                                previewThisCard.MoveTarget = previewRect;
                            }
                            else {
                                //ToolTips
                                Card prevw = previewThis[^1].Item1;
                                Card.HoveredObject hobj = prevw.GetHoveredState(mouse);
                                if (PrevHObject != hobj) {
                                    PrevHObject = hobj;
                                    string fname = prevw.CardFraction switch {
                                        Card.Fraction.Alliance => "'Szövetség'",
                                        Card.Fraction.CollectorCult => "'Kuratórium'",
                                        Card.Fraction.Empire => "'Birodalom'",
                                        Card.Fraction.Machines => "'Gépek'",
                                        Card.Fraction.TheEye => "'A szem'",
                                        Card.Fraction.None => "frakciómentes lapok",
                                        _ => string.Empty
                                    };
                                    string reqname = prevw.EffectRequirement switch {
                                        Card.Fraction.Alliance => "'Szövetség'",
                                        Card.Fraction.CollectorCult => "'Kuratórium'",
                                        Card.Fraction.Empire => "'Birodalom'",
                                        Card.Fraction.Machines => "'Gépek'",
                                        Card.Fraction.TheEye => "'A szem'",
                                        Card.Fraction.None => "frakciómentes lapok",
                                        _ => string.Empty
                                    };
                                    string specEffect = prevw.CardEffect switch {
                                        Card.Effect.ScrapEnemyCard => "A következő kör kezdetén az ellenfél eldob egy lapot,\n mielőtt azt kijátszhatná.",
                                        Card.Effect.ScrapFromShop => "Eltávolít egy lapot a boltból.",
                                        Card.Effect.AntiShow => "Az ellenfél nem fedheti fel a lapjaid ebben a körben.",
                                        Card.Effect.StealCard => "Ellopja az ellenfél egy kártyáját és kijátsza azt.",
                                        Card.Effect.DrawCard => "Húz még egy kártyát.",
                                        Card.Effect.ScrapOwnCard => "Véglegesen eltávolít egy lapot az aldobott halmodból.",
                                        Card.Effect.AttackBonus => "Támadási bónusz.",
                                        Card.Effect.HealthBonus => "Életerő/Autoritás növelése.",
                                        Card.Effect.MoneyBonus => "Játékpénz bónusz.",
                                        Card.Effect.ShowHand => "Ellenfél kezében lévő lapok felfedése.",
                                        Card.Effect.ShowDeck => "Ellenfél paklijának felfedése. Megmutatja, hogy milyen lapokat\nfog húzni az ellenfél a következő körben.",
                                        Card.Effect.SelfDestruct => "A lap kijátszásakor elpusztítja önmagát.",
                                        Card.Effect.None => "Nincs speciális képesség!",
                                        _ => string.Empty,
                                    };
                                    var distribution = GetFractionDistribution();
                                    ToolTipsBox.Text = hobj switch {
                                        Card.HoveredObject.None => $"Ez a kártya a {fname} lapjaihoz tartozik,\n" +
                                                                   $"melyből jelenleg {distribution.Item2[(int)prevw.CardFraction]} db van a tulajdonodban.\n" +
                                                                   $"Ez a paklid {distribution.Item1[(int)prevw.CardFraction].ToString("P2")}-ának felel meg.",
                                        Card.HoveredObject.Price => $"A lap ára {prevw.Price} játékpénz!\nEzt a lapot jelenleg {(PlayerMoney >= prevw.Price ? "meg tudod vásárolni" : "NEM tudod megvásárolni")}!",
                                        Card.HoveredObject.Fraction => $"Ez a kártya frakciójele.\nMeghatározza, hogy az adott lap\nmelyik frakcióhoz tartozik.\nEz a kártya a {fname} lapjai közé tartozik.",
                                        Card.HoveredObject.BaseAbilities => $"Ez a mező a lap alapképességeit határozza meg.\n" +
                                                                            $"Ezeket a képességeket nem köti feltétel,\n" +
                                                                            $"értékeiket kijátszásuk mindig megadja a játékosnak.\n\n" +
                                                                            $"Ez a lap {prevw.Money} pénzt, {prevw.Health} életerőt és {prevw.GetTrueAttack()} támadást biztosít.",
                                        Card.HoveredObject.SpecialAbility => $"Ez a mező a lap speciális képességeit határozza meg.\n" +
                                                                             $"A lap képessége {(prevw.EffectRequirement != Card.Fraction.None ? $"feltételhez kötött.\nA speciális feltétel kihasználásához legalább egy\n{reqname} lapot ki kell játszani a jelenlegi körben!\n" : "NEM kötött feltételhez,\naz alapképességekkel egyszerre kijátszható.\n")}" +
                                                                             $"A lap speciális képessége {prevw.EffectAmount} alkalommal biztosítja:\n{specEffect}",
                                        Card.HoveredObject.Unknown => "A lap adatai ismeretlenek!",
                                        _ => string.Empty
                                    };
                                }
                            }
                        }
                    }
                    //deselect selected
                    if (selectedCard != null) {
                        if (mouse.Current.LeftButton == ButtonState.Released &&
                            mouse.Previous.LeftButton == ButtonState.Pressed) {
                            previewThis[^1].Item2.StartLocation = selectedCard.Rect;
                            if (PlayerHand.Contains(selectedCard)) {
                                if (PlayedPileLoc.Contains(mouse.GetMousePosition())) {
                                    AddToPlayedPile(selectedCard, PlayerHandTarget[PlayerHand.IndexOf(selectedCard)], true);
                                    // SEND
                                    peer.SendAsync(new ActionPayload(ActionType.Play, selectedCard.GetCardDetails(), CryptographyHelper.NowMs()));
                                }
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
                PlayAllCardsButton.Update(gameTime);
            }
            ToolTipsBox.Update(gameTime);
            //CARDS with targets
            //TARGETS
            Parallel.ForEach(ShopTarget, ParallelOptions, item => {
                item.NextStep(gameTime);
            });
            Parallel.ForEach(PlayerHandTarget, ParallelOptions, item => {
                item.NextStep(gameTime);
            });
            Parallel.ForEach(EnemyHandTarget, ParallelOptions, item => {
                item.NextStep(gameTime);
            });
            Parallel.ForEach(PlayedPileTarget, ParallelOptions, item => {
                item.NextStep(gameTime);
            });
            //CARDS
            //hand cards
            Parallel.For(0, PlayerHand.Count, ParallelOptions, i => {
                if (selectedCard != PlayerHand[i])
                    PlayerHand[i].Rect = PlayerHandTarget[i].CurrentLocation;
            });
            Parallel.For(0, EnemyHand.Count, ParallelOptions, i => {
                EnemyHand[i].Rect = EnemyHandTarget[i].CurrentLocation;
            });
            //shop cards
            Parallel.For(0, Shop.Length, ParallelOptions, i => {
                if (Shop[i] != null && selectedCard != Shop[i]) {
                    Shop[i]!.Rect = ShopTarget[i].CurrentLocation;
                }
            });
            //played pile
            Parallel.For(0, PlayedPile.Count, ParallelOptions, i => {
                PlayedPile[i].Rect = PlayedPileTarget[i].CurrentLocation;
            });
            //preview
            if (previewThis.Count > 0 && selectedCard == null) {
                for (int i = previewThis.Count - 1; i >= 0; i--) {
                    if (!previewThis[i].Item2.IsTransforming &&
                        previewThis[i].Item2.MoveTarget != previewRect) {
                        previewThis.RemoveAt(i);
                    }
                }
            }
            //CARDS UPDATE
            //hand cards
            Parallel.For(0, PlayerHand.Count, ParallelOptions, i => {
                PlayerHand[i].Update(gameTime);
            });
            Parallel.For(0, EnemyHand.Count, ParallelOptions, i => {
                EnemyHand[i].Update(gameTime);
            });
            //shop cards
            Parallel.For(0, Shop.Length, ParallelOptions, i => {
                if (Shop[i] != null) {
                    Shop[i]!.Update(gameTime);
                }
            });
            //played pile
            Parallel.For(0, PlayedPile.Count, ParallelOptions, i => {
                PlayedPile[i].Update(gameTime);
            });
        }

        public void Draw(GameTime gameTime, SpriteBatch spriteBatch)
        {
            int Pdeckshowcount = PlayerDeck.Count > MAX_OFFSET_CARDS ? MAX_OFFSET_CARDS : PlayerDeck.Count;
            int Pscrapshowcount = PlayerScrap.Count > MAX_OFFSET_CARDS ? MAX_OFFSET_CARDS : PlayerScrap.Count;
            int Edeckshowcount = EnemyDeck.Count > MAX_OFFSET_CARDS ? MAX_OFFSET_CARDS : EnemyDeck.Count;
            int Escrapshowcount = EnemyScrap.Count > MAX_OFFSET_CARDS ? MAX_OFFSET_CARDS : EnemyScrap.Count;
            overlay ??= ResourceManager.GetColor(Color.Orange, spriteBatch);
            TurnIndicatorColor ??= ResourceManager.GetColor(Color.OrangeRed, spriteBatch);
            if (endingScreen is null) {
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
                //turnindicator
                if (playerTurn) {
                    spriteBatch.Draw(TurnIndicatorColor, TurnIndicatorLocs[0], Color.White);
                }
                else {
                    spriteBatch.Draw(TurnIndicatorColor, TurnIndicatorLocs[1], Color.White);
                }
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
                PlayAllCardsButton.Draw(gameTime, spriteBatch);
                //hand cards
                if (ShowPlayerHand) {
                    spriteBatch.Draw(overlay, PlayerHandLoc, new(255, 255, 255, 64));
                }
                for (int i = 0; i < PlayerHand.Count; i++) {
                    if (previewThis.TrueForAll(item => item.Item1 != PlayerHand[i]))
                        PlayerHand[i].Draw(gameTime, spriteBatch);
                }
                for (int i = 0; i < EnemyHand.Count; i++) {
                    if (previewThis.TrueForAll(item => item.Item1 != EnemyHand[i]))
                        EnemyHand[i].Draw(gameTime, spriteBatch);
                }
                //shop cards
                for (int i = 0; i < Shop.Length; i++) {
                    if (Shop[i] != null) {
                        if (previewThis.TrueForAll(item => item.Item1 != Shop[i]))
                            Shop[i]!.Draw(gameTime, spriteBatch);
                    }
                }
                //played pile
                if (ShowPlayedPile) {
                    spriteBatch.Draw(overlay, PlayedPileLoc, new(255, 255, 255, 64));
                }
                for (int i = 0; i < PlayedPile.Count; i++) {
                    if (previewThis.TrueForAll(item => item.Item1 != PlayedPile[i]))
                        PlayedPile[i].Draw(gameTime, spriteBatch);
                }
                //preview
                foreach (var item in previewThis) {
                    item.Item1.Draw(gameTime, spriteBatch);
                }
                //ToolTips
                if (previewThisCard is not null) {
                    ToolTipsBox.Draw(gameTime, spriteBatch);
                }
                //selector
                cardSelector?.Draw(gameTime, spriteBatch);
            }
            else {
                //ending
                endingScreen.Draw(gameTime, spriteBatch);
            }
        }

        public void Dispose() => peer.Dispose();
    }
}
