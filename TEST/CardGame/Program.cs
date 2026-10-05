using System;
using System.Diagnostics;

#nullable enable
namespace CardGame {
    internal class Program {
        private static readonly Stopwatch GlobalStopwatch = new();
        private static readonly Stopwatch InstanceStopwatch = new();
        private static BackGround? bg;
        private static ForeGround? fg;
        private static readonly short enemyDecisionMaking = 3; // 0 - NN, 1 - SHeuristic, 2 - Heuristics, 3 - Random
        private static readonly short playerDecisionMaking = 0; // 0 - NN, 1 - SHeuristic, 2 - Heuristics, 3 - Random
        private static readonly bool exportEnabled = false;
        private static readonly int runs = 100000;
        private static int PlayerWins = 0;
        private static int EnemyWins = 0;
        private static long totalElapsedMilliseconds = 0;
        private static long totalElapsedTicks = 0;

        static void Main(string[] args)
        {
            MLController.Init();

            /*// =====================================================================
            float[] goldenInput = new float[] {
            0.1521f, 0.2852f, 0.0535f, 0.4222f, 0.0869f, 0.8333f, 0.0000f, 0.1667f, 0.0000f, 0.0000f, 1.0000f, 0.2000f, 1.0000f, 0.0000f, 0.0000f, 0.0000f, 0.0000f, 0.0000f, 0.0000f, 0.0000f, 0.0000f, 0.0000f, 0.0000f, 0.0000f, 1.0000f, 0.0000f, 0.0000f, 0.4000f, 0.0000f, 0.0000f, 0.0000f, 0.0000f, 0.0000f, 0.1430f, 1.0000f, 0.5000f, 1.0000f, 0.0000f, 0.0000f, 0.0000f, 0.0000f, 0.0000f, 0.0000f, 0.0000f, 0.0000f, 0.0000f, 1.0000f, 0.0000f, 0.0000f, 0.0000f, 0.1000f, 0.0000f, 0.0000f, 0.0000f, 0.0000f, 0.0000f, 1.0000f, 0.0000f, 1.0000f, 0.3000f, 1.0000f, 0.0000f, 0.0000f, 1.0000f, 0.0000f, 0.0000f, 0.0000f, 0.0000f, 0.0000f, 0.0000f, 0.0000f, 0.0000f, 0.0000f, 0.0000f, 0.0000f, 0.2000f, 0.0000f, 0.0000f, 0.0000f, 0.0000f, 1.0000f, 0.0000f, 1.0000f, 0.4000f, 1.0000f, 0.0000f, 0.0000f, 0.0000f, 0.0000f, 0.0000f, 1.0000f, 0.0000f, 0.0000f, 0.0000f, 0.0000f, 0.0000f, 0.0000f, 0.1667f, 0.0000f, 0.0000f, 0.0000f, 1.0000f, 0.0000f, 0.0000f, 0.0000f, 0.0000f, 1.0000f, 0.4000f, 0.0000f, 0.0000f, 0.0000f, 0.0000f, 1.0000f, 0.0000f, 0.0000f, 0.0000f, 0.0000f, 0.0000f, 0.0000f, 0.0000f, 0.0000f, 0.1111f, 0.1000f, 0.0000f, 0.0000f, 0.0000f, 0.0000f, 0.0000f, 1.0000f, 0.0000f, 1.0000f, 0.3000f, 1.0000f, 0.0000f, 0.0000f, 0.0000f, 0.0000f, 0.0000f, 0.0000f, 0.0000f, 0.0000f, 0.0000f, 0.0000f, 1.0000f, 0.0000f, 0.0556f, 0.0000f, 0.2000f, 0.0000f, 0.0000f, 0.0000f, 0.0000f, 1.0000f, 0.0000f, 1.1333f, 0.5778f, 1.0612f, 0.8623f
            };

            ModelInput158 goldenTest = new ModelInput158 { Features = goldenInput };
            ModelOutput6 goldenResult = MLController.ShoppingEngine.Predict(goldenTest);

            Console.WriteLine("=== GOLDEN OUTPUT TESZT (C#) ===");
            for (int i = 0; i < goldenResult.Prediction.Length; i++)
                Console.WriteLine($"  score[{i}] = {goldenResult.Prediction[i]:F8}");
            Console.WriteLine("=================================");
            Console.WriteLine("Hasonlítsd össze a fenti értékeket a Python \"Várható (Python) kimenet\" sorával.");
            Console.WriteLine("Ha egyeznek (kb. 5-6 tizedesjegyig): a betöltött modellfájl helyes.");
            Console.WriteLine("Ha ELTÉRNEK: a NN\\shoppingAI mappában NEM a frissen tanított modell van.");
            Console.WriteLine("Nyomj meg egy gombot a folytatáshoz.");
            Console.ReadKey();
            // ===================== GOLDEN-OUTPUT TESZT VÉGE =====================*/

            Console.WriteLine("\n\n\nKonzolos alkalmazás elindult...");
            string EnemyString = enemyDecisionMaking == 3 ? "RandomAI" : "Heuristic";

            GlobalStopwatch.Start();
            for (int i = 0; i < runs; i++) {
                InstanceStopwatch.Restart();
                bg = new BackGround();
                MLController.SetMaxValues(bg.Type);
                fg = new ForeGround(bg) { EnemyDecisionMaking = enemyDecisionMaking, PlayerDecisionMaking = playerDecisionMaking, ExportEnabled = exportEnabled };
                while (fg.WINNER == ForeGround.GameWinner.InProgress) {
                    fg.Update();
                }
                if (fg.WINNER == ForeGround.GameWinner.Player) {
                    PlayerWins++;
                }
                else {
                    EnemyWins++;
                }
                InstanceStopwatch.Stop();
                totalElapsedMilliseconds += InstanceStopwatch.ElapsedMilliseconds;
                totalElapsedTicks += InstanceStopwatch.ElapsedTicks;
                if ((i + 1) % (runs / 10) == 0) {
                    Console.WriteLine($"Futás {i + 1}/{runs} befejezve. Jelenlegi állás: NN-AI: {PlayerWins}, {EnemyString}: {EnemyWins}");
                }
            }
            GlobalStopwatch.Stop();

            if (exportEnabled)
                ForeGround.FlushShoppingDataToDisk();

            Console.WriteLine($"A játék véget ért.\n A nyertesek eloszlása:\nNN-AI: {PlayerWins}, {EnemyString}: {EnemyWins}\nNN-AI: {PlayerWins / (float)runs * 100:F2}%, {EnemyString}: {EnemyWins / (float)runs * 100:F2}%");
            Console.WriteLine($"A teljes futásidő: {GlobalStopwatch.ElapsedMilliseconds} ms ==> {new DateTime(GlobalStopwatch.ElapsedTicks).ToString("HH:mm:ss")}");
            Console.WriteLine($"Az átlagos futásidő: {totalElapsedMilliseconds / (float)runs:F2} ms ==> {new DateTime(totalElapsedTicks / runs).ToString("HH:mm:ss")}");

            Console.WriteLine("Nyomj meg egy gombot a kilépéshez.");
            Console.ReadKey();
        }
    }
}