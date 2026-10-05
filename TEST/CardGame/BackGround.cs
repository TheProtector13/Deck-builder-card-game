using System;

namespace CardGame {
    internal class BackGround(BackGround.BackGroundType? planetType = null) {
        public BackGroundType Type { get; init; } = planetType == null ? (BackGroundType)Random.Shared.Next(0, 3) : (BackGroundType)planetType;

        public enum BackGroundType {
            Forest,
            Ice,
            Desert
        }
    }
}
