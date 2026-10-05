using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

#nullable enable
namespace CardGame {
    internal partial class ForeGround {
        private readonly BackGround BG;
        private readonly List<Card> GameDeck;
        private readonly Card?[] Shop;
        private readonly List<Card> PlayerDeck;
        private readonly List<Card> PlayerHand;
        private readonly List<Card> PlayerScrap;
        private readonly List<Card> EnemyDeck;
        private readonly List<Card> EnemyHand;
        private readonly List<Card> EnemyScrap;
        private readonly List<Card> PlayedPile;

        //Game variables
        private int zeroSlotStreak = 0, enemyZeroSlotStreak = 0;
        private float currentPenalty = 0f, enemyCurrentPenalty = 0f;
        private int PlayerAttack = 0;
        private int PlayerMoney = 0;
        private static int InitHealth = 90;
        private int PlayerHealth = InitHealth;
        private int EnemyAttack = 0;
        private int EnemyMoney = 0;
        private int EnemyHealth = InitHealth;
        private bool playerTurn = true;
        private int TurnCount = 0;
        private const int MaxTurns = 2000;
        private int PlayerCard2ScrapThisTurn = 0;
        private int EnemyCard2ScrapThisTurn = 0;
        private readonly List<Card> stolenCards = [];
        public short EnemyDecisionMaking { get; set; } = 0; // 0 - NN, 1 - StrategyHeuristic, 2 - Heuristic, 3 - Random
        public short PlayerDecisionMaking { get; set; } = 0; // 0 - NN, 1 - StrategyHeuristic, 2 - Heuristic, 3 - Random
        public bool ExportEnabled { get; set; } = false;
        public GameWinner WINNER { get; private set; } = GameWinner.InProgress;


        public enum GameWinner {
            InProgress,
            Player,
            Enemy
        }

        private ForeGround() => throw new NotImplementedException();
        public ForeGround(BackGround bg)
        {
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
            RefillShop();
            RefillPlayerHand();
            RefillEnemyHand();
            //ITT NEM UPDATEOLJUK A BG-T, AZT A GAME1 CSINÁLJA
        }

        private void AddToPlayedPile(Card card, bool player = true)
        {
            PlayedPile.Add(card);
            if (player) {
                PlayerHand.Remove(card);
            }
            else {
                card.Flipped = false;
                EnemyHand.Remove(card);
            }
        }

        private void PlayNext(bool player = true)
        {
            if (player) {
                for (int i = 0; i <= (int)Card.Effect.None; i++) {
                    foreach (var card in PlayerHand.Where(card => card.CardEffect == (Card.Effect)i)) {
                        AddToPlayedPile(card, true);
                        return;
                    }
                }
            }
            else {
                for (int i = 0; i <= (int)Card.Effect.None; i++) {
                    foreach (var card in EnemyHand.Where(card => card.CardEffect == (Card.Effect)i)) {
                        AddToPlayedPile(card, false);
                        return;
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
                        PlayedPile.Remove(card);
                        break;
                    case Card.Effect.ScrapOwnCard:
                        // optionally destroy a card from scrap pile
                        if (player) {
                            if (PlayerScrap.Count != 0 && PlayerHand.Count + PlayerDeck.Count + PlayerScrap.Count - card.EffectAmount > 6) {
                                int scrapped = 0;
                                while (scrapped < card.EffectAmount && PlayerScrap.Count > 0) {
                                    int scrapindex = 0;
                                    if (PlayerScrap.Count > 1) {
                                        if (PlayerDecisionMaking == 3) {
                                            scrapindex = RandomNumberGenerator.GetInt32(0, PlayerScrap.Count);
                                        }
                                        else if (PlayerDecisionMaking == 2) {
                                            Card toScrap = GreedyBuyAlgorithm(PlayerHealth, EnemyHealth, PlayerScrap, GetFractionDistribution(true).Item1, true);
                                            scrapindex = PlayerScrap.IndexOf(toScrap);
                                            if (scrapindex == -1)
                                                scrapindex = RandomNumberGenerator.GetInt32(0, PlayerScrap.Count);
                                        }
                                        else if (PlayerDecisionMaking == 1) {
                                            ModelOutput5 output5 = MLController.StrategyEngine.Predict(new ModelInput5() { Features = GetStrategyDistribution(false) });
                                            Card toScrap = StrategyAwareGreedyBuyAlgorithm(PlayerHealth, EnemyHealth, PlayerScrap, GetFractionDistribution(true).Item1, output5.Prediction, true);
                                            scrapindex = PlayerScrap.IndexOf(toScrap);
                                            if (scrapindex == -1)
                                                scrapindex = RandomNumberGenerator.GetInt32(0, PlayerScrap.Count);
                                        }
                                        else {
                                            List<Card> tempScrap = PlayerScrap;
                                            for (int i = 1; i < tempScrap.Count; i++) {
                                                ModelInput49 input = MLController.CreateDiscardInput(tempScrap[scrapindex], tempScrap[i], GetStrategyDistribution(true));
                                                ModelOutput2 output = MLController.DiscardEngine.Predict(input);
                                                if (output.Prediction[0] > output.Prediction[1]) {
                                                    scrapindex = i;
                                                }
                                            }
                                            scrapindex = PlayerScrap.IndexOf(tempScrap[scrapindex]);
                                        }
                                    }
                                    else {
                                        if (PlayerScrap[scrapindex].CardFraction != Card.Fraction.None)
                                            break;
                                    }

                                    if (PlayerScrap[scrapindex].CardFraction != Card.Fraction.None)
                                        GameDeck.Add(PlayerScrap[scrapindex]);
                                    PlayerScrap.RemoveAt(scrapindex);
                                    scrapped++;
                                }
                            }
                        }
                        else {
                            if (EnemyScrap.Count != 0 && EnemyHand.Count + EnemyDeck.Count + EnemyScrap.Count - card.EffectAmount > 6) {
                                int scrapped = 0;
                                while (scrapped < card.EffectAmount && EnemyScrap.Count > 0) {
                                    int scrapindex = 0;
                                    if (EnemyDecisionMaking == 3) {
                                        scrapindex = RandomNumberGenerator.GetInt32(0, EnemyScrap.Count);
                                    }
                                    else if (EnemyDecisionMaking == 2) {
                                        Card toScrap = GreedyBuyAlgorithm(EnemyHealth, PlayerHealth, EnemyScrap, GetFractionDistribution(false).Item1, true);
                                        scrapindex = EnemyScrap.IndexOf(toScrap);
                                        if (scrapindex == -1)
                                            scrapindex = RandomNumberGenerator.GetInt32(0, EnemyScrap.Count);
                                    }
                                    else if (EnemyDecisionMaking == 1) {
                                        ModelOutput5 output5 = MLController.StrategyEngine.Predict(new ModelInput5() { Features = GetStrategyDistribution(true) });
                                        Card toScrap = StrategyAwareGreedyBuyAlgorithm(EnemyHealth, PlayerHealth, EnemyScrap, GetFractionDistribution(false).Item1, output5.Prediction, true);
                                        scrapindex = EnemyScrap.IndexOf(toScrap);
                                        if (scrapindex == -1)
                                            scrapindex = RandomNumberGenerator.GetInt32(0, EnemyScrap.Count);
                                    }
                                    else {
                                        List<Card> tempScrap = EnemyScrap;
                                        for (int i = 1; i < tempScrap.Count; i++) {
                                            ModelInput49 input = MLController.CreateDiscardInput(tempScrap[scrapindex], tempScrap[i], GetStrategyDistribution(false));
                                            ModelOutput2 output = MLController.DiscardEngine.Predict(input);
                                            if (output.Prediction[0] > output.Prediction[1]) {
                                                scrapindex = i;
                                            }
                                        }
                                        scrapindex = EnemyScrap.IndexOf(tempScrap[scrapindex]);
                                    }
                                    if (EnemyScrap[scrapindex].CardFraction != Card.Fraction.None)
                                        GameDeck.Add(EnemyScrap[scrapindex]);
                                    EnemyScrap.RemoveAt(scrapindex);
                                    scrapped++;
                                }
                            }
                        }
                        break;
                    case Card.Effect.ScrapFromShop:
                        if (player) {
                            for (int i = 0; i < card.EffectAmount; i++) {
                                int scrapindex = 0;
                                if (PlayerDecisionMaking == 3) {
                                    scrapindex = RandomNumberGenerator.GetInt32(0, Shop.Length);
                                }
                                else if (PlayerDecisionMaking == 2) {
                                    Card toScrap = GreedyBuyAlgorithm(PlayerHealth, EnemyHealth, Shop.Where(card => card != null).Select(card => card!).ToList(), GetFractionDistribution(false).Item1, false);
                                    scrapindex = Array.IndexOf(Shop, toScrap);
                                }
                                else if (PlayerDecisionMaking == 1) {
                                    Card toScrap = StrategyAwareGreedyBuyAlgorithm(PlayerHealth, EnemyHealth, Shop.Where(card => card != null).Select(card => card!).ToList(), GetFractionDistribution(false).Item1, GetStrategyDistribution(false), false);
                                    scrapindex = Array.IndexOf(Shop, toScrap);
                                }
                                else {
                                    Card[] mydeck = PlayerDeck.Concat(PlayerHand).Concat(PlayerScrap).ToArray();
                                    Card[] enemydeck = EnemyDeck.Concat(EnemyHand).Concat(EnemyScrap).ToArray();
                                    ModelInput158 input104 = MLController.CreateShoppingInput(GetStrategyDistribution(false), GetStrategyDistribution(false), GetFractionDistribution(true).Item1, GetFractionDistribution(false).Item1, Shop, Shop.Where(c => c != null).Max(c => c!.Price), EnemyHealth / (float)InitHealth, PlayerHealth / (float)InitHealth, mydeck, enemydeck);
                                    ModelOutput6 output6 = MLController.ShoppingEngine.Predict(input104);
                                    scrapindex = -1;
                                    float bestScrapScore = float.NegativeInfinity;
                                    for (int scrapCand = 1; scrapCand < Shop.Length; scrapCand++) {
                                        if (Shop[scrapCand] == null)
                                            continue;
                                        if (output6.Prediction[scrapCand] > bestScrapScore) {
                                            bestScrapScore = output6.Prediction[scrapCand];
                                            scrapindex = scrapCand;
                                        }
                                    }
                                }
                                if (Shop[scrapindex] != null) {
                                    if (Shop[scrapindex]!.CardFraction != Card.Fraction.None)
                                        GameDeck.Add(Shop[scrapindex]!);
                                    Shop[scrapindex] = null;
                                }
                                RefillShop();
                            }
                        }
                        else {
                            for (int i = 0; i < card.EffectAmount; i++) {
                                int scrapindex = 0;
                                if (EnemyDecisionMaking == 3) {
                                    scrapindex = RandomNumberGenerator.GetInt32(0, Shop.Length);
                                }
                                else if (EnemyDecisionMaking == 2) {
                                    Card toScrap = GreedyBuyAlgorithm(EnemyHealth, PlayerHealth, Shop.Where(card => card != null).Select(card => card!).ToList(), GetFractionDistribution(true).Item1, false);
                                    scrapindex = Array.IndexOf(Shop, toScrap);
                                }
                                else if (EnemyDecisionMaking == 1) {
                                    Card toScrap = StrategyAwareGreedyBuyAlgorithm(EnemyHealth, PlayerHealth, Shop.Where(card => card != null).Select(card => card!).ToList(), GetFractionDistribution(true).Item1, GetStrategyDistribution(true), false);
                                    scrapindex = Array.IndexOf(Shop, toScrap);
                                }
                                else {
                                    Card[] mydeck = EnemyDeck.Concat(EnemyHand).Concat(EnemyScrap).ToArray();
                                    Card[] enemydeck = PlayerDeck.Concat(PlayerHand).Concat(PlayerScrap).ToArray();
                                    ModelInput158 input104 = MLController.CreateShoppingInput(GetStrategyDistribution(true), GetStrategyDistribution(true), GetFractionDistribution(false).Item1, GetFractionDistribution(true).Item1, Shop, Shop.Where(c => c != null).Max(c => c!.Price), PlayerHealth / (float)InitHealth, EnemyHealth / (float)InitHealth, mydeck, enemydeck);
                                    ModelOutput6 output6 = MLController.ShoppingEngine.Predict(input104);
                                    scrapindex = -1;
                                    float bestScrapScore = float.NegativeInfinity;
                                    for (int scrapCand = 1; scrapCand < Shop.Length; scrapCand++) {
                                        if (Shop[scrapCand] == null)
                                            continue;
                                        if (output6.Prediction[scrapCand] > bestScrapScore) {
                                            bestScrapScore = output6.Prediction[scrapCand];
                                            scrapindex = scrapCand;
                                        }
                                    }
                                }
                                if (Shop[scrapindex] != null) {
                                    if (Shop[scrapindex]!.CardFraction != Card.Fraction.None)
                                        GameDeck.Add(Shop[scrapindex]!);
                                    Shop[scrapindex] = null;
                                }
                                RefillShop();
                            }
                        }
                        break;
                    case Card.Effect.ShowHand:
                        break;
                    case Card.Effect.ShowDeck:
                        break;
                    case Card.Effect.StealCard:
                        List<Card> cards3 = player ? EnemyHand : PlayerHand;
                        if (cards3.Any(tcard => tcard.CardEffect == Card.Effect.AntiShow)) {
                            break;
                        }
                        for (int i = 0; i < card.EffectAmount; i++) {
                            if (cards3.Count == 0) {
                                break;
                            }
                            Card stolen = DeckGenerator.GetCard(cards3);
                            stolenCards.Add(stolen);
                            AddToPlayedPile(stolen, !player);
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
                if (player == playerTurn) {
                    foreach (var card in PlayedPile) {
                        if (stolenCards.Contains(card))
                            continue;
                        if (card.CardFraction != Card.Fraction.None) {
                            cards[(int)card.CardFraction]++;
                            allcards++;
                        }
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
                if (player == playerTurn) {
                    foreach (var card in PlayedPile) {
                        if (stolenCards.Contains(card))
                            continue;
                        if (card.CardFraction != Card.Fraction.None) {
                            cards[(int)card.CardFraction]++;
                            allcards++;
                        }
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

        private Tuple<float[], int[]> GetFractionDistribution(bool player = true)
        {
            int allcards = 0;
            int[] cards = new int[6];
            float[] distribution = new float[6];
            List<Card> tmp = player ? PlayerHand.Concat(PlayerDeck).Concat(PlayerScrap).ToList() : EnemyHand.Concat(EnemyDeck).Concat(EnemyScrap).ToList();
            foreach (var card in tmp) {
                cards[(int)card.CardFraction]++;
                allcards++;
            }
            if (playerTurn == player) {
                foreach (var card in PlayedPile) {
                    if (stolenCards.Contains(card))
                        continue;
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
                    if (ExportEnabled)
                        ExportShoppingAction(i);
                    break;
                }
            }
            card.Flipped = true;
            RefillShop();
        }

        private void RefillPlayerHand()
        {
            while (PlayerHand.Count < 5) {
                if (PlayerDeck.Count == 0) {
                    PlayerDeck.AddRange(PlayerScrap);
                    PlayerScrap.Clear();
                    DeckGenerator.ShuffleDeck(PlayerDeck);
                }
                PlayerHand.Add(PlayerDeck[^1]);
                PlayerHand[^1].Flipped = false;
                PlayerDeck.RemoveAt(PlayerDeck.Count - 1);
            }
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
        }

        private void RefillEnemyHand()
        {
            while (EnemyHand.Count < 5) {
                if (EnemyDeck.Count == 0) {
                    EnemyDeck.AddRange(EnemyScrap);
                    EnemyScrap.Clear();
                    DeckGenerator.ShuffleDeck(EnemyDeck);
                }
                EnemyHand.Add(EnemyDeck[^1]);
                EnemyDeck.RemoveAt(EnemyDeck.Count - 1);
            }
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
        }

        private void RefillShop()
        {
            if (Shop[0] == null) {
                Shop[0] = DeckGenerator.GetMoneyCard();
                Shop[0]!.Flipped = false;
            }
            for (int i = 1; i < Shop.Length; i++) {
                if (Shop[i] == null) {
                    if (GameDeck.Count > 0) {
                        Shop[i] = DeckGenerator.GetCard(GameDeck);
                        GameDeck.Remove(Shop[i]!);
                        Shop[i]!.Flipped = false;
                    }
                    else {
                        Shop[i] = DeckGenerator.GetMoneyCard();
                        Shop[i]!.Flipped = false;
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
            stolenCards.Clear();
        }

        private T[][] GetCombinations<T>(List<T> array, int Comb_length)
        {
            static void Combination(int index, int r_length, List<T> data, ref List<T[]> result, List<T> input)
            {
                int length = input.Count;
                if (data.Count == r_length) {
                    result.Add(data.ToArray());
                    return;
                }
                for (int i = index; i < length; i++) {
                    data.Add(input[i]);
                    Combination(i + 1, r_length, data, ref result, input);
                    data.RemoveAt(data.Count - 1);
                }
            }
            int n = array.Count;
            List<T[]> result = [];
            Combination(0, Comb_length, [], ref result, array);
            return result.ToArray();
        }

        private static float[] ComputeFractionCounterMultipliers(float[] strategyPrediction)
        {
            int n = strategyPrediction.Length; // 5
            int[] order = Enumerable.Range(0, n)
                .OrderByDescending(i => strategyPrediction[i])
                .ToArray();

            const float maxMult = 2.0f;
            const float minMult = 0.5f;
            float ratio = minMult / maxMult; // 0.25

            float[] multiplier = new float[n];
            for (int rank = 0; rank < n; rank++) {
                int fractionIndex = order[rank];
                float t = (float)rank / (n - 1);
                multiplier[fractionIndex] = maxMult * MathF.Pow(ratio, t);
            }
            return multiplier;
        }

        private static Card StrategyAwareGreedyBuyAlgorithm(
            int myHP, int enemyHP, List<Card> availableCards,
            float[] fractiondistribution, float[] strategyPrediction,
            bool inverseOutput = false)
        {
            double[] GetWeightTable(int MyHP, int EnemyHP)
            {
                double AggroThreshold = InitHealth * 0.2;
                double DangerThreshold = InitHealth * 0.3;
                if (EnemyHP <= AggroThreshold || MyHP <= DangerThreshold)
                    return [2.5, 0.5, 0.3, 0.5];
                return [1.0, 1.0, 1.0, 1.5];
            }

            float[] counterMultiplier = ComputeFractionCounterMultipliers(strategyPrediction);
            double[] weightTable = GetWeightTable(myHP, enemyHP);

            Card? bestCard = null;
            double bestScore = double.MinValue;
            foreach (var card in availableCards) {
                double score = 0;
                score += (card.GetTrueAttack() + (card.CardEffect == Card.Effect.AttackBonus ? card.EffectAmount : 0)) * weightTable[0];
                score += (card.Health + (card.CardEffect == Card.Effect.HealthBonus ? card.EffectAmount : 0)) * weightTable[1];
                score += (card.Money + (card.CardEffect == Card.Effect.MoneyBonus ? card.EffectAmount : 0)) * weightTable[2];

                double synergyBase = (fractiondistribution[(int)card.CardFraction] + 0.05) * (13 - (int)card.CardEffect);
                float counterBonus = card.CardFraction != Card.Fraction.None
                    ? counterMultiplier[(int)card.CardFraction]
                    : 1f;
                score += synergyBase * weightTable[3] * counterBonus;

                if (inverseOutput) score = -score;
                if (score > bestScore) { bestScore = score; bestCard = card; }
            }
            return bestCard!;
        }

        private static Card GreedyBuyAlgorithm(int myHP, int enemyHP, List<Card> availableCards, float[] fractiondistribution, bool inverseOutput = false)
        {
            double[] GetWeightTable(int MyHP, int EnemyHP)
            {
                double AggroThreshold = InitHealth * 0.2;
                double DangerThreshold = InitHealth * 0.3;
                if (EnemyHP <= AggroThreshold || MyHP <= DangerThreshold) {
                    return [2.5, 0.5, 0.3, 0.5];
                }
                return [1.0, 1.0, 1.0, 1.5];
            }

            double[] weightTable = GetWeightTable(myHP, enemyHP);
            Card? bestCard = null;
            double bestScore = double.MinValue;
            foreach (var card in availableCards) {
                double score = 0;
                score += (card.GetTrueAttack() + (card.CardEffect == Card.Effect.AttackBonus ? card.EffectAmount : 0)) * weightTable[0];
                score += (card.Health + (card.CardEffect == Card.Effect.HealthBonus ? card.EffectAmount : 0)) * weightTable[1];
                score += (card.Money + (card.CardEffect == Card.Effect.MoneyBonus ? card.EffectAmount : 0)) * weightTable[2];
                score += fractiondistribution[(int)card.CardFraction] * (13 - (int)card.CardEffect) * weightTable[3];
                if (inverseOutput) {
                    score = -score;
                }
                if (score > bestScore) {
                    bestScore = score;
                    bestCard = card;
                }
            }
            return bestCard!;
        }

        private float GetPenalty(int streak, bool player)
        {
            float currentPenalty = player ? this.currentPenalty : this.enemyCurrentPenalty;
            if (streak > 0) {
                float penalty = 0.02f * (float)Math.Pow(2.5, streak - 1);
                currentPenalty = Math.Min(penalty, 1f);
            }
            else {
                currentPenalty *= 0.8f;
                if (currentPenalty < 0.001f)
                    currentPenalty = 0f;
            }
            if (currentPenalty < 0.00001f)
                currentPenalty = 0f;
            if (player)
                this.currentPenalty = currentPenalty;
            else
                this.enemyCurrentPenalty = currentPenalty;
            return currentPenalty;
        }

        public void Update()
        {
            //GamePlay Logic Here
            //EndingCheck
            if (PlayerHealth <= 0) {
                WINNER = GameWinner.Enemy;
                if (ExportEnabled)
                    FlushShoppingEpisode(WINNER);
                return;
            }
            else if (EnemyHealth <= 0) {
                WINNER = GameWinner.Player;
                if (ExportEnabled)
                    FlushShoppingEpisode(WINNER);
                return;
            }
            else if (TurnCount >= MaxTurns) {
                WINNER = PlayerHealth >= EnemyHealth ? GameWinner.Player : GameWinner.Enemy;
                if (ExportEnabled)
                    FlushShoppingEpisode(WINNER);
                return;
            }
            //
            if (playerTurn) {
                //ScrapCards
                if (PlayerCard2ScrapThisTurn != 0) {
                    PlayerCard2ScrapThisTurn = Math.Clamp(PlayerCard2ScrapThisTurn, 0, PlayerHand.Count - 1);
                    for (int i = 0; i < PlayerCard2ScrapThisTurn; i++) {
                        Card scrapped;
                        int scrappedIndex = 0;
                        if (PlayerDecisionMaking == 2) {
                            scrappedIndex = RandomNumberGenerator.GetInt32(0, PlayerHand.Count);
                        }
                        else if (PlayerDecisionMaking == 1) {
                            Card toScrap = GreedyBuyAlgorithm(PlayerHealth, EnemyHealth, PlayerHand, GetFractionDistribution(true).Item1, true);
                            scrappedIndex = PlayerHand.IndexOf(toScrap);
                            if (scrappedIndex == -1)
                                scrappedIndex = RandomNumberGenerator.GetInt32(0, PlayerHand.Count);
                        }
                        else {
                            for (int j = 1; j < PlayerHand.Count; j++) {
                                ModelInput49 input = MLController.CreateDiscardInput(PlayerHand[scrappedIndex], PlayerHand[j], GetStrategyDistribution(true));
                                ModelOutput2 output = MLController.DiscardEngine.Predict(input);
                                if (output.Prediction[0] > output.Prediction[1]) {
                                    scrappedIndex = j;
                                }
                            }
                        }
                        scrapped = PlayerHand[scrappedIndex];

                        PlayerScrap.Add(scrapped);
                        PlayerHand.Remove(scrapped);
                    }
                    PlayerCard2ScrapThisTurn = 0;
                }
                //PlayCards
                if (PlayedPile.Any(card => !card.BaseApplied)) {
                    for (int i = 0; i < PlayedPile.Count; i++) {
                        PlayCard(PlayedPile[i], true);
                    }
                }
                else if (PlayerHand.Count == 0) {
                    //Buy something from shop
                    List<Card> affordableCards = Shop.Where(card => card!.Price <= PlayerMoney).ToList()!;
                    ModelOutput5? strategy = null;
                    strategy = MLController.StrategyEngine.Predict(new ModelInput5() { Features = GetStrategyDistribution(false) });
                    while (affordableCards.Count > 0) {
                        if (ExportEnabled)
                            ExportShoppingDecision(true, strategy.Prediction, PlayerMoney);
                        Card toBuy;
                        if (PlayerDecisionMaking == 3) {
                            toBuy = DeckGenerator.GetCard(affordableCards);
                        }
                        else if (PlayerDecisionMaking == 2) {
                            toBuy = GreedyBuyAlgorithm(PlayerHealth, EnemyHealth, affordableCards, GetFractionDistribution(true).Item1, false);
                        }
                        else if (PlayerDecisionMaking == 1) {
                            toBuy = StrategyAwareGreedyBuyAlgorithm(PlayerHealth, EnemyHealth, affordableCards, GetFractionDistribution(true).Item1, strategy.Prediction, false);
                        }
                        else {
                            Card[] mydeck = PlayerDeck.Concat(PlayerHand).Concat(PlayerScrap).ToArray();
                            Card[] enemydeck = EnemyDeck.Concat(EnemyHand).Concat(EnemyScrap).ToArray();
                            ModelInput158 input104 = MLController.CreateShoppingInput(strategy.Prediction, GetStrategyDistribution(true), GetFractionDistribution(true).Item1, GetFractionDistribution(false).Item1, Shop, PlayerMoney, PlayerHealth / (float)InitHealth, EnemyHealth / (float)InitHealth, mydeck, enemydeck);
                            ModelOutput6 choosen = MLController.ShoppingEngine.Predict(input104);
                            int chosenIndex = -1;
                            float bestScore = float.NegativeInfinity;
                            choosen.Prediction[0] -= Math.Abs(choosen.Prediction[0]) * GetPenalty(zeroSlotStreak, true);
                            for (int i = 0; i < Shop.Length; i++) {
                                if (Shop[i] == null || Shop[i]!.Price > PlayerMoney)
                                    continue;
                                if (choosen.Prediction[i] > bestScore) {
                                    bestScore = choosen.Prediction[i];
                                    chosenIndex = i;
                                }
                            }
                            if (chosenIndex == 0)
                                zeroSlotStreak++;
                            else
                                zeroSlotStreak = 0;
                            toBuy = Shop[chosenIndex]!;
                        }
                        BuyFromShop(toBuy, true);
                        PlayerMoney -= toBuy.Price;
                        affordableCards = Shop.Where(card => card!.Price <= PlayerMoney).ToList()!;
                    }
                    ClearPlayedPile(true);
                    RefillPlayerHand();
                    PlayerMoney = 0;
                    EnemyHealth -= PlayerAttack;
                    PlayerAttack = 0;
                    playerTurn = false;
                }
                else {
                    PlayNext(true);
                }
            }
            else {
                //ScrapCards
                if (EnemyCard2ScrapThisTurn != 0) {
                    EnemyCard2ScrapThisTurn = Math.Clamp(EnemyCard2ScrapThisTurn, 0, EnemyHand.Count - 1);
                    for (int i = 0; i < EnemyCard2ScrapThisTurn; i++) {
                        Card scrapped;
                        if (EnemyDecisionMaking == 3)
                            scrapped = DeckGenerator.GetCard(EnemyHand);
                        else if (EnemyDecisionMaking == 2 || EnemyDecisionMaking == 1) {
                            // heuristic logic One-Step Lookahead
                            double GetScore(Card[] action)
                            {
                                double score = 0;
                                foreach (var card in action) {
                                    int sinergy = action.Count(c => c != card && c.CardFraction == card.CardFraction);
                                    score += ((card.GetTrueAttack() + (card.CardEffect == Card.Effect.AttackBonus ? card.EffectAmount : 0)) * 2) + (card.Health + (card.CardEffect == Card.Effect.HealthBonus ? card.EffectAmount : 0) +
                                         card.Money + (card.CardEffect == Card.Effect.MoneyBonus ? card.EffectAmount : 0) + (sinergy * (13 - (int)card.CardEffect)));
                                }
                                return score;
                            }
                            Card[][] legalactions = GetCombinations(new List<Card>(EnemyHand), EnemyHand.Count - 1);
                            Card[]? bestActions = null;
                            double bestScore = double.MinValue;
                            for (int j = 0; j < legalactions.Length; j++) {
                                double score = GetScore(legalactions[j]);
                                if (score > bestScore) {
                                    bestScore = score;
                                    bestActions = legalactions[j];
                                }
                            }
                            scrapped = EnemyHand.Except(bestActions!).First();
                        }
                        else {
                            int scrapindex = 0;
                            for (int j = 1; j < EnemyHand.Count; j++) {
                                ModelInput49 input = MLController.CreateDiscardInput(EnemyHand[scrapindex], EnemyHand[j], GetStrategyDistribution(false));
                                ModelOutput2 output = MLController.DiscardEngine.Predict(input);
                                if (output.Prediction[0] > output.Prediction[1]) {
                                    scrapindex = j;
                                }
                            }
                            scrapped = EnemyHand[scrapindex];
                        }
                        EnemyScrap.Add(scrapped);
                        EnemyHand.Remove(scrapped);
                    }
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
                    ModelOutput5 strategy = MLController.StrategyEngine.Predict(new ModelInput5() { Features = GetStrategyDistribution(true) });
                    while (affordableCards.Count > 0) {
                        if (ExportEnabled) {
                            ExportShoppingDecision(false, strategy.Prediction, EnemyMoney);
                        }
                        Card toBuy;
                        if (EnemyDecisionMaking == 3)
                            toBuy = affordableCards[RandomNumberGenerator.GetInt32(0, affordableCards.Count)];
                        else if (EnemyDecisionMaking == 2) {
                            toBuy = GreedyBuyAlgorithm(EnemyHealth, PlayerHealth, affordableCards, GetFractionDistribution(false).Item1);
                        }
                        else if (EnemyDecisionMaking == 1) {
                            toBuy = StrategyAwareGreedyBuyAlgorithm(EnemyHealth, PlayerHealth, affordableCards, GetFractionDistribution(false).Item1, strategy.Prediction);
                        }
                        else {
                            Card[] mydeck = EnemyDeck.Concat(EnemyHand).Concat(EnemyScrap).ToArray();
                            Card[] enemydeck = PlayerDeck.Concat(PlayerHand).Concat(PlayerScrap).ToArray();
                            ModelInput158 input104 = MLController.CreateShoppingInput(strategy.Prediction, GetStrategyDistribution(false), GetFractionDistribution(false).Item1, GetFractionDistribution(true).Item1, Shop, EnemyMoney, EnemyHealth / (float)InitHealth, PlayerHealth / (float)InitHealth, mydeck, enemydeck);
                            ModelOutput6 choosen = MLController.ShoppingEngine.Predict(input104);
                            int chosenIndex = -1;
                            float bestScore = float.NegativeInfinity;
                            choosen.Prediction[0] -= Math.Abs(choosen.Prediction[0]) * GetPenalty(enemyZeroSlotStreak, false);
                            for (int i = affordableCards.Count > 1 ? 1 : 0;
                                i < Shop.Length;
                                i++) {
                                if (Shop[i] == null || Shop[i]!.Price > EnemyMoney)
                                    continue;
                                if (choosen.Prediction[i] > bestScore) {
                                    bestScore = choosen.Prediction[i];
                                    chosenIndex = i;
                                }
                            }
                            if (chosenIndex == 0)
                                enemyZeroSlotStreak++;
                            else
                                enemyZeroSlotStreak = 0;
                            toBuy = Shop[chosenIndex]!;
                        }
                        BuyFromShop(toBuy, false);
                        EnemyMoney -= toBuy.Price;
                        affordableCards = Shop.Where(card => card!.Price <= EnemyMoney).ToList()!;
                    }
                    ClearPlayedPile(false);
                    RefillEnemyHand();
                    EnemyMoney = 0;
                    PlayerHealth -= EnemyAttack;
                    EnemyAttack = 0;
                    playerTurn = true;
                    TurnCount++;
                }
                else {
                    PlayNext(false);
                }
                //
            }
            //END of GamePlay Logic
        }

    }
}