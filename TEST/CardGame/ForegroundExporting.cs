using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

#nullable enable
namespace CardGame {
    internal partial class ForeGround {
        private const int ExportStrategyDim = 5;
        private const int ExportFractionDim = 5;
        private const int ExportEffectDim = 12;
		private const int ExportCardFractionDim = 5;																											  
        private const int ExportSlotDim = 24; // exists + priceRatio + freeEffect + 12 effect + attack + health + money + 5 fraction + synergy

        private static long NextExportEpisodeId;
        private readonly long exportEpisodeId = System.Threading.Interlocked.Increment(ref NextExportEpisodeId);
        private int exportDecisionIndex = 0;
        private readonly List<ShoppingExportRow> exportShoppingRows = [];

        private static readonly List<FinalizedShoppingRow> AllShoppingRows = [];

        private sealed class ShoppingExportRow {
            public required long EpisodeId { get; init; }
            public required int DecisionIndex { get; init; }
            public required int Actor { get; init; } // 1 = player, 0 = enemy
            public required float[] Features { get; init; }
            public required int Action { get; init; }
            public required int Money { get; init; }
            public required int MyHealth { get; init; }
            public required int EnemyHealth { get; init; }
            public required float MyDeckValue { get; init; }
            public required float EnemyDeckValue { get; init; }
        }

        private sealed class FinalizedShoppingRow {
            public required long EpisodeId { get; init; }
            public required int DecisionIndex { get; init; }
            public required int Actor { get; init; }
            public required float[] Features { get; init; }
            public required int Action { get; init; }
            public required int Money { get; init; }
            public required int MyHealth { get; init; }
            public required int EnemyHealth { get; init; }
            public required float MyDeckValue { get; init; }
            public required float EnemyDeckValue { get; init; }
            public required int Reward { get; init; }
            public required int Done { get; init; }
        }

        private static DateTime ExportStartTime { get; } = DateTime.Now;
        private static string ShoppingExportPath => Path.Combine(AppContext.BaseDirectory, $"shopping_experience_{ExportStartTime:yyyyMMdd_HHmmss}.csv");

        private static string Csv(float value) => value.ToString("R", CultureInfo.InvariantCulture);
        private static string Csv(int value) => value.ToString(CultureInfo.InvariantCulture);
        private static string Csv(long value) => value.ToString(CultureInfo.InvariantCulture);

        private static void EnsureShoppingCsvHeader()
        {
            if (File.Exists(ShoppingExportPath))
                return;

            StringBuilder sb = new();
            List<string> columns = [
                "episode",
                "decision",
                "actor",
                "money",
                "my_health",
                "enemy_health",
                "my_deck_value",
                "enemy_deck_value"
            ];

            for (int i = 0; i < ExportStrategyDim; i++)
                columns.Add($"strategy_{i}");
            for (int i = 0; i < ExportFractionDim; i++)
                columns.Add($"synergy_fraction_{i}");

            for (int slot = 0; slot < 6; slot++) {
                columns.Add($"slot{slot}_exists");
                columns.Add($"slot{slot}_price_ratio");
                columns.Add($"slot{slot}_free_effect");
                for (int effect = 0; effect < ExportEffectDim; effect++)
                    columns.Add($"slot{slot}_effect_{effect}");
				columns.Add($"slot{slot}_attack");
                columns.Add($"slot{slot}_health");
                columns.Add($"slot{slot}_money");
                for (int fraction = 0; fraction < ExportCardFractionDim; fraction++)
                    columns.Add($"slot{slot}_fraction_{fraction}");
                columns.Add($"slot{slot}_synergy");								  
            }

            columns.Add("action");
            columns.Add("reward");
            columns.Add("done");
            sb.AppendLine(string.Join(",", columns));
            File.WriteAllText(ShoppingExportPath, sb.ToString(), Encoding.UTF8);
        }

        private static double ScoreCardForDeckValue(Card card, float[] fractiondistribution)
        {
            const double wAtk = 1.0, wHp = 1.0, wMoney = 1.0, wSynergy = 1.5;
            double score = 0;
            score += (card.GetTrueAttack() + (card.CardEffect == Card.Effect.AttackBonus ? card.EffectAmount : 0)) * wAtk;
            score += (card.Health + (card.CardEffect == Card.Effect.HealthBonus ? card.EffectAmount : 0)) * wHp;
            score += (card.Money + (card.CardEffect == Card.Effect.MoneyBonus ? card.EffectAmount : 0)) * wMoney;
            score += fractiondistribution[(int)card.CardFraction] * (13 - (int)card.CardEffect) * wSynergy;
            return score;
        }

        private float ComputeDeckValue(bool player)
        {
            List<Card> owned = player
                ? PlayerHand.Concat(PlayerDeck).Concat(PlayerScrap).ToList()
                : EnemyHand.Concat(EnemyDeck).Concat(EnemyScrap).ToList();
            float[] fractiondistribution = GetFractionDistribution(player).Item1;

            double total = 0;
            foreach (Card card in owned)
                total += ScoreCardForDeckValue(card, fractiondistribution);
            return (float)total;
        }

        private static float[] BuildCardFeature(Card? card, int availableMoney, float[] fractiondistribution6)
        {
            float[] result = new float[ExportSlotDim];

            // 0: exists = a háló számára ténylegesen megvehető lap.
            if (card == null || card.Price > availableMoney)
                return result;

            result[0] = 1f;

            // 1: költség
            result[1] = availableMoney > 0
                ? Math.Clamp((float)card.Price / availableMoney, 0f, 1f)
                : 0f;

            // 2: nincs frakció-követelmény de van tényleges effektus. ==> azaz ingyenes effektus
            result[2] = card.EffectRequirement == Card.Fraction.None &&
                        card.CardEffect != Card.Effect.None
                ? 1f
                : 0f;

            // 3..14: effect one-hot
            for (int i = 0; i < ExportEffectDim; i++)
                result[3 + i] = card.CardEffect == (Card.Effect)i ? 1f : 0f;

			// 15..17: Attack, Health, Money
            int idx = 3 + ExportEffectDim; // 15
            result[idx++] = (card.GetTrueAttack() + (card.CardEffect == Card.Effect.AttackBonus ? card.EffectAmount : 0)) / (float)MLController.AttackMax;
            result[idx++] = (card.Health + (card.CardEffect == Card.Effect.HealthBonus ? card.EffectAmount : 0)) / (float)MLController.HealthMax;
            result[idx++] = (card.Money + (card.CardEffect == Card.Effect.MoneyBonus ? card.EffectAmount : 0)) / (float)MLController.MaxMoney;
            // idx = 18

            // 18..22: a lap saját frakciója (5)
            for (int i = 0; i < ExportCardFractionDim; i++)
                result[idx + i] = card.CardFraction == (Card.Fraction)i ? 1f : 0f;
            idx += ExportCardFractionDim; // idx = 23

            // 23: szinergia
            result[idx] = fractiondistribution6[(int)card.CardFraction] * (13 - (int)card.CardEffect) / 13f;
			
            return result;
        }

        // Shopping döntés előtt hívandó !!!
        private void ExportShoppingDecision(
            bool player,
            float[] strategyDistribution,
            int availableMoney)
        {

            EnsureShoppingCsvHeader();
            if (strategyDistribution.Length != ExportStrategyDim)
                throw new ArgumentException("Strategy distribution must contain exactly 5 values.", nameof(strategyDistribution));

            float[] ownFractionDistribution = GetStrategyDistribution(player);
			float[] ownFractionDistribution6 = GetFractionDistribution(player).Item1;																		 
            float[] features = new float[ExportStrategyDim + ExportFractionDim + (6 * ExportSlotDim)];
            int offset = 0;

            for (int i = 0; i < ExportStrategyDim; i++)
                features[offset++] = strategyDistribution[i];

            for (int i = 0; i < ExportFractionDim; i++)
                features[offset++] = ownFractionDistribution[i];

            for (int slot = 0; slot < 6; slot++) {
                float[] cardFeatures = BuildCardFeature(Shop[slot], availableMoney, ownFractionDistribution6);
                Array.Copy(cardFeatures, 0, features, offset, cardFeatures.Length);
                offset += cardFeatures.Length;
            }

            exportShoppingRows.Add(new ShoppingExportRow {
                EpisodeId = exportEpisodeId,
                DecisionIndex = exportDecisionIndex++,
                Actor = player ? 1 : 0,
                Features = features,
                Action = -1, // ExportShoppingAction tölti ki közvetlenül a Predict után.
                Money = availableMoney,
                MyHealth = player ? PlayerHealth : EnemyHealth,
                EnemyHealth = player ? EnemyHealth : PlayerHealth,
                MyDeckValue = ComputeDeckValue(player),
                EnemyDeckValue = ComputeDeckValue(!player)
            });
        }

        // Akkor kell hívni amikor már megvan a slot index. !!!
        private void ExportShoppingAction(int chosenSlot)
        {
            if (exportShoppingRows.Count == 0)
                return;

            ShoppingExportRow old = exportShoppingRows[^1];
            exportShoppingRows[^1] = new ShoppingExportRow {
                EpisodeId = old.EpisodeId,
                DecisionIndex = old.DecisionIndex,
                Actor = old.Actor,
                Features = old.Features,
                Action = chosenSlot,
                Money = old.Money,
                MyHealth = old.MyHealth,
                EnemyHealth = old.EnemyHealth,
                MyDeckValue = old.MyDeckValue,
                EnemyDeckValue = old.EnemyDeckValue
            };
        }

        // A játék végén hívandó. !!!
        private void FlushShoppingEpisode(GameWinner winner)
        {
            if (exportShoppingRows.Count == 0)
                return;

            int lastPlayerIndex = -1;
            int lastEnemyIndex = -1;
            for (int i = 0; i < exportShoppingRows.Count; i++) {
                if (exportShoppingRows[i].Actor == 1) lastPlayerIndex = i;
                else lastEnemyIndex = i;
            }

            for (int i = 0; i < exportShoppingRows.Count; i++) {
                ShoppingExportRow row = exportShoppingRows[i];
                bool isLastForActor = row.Actor == 1 ? i == lastPlayerIndex : i == lastEnemyIndex;
                int done = isLastForActor ? 1 : 0;

                int reward = 0;
                if (isLastForActor && winner != GameWinner.InProgress) {
                    bool actorWon = (row.Actor == 1 && winner == GameWinner.Player) ||
                                     (row.Actor == 0 && winner == GameWinner.Enemy);
                    reward = actorWon ? 1 : -1;
                }

                AllShoppingRows.Add(new FinalizedShoppingRow {
                    EpisodeId = row.EpisodeId,
                    DecisionIndex = row.DecisionIndex,
                    Actor = row.Actor,
                    Features = row.Features,
                    Action = row.Action,
                    Money = row.Money,
                    MyHealth = row.MyHealth,
                    EnemyHealth = row.EnemyHealth,
                    MyDeckValue = row.MyDeckValue,
                    EnemyDeckValue = row.EnemyDeckValue,
                    Reward = reward,
                    Done = done,
                });
            }

            exportShoppingRows.Clear();
            exportDecisionIndex = 0;
        }

        internal static void FlushShoppingDataToDisk()
        {
            if (AllShoppingRows.Count == 0)
                return;

            EnsureShoppingCsvHeader();
            using StreamWriter writer = new(ShoppingExportPath, append: true, Encoding.UTF8);

            foreach (FinalizedShoppingRow row in AllShoppingRows) {
                List<string> values = [
                    Csv(row.EpisodeId),
                    Csv(row.DecisionIndex),
                    Csv(row.Actor),
                    Csv(row.Money),
                    Csv(row.MyHealth),
                    Csv(row.EnemyHealth),
                    Csv(row.MyDeckValue),
                    Csv(row.EnemyDeckValue)
                ];
                values.AddRange(row.Features.Select(Csv));
                values.Add(Csv(row.Action));
                values.Add(Csv(row.Reward));
                values.Add(Csv(row.Done));
                writer.WriteLine(string.Join(",", values));
            }

            int writtenCount = AllShoppingRows.Count;
            AllShoppingRows.Clear();
            Console.WriteLine($"Shopping export: {writtenCount} sor kiírva ide: {ShoppingExportPath}");
        }
    }
}