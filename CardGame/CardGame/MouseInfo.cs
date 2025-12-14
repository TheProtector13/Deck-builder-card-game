using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CardGame
{
    internal class MouseInfo(MouseState current)
    {
        public MouseState Current { get; private set; } = current;
        public MouseState Previous { get; private set; } = current;
        public int WheelDelta { get; private set; } = 0;

        public Point GetMousePosition() => Current.Position;

        public void Update(MouseState newCurrent)
        {
            Previous = Current;
            Current = newCurrent;
            WheelDelta = Current.ScrollWheelValue - Previous.ScrollWheelValue;
        }


    }
}
