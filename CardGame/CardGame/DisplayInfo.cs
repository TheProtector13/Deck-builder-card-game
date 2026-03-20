using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CardGame {
    internal static class DisplayInfo {
        /// <summary>
        /// Gets the width of the screen in pixels.
        /// </summary>
        public static int ScreenWidth { get; private set; }
        /// <summary>
        /// Gets the height of the screen in pixels.
        /// </summary>
        public static int ScreenHeight { get; private set; }
        /// <summary>
        /// Gets the aspect ratio of the current display or rendering surface.
        /// </summary>
        /// <remarks>The aspect ratio is typically used in graphics or UI calculations to ensure proper
        /// scaling and alignment of visual elements. This property is read-only and reflects the current state of the
        /// display or rendering context.</remarks>
        public static float AspectRatio { get; private set; }
        /// <summary>
        /// Gets the dimensions of the screen as a <see cref="Rectangle"/>.
        /// </summary>
        public static Rectangle ScreenRect { get; private set; }

        /// <summary>
        /// Gets or sets a value indicating whether the game itself has focus.
        /// </summary>
        public static bool IsFocused { get; set; } = true;

        static DisplayInfo()
        {
            ScreenWidth = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width;
            ScreenHeight = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height;
            AspectRatio = (float)ScreenWidth / ScreenHeight;
            ScreenRect = new Rectangle(0, 0, ScreenWidth, ScreenHeight);
        }

        public static void Init()
        {
            ScreenWidth = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width;
            ScreenHeight = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height;
            AspectRatio = (float)ScreenWidth / ScreenHeight;
            ScreenRect = new Rectangle(0, 0, ScreenWidth, ScreenHeight);
        }

        public static int GetPXfromWidth(double percent) => (int)Math.Round(ScreenWidth * percent);

        public static int GetPXfromHeight(double percent) => (int)Math.Round(ScreenHeight * percent);

        public static double GetPercentFromWidth(int px) => (double)px / ScreenWidth;

        public static double GetPercentFromHeight(int px) => (double)px / ScreenHeight;

        /// <summary>
        /// Centers a target rectangle within a specified screen rectangle.
        /// </summary>
        /// <param name="target">The rectangle to be centered.</param>
        /// <param name="screen">The rectangle within which the target rectangle will be centered.</param>
        /// <returns>A new <see cref="Rectangle"/> representing the target rectangle positioned at the center of the screen
        /// rectangle. The size of the target rectangle remains unchanged.</returns>
        public static Rectangle CenterRect(Rectangle target, Rectangle screen)
        {
            return new Rectangle(
                screen.X + ((screen.Width - target.Width) / 2),
                screen.Y + ((screen.Height - target.Height) / 2),
                target.Width,
                target.Height);
        }

        /// <summary>
        /// Adjusts the dimensions of the target rectangle to fill the screen rectangle while maintaining the aspect ratio of the
        /// target rectangle.
        /// </summary>
        /// <param name="target">The rectangle to be adjusted.</param>
        /// <param name="screen">The rectangle representing the screen dimensions.</param>
        /// <returns>A new <see cref="Rectangle"/> that represent what needs to be cut out and rescaled from the <paramref name="target"/> rectangle
        /// in order to fill the <paramref name="screen"/> rectangel.</returns>
        public static Rectangle FillRect(Rectangle target, Rectangle screen)
        {
            double screenRatio = (double)screen.Width / screen.Height;
            double targetRatio = (double)target.Width / target.Height;
            return new Rectangle(
                screenRatio >= targetRatio ? 0 : (target.Width - (int)(target.Height * screenRatio)) / 2,
                screenRatio <= targetRatio ? 0 : (target.Height - (int)(target.Width / screenRatio)) / 2,
                screenRatio >= targetRatio ? target.Width : (int)(target.Height * screenRatio),
                screenRatio <= targetRatio ? target.Height : (int)(target.Width / screenRatio));
        }

        /// <summary>
        /// Adjusts the dimensions of the specified rectangle to fill the display area while maintaining its aspect
        /// ratio.
        /// </summary>
        /// <param name="target">The rectangle to be adjusted.</param>
        /// <returns>A new <see cref="Rectangle"/> that represent what needs to be cut out and rescaled from the <paramref name="target"/> rectangle
        /// in order to fill the display area.</returns>
        public static Rectangle FillRect(Rectangle target)
        {
            return FillRect(target, new Rectangle(0, 0, ScreenWidth, ScreenHeight));
        }

        /// <summary>
        /// Calculates the largest rectangle with the same aspect ratio as the specified image that fits within the
        /// target rectangle and is aligned to the bottom edge.
        /// </summary>
        /// <param name="image">The rectangle representing the original image whose aspect ratio is to be preserved.</param>
        /// <param name="target">The rectangle within which the image should be fitted and aligned to the bottom.</param>
        /// <returns>A rectangle representing the fitted area within the target rectangle, maintaining the image's aspect ratio
        /// and aligned to the bottom edge.</returns>
        public static Rectangle FitRectBottom(Rectangle image, Rectangle target)
        {
            double imageRatio = (double)image.Width / image.Height;
            double targetRatio = (double)target.Width / target.Height;

            int width, height;

            if (targetRatio > imageRatio) {
                // Fit by height
                height = target.Height;
                width = (int)(height * imageRatio);
            }
            else {
                // Fit by width
                width = target.Width;
                height = (int)(width / imageRatio);
            }

            int x = target.X + ((target.Width - width) / 2);
            int y = target.Y + target.Height - height;

            return new Rectangle(x, y, width, height);
        }
    }
}
