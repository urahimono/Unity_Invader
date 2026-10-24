namespace UnityInvader
{
    public sealed class PlayerUnit
    {
        public  const double TickDuration = 1.0 / 60.0;
        private       double _accumulatedTime;

        public PlayerBody Player { get; } = new PlayerBody();

        public PlayerShot Shot { get; } = new PlayerShot();

        public void Advance(double inElapsedSeconds, bool inPlaying, PlayerInputState inInput)
        {
            if (inPlaying == false)
            {
                _accumulatedTime = 0;
                return;
            }

            // Sample every render frame so release/press between logical ticks is retained.
            Shot.ReadFire(inInput.Fire);
            _accumulatedTime += inElapsedSeconds;
            while (_accumulatedTime + 1e-12 >= TickDuration)
            {
                Tick(inInput);
                _accumulatedTime -= TickDuration;
            }
        }

        // GameplaySimulation owns the live clock; Advance remains available for isolated verification.
        internal void Tick(PlayerInputState inInput)
        {
            Player.Tick(inInput);
            Shot.Tick(Player);
        }
    }
}
