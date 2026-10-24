namespace UnityInvader
{
    // One logical shot slot. No Unity objects or collision rules.
    public sealed class PlayerShot
    {
        private bool _pendingFire;

        public bool Active    { get; private set; }
        public bool FireReady { get; private set; } = true;

        public int X { get; private set; }
        public int Y { get; private set; }

        public const int SpeedPerTick = 4;
        public const int Width        = 1;
        public const int Height       = 4;

        public void Deactivate()
        {
            Active = false; // A hit must not re-arm Fire.
        }

        public void ReadFire(bool inPressed)
        {
            if (inPressed == false)
            {
                FireReady = true;
                return;
            }
            if (FireReady == false)
            {
                return;
            }
            FireReady = false; // Consume presses even while the slot is occupied.
            if (Active == false)
            {
                _pendingFire = true;
            }
        }

        public void Tick(PlayerBody inPlayer)
        {
            if (Active)
            {
                Y += SpeedPerTick;
                if (Y >= PlayerBody.ScreenHeight)
                {
                    Active = false;
                }
            }
            if (_pendingFire)
            {
                if (Active == false)
                {
                    Active = true;
                    X      = inPlayer.X;
                    Y      = inPlayer.Y + 8;
                }
                _pendingFire = false;
            }
        }
    }
}
