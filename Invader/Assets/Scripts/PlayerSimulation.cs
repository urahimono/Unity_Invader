namespace UnityInvader
{
    public sealed class PlayerSimulation
    {
        public const double TickDuration = 1.0 / 60.0;
        private double accumulatedTime;

        public PlayerModel Player { get; } = new PlayerModel();

        public void Advance(double elapsedSeconds, bool playing, PlayerInputState input)
        {
            if (!playing)
            {
                accumulatedTime = 0;
                return;
            }

            accumulatedTime += elapsedSeconds;
            while (accumulatedTime + 1e-12 >= TickDuration)
            {
                Player.Tick(input);
                accumulatedTime -= TickDuration;
            }
        }
    }
}
