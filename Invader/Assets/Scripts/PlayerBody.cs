using System;

namespace UnityInvader
{
    // Logical pixels, independent of Unity transforms. All values are provisional.
    public sealed class PlayerBody
    {
        public const int ScreenWidth  = 224;
        public const int ScreenHeight = 256;
        public const int SpriteSize   = 16;
        public const int MinimumX     = SpriteSize / 2;
        public const int MaximumX     = ScreenWidth - SpriteSize / 2;

        public int X { get; private set; } = ScreenWidth / 2;

        public int Y => 24;

        public void Tick(PlayerInputState inInput)
        {
            int direction = inInput.Right ? 1 : inInput.Left ? -1 : 0;
            X             = Math.Max(MinimumX, Math.Min(MaximumX, X + direction));
        }
    }
}
