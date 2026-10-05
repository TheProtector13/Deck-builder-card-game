using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ML;
using Microsoft.ML.Data;
using static CardGame.BackGround;

namespace CardGame {
    public class ModelInput158 {
        [VectorType(158)]
        [ColumnName("serving_default_keras_tensor")]
        public float[] Features { get; set; }
    }

    public class ModelInput49 {
        [VectorType(49)]
        [ColumnName("serving_default_keras_tensor")]
        public float[] Features { get; set; }
    }

    public class ModelInput5 {
        [VectorType(5)]
        [ColumnName("serving_default_keras_tensor")]
        public float[] Features { get; set; }
    }

    public class ModelOutput6 {
        [VectorType(6)]
        [ColumnName("StatefulPartitionedCall_1")]
        public float[] Prediction { get; set; }
    }

    public class ModelOutput5 {
        [VectorType(5)]
        [ColumnName("StatefulPartitionedCall_1")]
        public float[] Prediction { get; set; }
    }

    public class ModelOutput2 {
        [VectorType(2)]
        [ColumnName("StatefulPartitionedCall_1")]
        public float[] Prediction { get; set; }
    }

    internal static class MLController {
        public static int MaxPrice { get; set; } = 8;
        public static int MaxMoney { get; set; } = 5;
        public static int HealthMax { get; set; } = 10;
        public static int AttackMax { get; set; } = 18;
        private static readonly MLContext mlContext;

        public static ITransformer DiscardModel { get; private set; }
        public static ITransformer ShoppingModel { get; private set; }
        public static ITransformer StrategyModel { get; private set; }

        public static PredictionEngine<ModelInput49, ModelOutput2> DiscardEngine { get; private set; }
        public static PredictionEngine<ModelInput158, ModelOutput6> ShoppingEngine { get; private set; }
        public static PredictionEngine<ModelInput5, ModelOutput5> StrategyEngine { get; private set; }

        static MLController()
        {
            mlContext = new MLContext();

            // Load pre-trained TensorFlow models
            var discardModel = mlContext.Model.LoadTensorFlowModel("NN\\discardingAI");
            var shoppingModel = mlContext.Model.LoadTensorFlowModel("NN\\shoppingAI");
            var strategyModel = mlContext.Model.LoadTensorFlowModel("NN\\strategyAI");

            var discardPipeline = discardModel.ScoreTensorFlowModel(
                outputColumnNames: new[] { "StatefulPartitionedCall_1" },
                inputColumnNames: new[] { "serving_default_keras_tensor" },
                addBatchDimensionInput: false);
            var shoppingPipeline = shoppingModel.ScoreTensorFlowModel(
                outputColumnNames: new[] { "StatefulPartitionedCall_1" },
                inputColumnNames: new[] { "serving_default_keras_tensor" },
                addBatchDimensionInput: false);
            var strategyPipeline = strategyModel.ScoreTensorFlowModel(
                outputColumnNames: new[] { "StatefulPartitionedCall_1" },
                inputColumnNames: new[] { "serving_default_keras_tensor" },
                addBatchDimensionInput: false);

            var emptyData = mlContext.Data.LoadFromEnumerable<ModelInput49>(Array.Empty<ModelInput49>());
            DiscardModel = discardPipeline.Fit(emptyData);
            DiscardEngine = mlContext.Model.CreatePredictionEngine<ModelInput49, ModelOutput2>(DiscardModel);

            emptyData = mlContext.Data.LoadFromEnumerable<ModelInput158>(Array.Empty<ModelInput158>());
            ShoppingModel = shoppingPipeline.Fit(emptyData);
            ShoppingEngine = mlContext.Model.CreatePredictionEngine<ModelInput158, ModelOutput6>(ShoppingModel);

            emptyData = mlContext.Data.LoadFromEnumerable<ModelInput5>(Array.Empty<ModelInput5>());
            StrategyModel = strategyPipeline.Fit(emptyData);
            StrategyEngine = mlContext.Model.CreatePredictionEngine<ModelInput5, ModelOutput5>(StrategyModel);
        }

        public static void Init() { }

        public static void SetMaxValues(BackGroundType terrainType)
        {
            Card[] allCards = [.. DeckGenerator.GenSingleDeck(terrainType), .. DeckGenerator.GenStartDeck(), .. DeckGenerator.GetMoneyCards()];
            MaxPrice = allCards.Max(c => c.Price);
            MaxMoney = allCards.Max(c => c.Money + (c.CardEffect == Card.Effect.MoneyBonus ? c.EffectAmount : 0));
            HealthMax = allCards.Max(c => c.Health + (c.CardEffect == Card.Effect.HealthBonus ? c.EffectAmount : 0));
            AttackMax = allCards.Max(c => c.GetTrueAttack() + (c.CardEffect == Card.Effect.AttackBonus ? c.EffectAmount : 0));
        }

        public static ModelInput49 CreateDiscardInput(Card card1, Card card2, float[] fractiondistribution)
        {
            static float[] CardToInputArray(Card card)
            {
                float[] output = new float[22];
                output[0] = card.Price / (float)MaxPrice;
                for (int i = 0; i < (int)Card.Fraction.None; i++) {
                    output[1 + i] = (card.CardFraction == (Card.Fraction)i) ? 1f : 0f;
                }
                for (int i = 0; i < (int)Card.Effect.None; i++) {
                    output[1 + (int)Card.Fraction.None + i] = (card.CardEffect == (Card.Effect)i) ? 1f : 0f;
                }
                int index = 1 + (int)Card.Fraction.None + (int)Card.Effect.None;
                output[index++] = (card.CardEffect != Card.Effect.None && card.EffectRequirement == Card.Fraction.None) ? 1f : 0f; //free effect
                output[index++] = (card.GetTrueAttack() + (card.CardEffect == Card.Effect.AttackBonus ? card.EffectAmount : 0)) / (float)AttackMax;
                output[index++] = (card.Health + (card.CardEffect == Card.Effect.HealthBonus ? card.EffectAmount : 0)) / (float)HealthMax;
                output[index++] = (card.Money + (card.CardEffect == Card.Effect.MoneyBonus ? card.EffectAmount : 0)) / (float)MaxMoney;
                return output;
            }
            float[] features = [.. fractiondistribution, .. CardToInputArray(card1), .. CardToInputArray(card2)];
            return new ModelInput49 { Features = features };
        }

        public static ModelInput158 CreateShoppingInput(float[] strategyAIoutput, float[] fractiondistribution, float[] myfullfractiondistribution, float[] enemyfullfractiondistribution, Card?[] cards, float money, float myhealth, float enemyhealth, Card[] myDeck, Card[] enemyDeck)
        {
            static float GetDeckValue(Card[] deck, float[] fractiondistribution)
            {
                static double GetCardValue(Card card, float[] fractiondistribution)
                {
                    const double wAtk = 1.0, wHp = 1.0, wMoney = 1.0, wSynergy = 1.5;
                    double score = 0;
                    score += (card.GetTrueAttack() + (card.CardEffect == Card.Effect.AttackBonus ? card.EffectAmount : 0)) * wAtk;
                    score += (card.Health + (card.CardEffect == Card.Effect.HealthBonus ? card.EffectAmount : 0)) * wHp;
                    score += (card.Money + (card.CardEffect == Card.Effect.MoneyBonus ? card.EffectAmount : 0)) * wMoney;
                    score += fractiondistribution[(int)card.CardFraction] * (13 - (int)card.CardEffect) * wSynergy;
                    return score;
                }
                double total = 0;
                foreach (Card card in deck) {
                    total += GetCardValue(card, fractiondistribution);
                }
                return (float)total;
            }

            const int slotDim = 24;
            if (cards.Length > 6)
                throw new ArgumentException("cards array cannot have more than 6 elements");
            List<float> cardFeatures = [];
            for (int slot = 0; slot < 6; slot++) {
                Card? card = slot < cards.Length ? cards[slot] : null;
                float[] featueres = new float[slotDim];

                if (card != null && card.Price <= money) {
                    featueres[0] = 1f; //exists
                    featueres[1] = money > 0 ? Math.Clamp((float)card.Price / money, 0f, 1f) : 0f;
                    featueres[2] = (card.EffectRequirement == Card.Fraction.None && card.CardEffect != Card.Effect.None) ? 1f : 0f;
                    for (int i = 0; i < (int)Card.Effect.None; i++) {
                        featueres[3 + i] = (card.CardEffect == (Card.Effect)i) ? 1f : 0f;
                    }

                    int idx = 3 + (int)Card.Effect.None; // 15
                    featueres[idx++] = (card.GetTrueAttack() + (card.CardEffect == Card.Effect.AttackBonus ? card.EffectAmount : 0)) / (float)AttackMax;
                    featueres[idx++] = (card.Health + (card.CardEffect == Card.Effect.HealthBonus ? card.EffectAmount : 0)) / (float)HealthMax;
                    featueres[idx++] = (card.Money + (card.CardEffect == Card.Effect.MoneyBonus ? card.EffectAmount : 0)) / (float)MaxMoney;
                    // idx = 18

                    for (int i = 0; i < (int)Card.Fraction.None; i++) {
                        featueres[idx + i] = (card.CardFraction == (Card.Fraction)i) ? 1f : 0f;
                    }
                    idx += (int)Card.Fraction.None; // idx = 23

                    featueres[idx] = myfullfractiondistribution[(int)card.CardFraction] * (13 - (int)card.CardEffect) / 13f;
                }

                cardFeatures.AddRange(featueres);
            }
            return new ModelInput158 { Features = [.. strategyAIoutput, .. fractiondistribution, .. cardFeatures, myhealth, enemyhealth, GetDeckValue(myDeck, myfullfractiondistribution), GetDeckValue(enemyDeck, enemyfullfractiondistribution)] };
        }

    }
}
