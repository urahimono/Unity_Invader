using UnityEngine;

namespace UnityInvader
{
    // Projectile center-based swept interval. Source is the actual projectile Component.
    public readonly struct HitInfo
    {
        public readonly Component Source;
        public readonly Vector2   Start;
        public readonly Vector2   End;
        public readonly Vector2   Size;
        public readonly Vector2   Direction;

        public HitInfo(Component inSource, Vector2 inStart, Vector2 inEnd, Vector2 inSize, Vector2 inDirection)
        {
            Source    = inSource;
            Start     = inStart;
            End       = inEnd;
            Size      = inSize;
            Direction = inDirection;
        }
    }
}
