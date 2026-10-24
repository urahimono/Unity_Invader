namespace UnityInvader
{
    public sealed class PlayerUnit
    {
        public  const double TickDuration = 1.0 / 60.0;
        private       double _accumulatedTime;

        public PlayerBody Player { get; } = new PlayerBody();

        public PlayerShot Shot { get; } = new PlayerShot();

        public void Advance(double elapsedSeconds, bool playing, PlayerInputState input)
        {
            if (playing == false)
            {
                _accumulatedTime = 0;
                return;
            }

            // Sample every render frame so release/press between logical ticks is retained.
            Shot.ReadFire(input.Fire);
            _accumulatedTime += elapsedSeconds;
            while (_accumulatedTime + 1e-12 >= TickDuration)
            {
                Tick(input);
                _accumulatedTime -= TickDuration;
            }
        }

        // ArcadeGameplaySimulation owns the live clock; Advance remains available for isolated verification.
        internal void Tick(PlayerInputState input)
        {
            Player.Tick(input);
            Shot.Tick(Player);
        }
    }
}
