namespace UnityInvader
{
    public sealed class InvaderFormationSimulation
    {
        public  const    double           TickDuration = 1.0 / 60.0;
        private          double           _accumulatedTime;
        private readonly InvaderFormation _formation   = new InvaderFormation();

#if UNITY_EDITOR
        public FormationVerificationState EditorCapture()
        {
            return _formation.EditorCapture();
        }
#endif

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
    }
}
