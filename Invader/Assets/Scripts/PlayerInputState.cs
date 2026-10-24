namespace UnityInvader
{
    public readonly struct PlayerInputState
    {
        public bool Left  { get; }
        public bool Right { get; }
        public bool Fire  { get; }

        public PlayerInputState(bool left, bool right, bool fire = false)
        {
            Left  = left;
            Right = right;
            Fire  = fire;
        }
    }
}
