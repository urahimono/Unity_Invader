namespace UnityInvader
{
    public sealed class InvaderFormationSimulation
    {
        private double _accumulatedTime;

        public  const    double           TickDuration = 1.0 / 60.0;
        private readonly InvaderFormation _formation   = new InvaderFormation();

        public void Advance(double inElapsedSeconds, bool inPlaying)
        {
            if (inPlaying == false)
            {
                _accumulatedTime = 0;
                return;
            }
            _accumulatedTime += inElapsedSeconds;
            while (_accumulatedTime + 1e-12 >= TickDuration)
            {
                _formation.Tick();
                _accumulatedTime -= TickDuration;
            }
        }

#if UNITY_EDITOR
        public FormationVerificationState EditorCapture()
        {
            return _formation.EditorCapture();
        }
#endif
    }
}
