using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace CardGame {
    internal class MouseInfo(MouseState current) {
        public MouseState Current { get; private set; } = current;
        public MouseState Previous { get; private set; } = current;
        public int WheelDelta { get; private set; } = 0;

        public Point GetMousePosition() => Current.Position;

        public void Update(MouseState newCurrent)
        {
            if (!DisplayInfo.IsFocused) return;
            Previous = Current;
            Current = newCurrent;
            WheelDelta = (Current.ScrollWheelValue - Previous.ScrollWheelValue) / 120;
        }

    }
}
