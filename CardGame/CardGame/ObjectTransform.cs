using System;
using Microsoft.Xna.Framework;

#nullable enable
namespace CardGame {
    internal class ObjectTransform {
        /// <summary>
        /// Gets the default duration used for transform operations.
        /// </summary>
        /// <remarks>This value represents the standard time interval applied to transformations when no
        /// specific duration is provided. It can be used as a baseline for animation or timing-related features that
        /// require a default transform time.</remarks>
        public static TimeSpan DefaultTransformTime { get; } = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 2);
        private Rectangle _moveTarget;
        private Rectangle _startLocation;
        private TimeSpan _transformBegin = TimeSpan.Zero;
        /// <summary>
        /// Gets or sets the target rectangle to which the object will be moved.
        /// </summary>
        public Rectangle MoveTarget
        {
            get => _moveTarget;
            set {
                _moveTarget = value;
                IsTransforming = true;
            }
        }
        /// <summary>
        /// Gets or sets the initial location and size of the rectangle before any transformations are applied.
        /// </summary>
        /// <remarks>Setting this property also updates the current location to match the new start
        /// location and marks the object as being in a transforming state.</remarks>
        public Rectangle StartLocation
        {
            get => _startLocation;
            set {
                _startLocation = value;
                CurrentLocation = value;
                _transformBegin = TimeSpan.Zero;
                IsTransforming = true;
            }
        }
        /// <summary>
        /// Gets the current location and size as a rectangle.
        /// </summary>
        public Rectangle CurrentLocation { get; private set; }
        /// <summary>
        /// Gets or sets the time interval to apply as a transformation during processing.
        /// If null, the default transform time will be used.
        /// </summary>
        public TimeSpan? TransformTime { get; set; } = null;
        /// <summary>
        /// Gets a value indicating whether a transformation operation is currently in progress.
        /// </summary>
        public bool IsTransforming { get; private set; } = false;

        /// <summary>
        /// Advances the transformation animation by one step based on the specified game time.
        /// </summary>
        /// <remarks>Call this method repeatedly, typically once per frame, to animate the transformation.
        /// When the transformation completes, the method returns false and the object's location is updated to the
        /// target position.</remarks>
        /// <param name="gameTime">The current game time, used to determine the progress of the transformation.</param>
        /// <returns>true if the transformation is still in progress; otherwise, false.</returns>
        public bool NextStep(GameTime gameTime)
        {
            if (!IsTransforming) {
                return false;
            }
            if (_transformBegin == TimeSpan.Zero) {
                _transformBegin = gameTime.TotalGameTime;
            }
            TimeSpan transformDuration = TransformTime ?? DefaultTransformTime;
            TimeSpan timeElapsed = gameTime.TotalGameTime - _transformBegin;
            if (timeElapsed >= transformDuration) {
                _transformBegin = TimeSpan.Zero;
                _startLocation = _moveTarget;
                CurrentLocation = _moveTarget;
                IsTransforming = false;
                return false;
            }
            float progress = (float)(timeElapsed.TotalMilliseconds / transformDuration.TotalMilliseconds);
            int newX = (int)(_startLocation.X + ((_moveTarget.X - _startLocation.X) * progress));
            int newY = (int)(_startLocation.Y + ((_moveTarget.Y - _startLocation.Y) * progress));
            int newWidth = (int)(_startLocation.Width + ((_moveTarget.Width - _startLocation.Width) * progress));
            int newHeight = (int)(_startLocation.Height + ((_moveTarget.Height - _startLocation.Height) * progress));
            CurrentLocation = new Rectangle(newX, newY, newWidth, newHeight);
            return true;
        }

        /// <summary>
        /// Initializes a new instance of the ObjectTransform class with the specified starting location.
        /// </summary>
        /// <param name="startLocation">The initial location and size of the object, represented as a Rectangle.</param>
        public ObjectTransform(Rectangle startLocation)
        {
            _startLocation = startLocation;
            CurrentLocation = startLocation;
            _moveTarget = startLocation;
        }

        /// <summary>
        /// Initializes a new instance of the ObjectTransform class with the specified starting location and movement
        /// target.
        /// </summary>
        /// <param name="startLocation">The initial location of the object before transformation begins.</param>
        /// <param name="moveTarget">The target location to which the object will be moved during the transformation.</param>
        public ObjectTransform(Rectangle startLocation, Rectangle moveTarget)
        {
            _startLocation = startLocation;
            CurrentLocation = startLocation;
            _moveTarget = moveTarget;
            IsTransforming = true;
        }
    }
}
