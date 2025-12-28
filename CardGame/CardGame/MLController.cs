using System;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace CardGame {
    public class ModelInput78 {
        [VectorType(78)]
        [ColumnName("serving_default_keras_tensor")]
        public float[] Features { get; set; }
    }

    public class ModelInput26 {
        [VectorType(26)]
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
        private static readonly MLContext mlContext;

        public static ITransformer DiscardModel { get; private set; }
        public static ITransformer ShoppingModel { get; private set; }
        public static ITransformer StrategyModel { get; private set; }

        public static PredictionEngine<ModelInput26, ModelOutput2> DiscardEngine { get; private set; }
        public static PredictionEngine<ModelInput78, ModelOutput6> ShoppingEngine { get; private set; }
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

            var emptyData = mlContext.Data.LoadFromEnumerable<ModelInput26>(Array.Empty<ModelInput26>());
            DiscardModel = discardPipeline.Fit(emptyData);
            DiscardEngine = mlContext.Model.CreatePredictionEngine<ModelInput26, ModelOutput2>(DiscardModel);

            emptyData = mlContext.Data.LoadFromEnumerable<ModelInput78>(Array.Empty<ModelInput78>());
            ShoppingModel = shoppingPipeline.Fit(emptyData);
            ShoppingEngine = mlContext.Model.CreatePredictionEngine<ModelInput78, ModelOutput6>(ShoppingModel);

            emptyData = mlContext.Data.LoadFromEnumerable<ModelInput5>(Array.Empty<ModelInput5>());
            StrategyModel = strategyPipeline.Fit(emptyData);
            StrategyEngine = mlContext.Model.CreatePredictionEngine<ModelInput5, ModelOutput5>(StrategyModel);
        }

        public static void Init() { }
    }
}
