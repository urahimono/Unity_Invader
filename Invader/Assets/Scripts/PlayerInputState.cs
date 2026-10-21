namespace UnityInvader
{
    public readonly struct PlayerInputState
    {
        public bool Left { get; }
        public bool Right { get; }

        public PlayerInputState(bool left, bool right)
        {
            Left = left;
            Right = right;
        }
    }
}
