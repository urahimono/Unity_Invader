namespace UnityInvader
{
    public sealed class InvaderFormationSimulation
    {
        public const double TickDuration = 1.0 / 60.0;
        private double accumulatedTime;
        public InvaderFormation Formation { get; } = new InvaderFormation();

        public void Advance(double elapsedSeconds, bool playing)
        {
            if (!playing)
            {
                accumulatedTime = 0;
                return;
            }
            accumulatedTime += elapsedSeconds;
            while (accumulatedTime + 1e-12 >= TickDuration)
            {
                Formation.Tick();
                accumulatedTime -= TickDuration;
            }
        }
    }
}
