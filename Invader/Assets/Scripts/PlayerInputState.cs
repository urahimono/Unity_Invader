namespace UnityInvader
{
    public readonly struct PlayerInputState
    {
        public bool Left  { get; }
        public bool Right { get; }
        public bool Fire  { get; }

        public PlayerInputState(bool inLeft, bool inRight, bool inFire = false)
        {
            Left  = inLeft;
            Right = inRight;
            Fire  = inFire;
        }
    }
}
