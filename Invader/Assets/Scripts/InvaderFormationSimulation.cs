namespace UnityInvader
{
    public sealed class InvaderFormationSimulation
    {
        public  const double TickDuration = 1.0 / 60.0;
        private       double _accumulatedTime;
        public InvaderFormation Formation { get; } = new InvaderFormation();

        public void Advance(double elapsedSeconds, bool playing)
        {
            if (playing == false)
            {
                _accumulatedTime = 0;
                return;
            }
            _accumulatedTime += elapsedSeconds;
            while (_accumulatedTime + 1e-12 >= TickDuration)
            {
                Formation.Tick();
                _accumulatedTime -= TickDuration;
            }
        }
    }
}
