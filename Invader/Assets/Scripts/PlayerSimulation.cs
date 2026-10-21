namespace UnityInvader
{
    public sealed class PlayerSimulation
    {
        public const double TickDuration = 1.0 / 60.0;
        private double accumulatedTime;

        public PlayerModel Player { get; } = new PlayerModel();
        public PlayerShot Shot { get; } = new PlayerShot();

        public void Advance(double elapsedSeconds, bool playing, PlayerInputState input)
        {
            if (!playing)
            {
                accumulatedTime = 0;
                return;
            }

            // Sample every render frame so release/press between logical ticks is retained.
            Shot.ReadFire(input.Fire);
            accumulatedTime += elapsedSeconds;
            while (accumulatedTime + 1e-12 >= TickDuration)
            {
                Tick(input);
                accumulatedTime -= TickDuration;
            }
        }

        // PlayingSimulation owns the live clock; Advance remains available for isolated verification.
        internal void Tick(PlayerInputState input)
        {
            Player.Tick(input);
            Shot.Tick(Player);
        }
    }
}
