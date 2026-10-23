using System;

namespace UnityInvader
{
    // Logical pixels, independent of Unity transforms. All values are provisional.
    public sealed class PlayerModel
    {
        public const int ScreenWidth  = 224;
        public const int ScreenHeight = 256;
        public const int SpriteSize   = 16;
        public const int MinimumX     = SpriteSize / 2;
        public const int MaximumX     = ScreenWidth - SpriteSize / 2;

        public int X { get; private set; } = ScreenWidth / 2;
        public int Y => 24;

        public void Tick(PlayerInputState input)
        {
            int direction = input.Right ? 1 : input.Left ? -1 : 0;
            X = Math.Max(MinimumX, Math.Min(MaximumX, X + direction));
        }
    }
}
