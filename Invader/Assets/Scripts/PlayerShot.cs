namespace UnityInvader
{
    // One logical shot slot. No Unity objects or collision rules.
    public sealed class PlayerShot
    {
        public const int SpeedPerTick = 4;
        public bool Active { get; private set; }
        public int X { get; private set; }
        public int Y { get; private set; }
        public bool FireReady { get; private set; } = true;
        private bool pendingFire;

        public void ReadFire(bool pressed)
        {
            if (!pressed)
            {
                FireReady = true;
                return;
            }
            if (!FireReady) return;
            FireReady = false; // Consume presses even while the slot is occupied.
            if (!Active) pendingFire = true;
        }

        public void Tick(PlayerModel player)
        {
            if (Active)
            {
                Y += SpeedPerTick;
                if (Y >= PlayerModel.ScreenHeight) Active = false;
            }
            if (pendingFire)
            {
                if (!Active)
                {
                    Active = true;
                    X = player.X;
                    Y = player.Y + 8;
                }
                pendingFire = false;
            }
        }
    }
}
