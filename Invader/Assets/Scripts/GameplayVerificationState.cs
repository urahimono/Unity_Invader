#if UNITY_EDITOR
using System.Collections.Generic;

namespace UnityInvader
{
    // Editor-only snapshots own detached arrays of immutable value elements.
    public readonly struct FormationVerificationState
    {
        public int X              { get; }
        public int Y              { get; }
        public int Direction      { get; }
        public int StepCount      { get; }
        public int AliveCount     { get; }
        public int AnimationFrame { get; }

        public IReadOnlyList<InvaderDisplayState> Invaders { get; }

        public FormationVerificationState(int inX, int inY, int inDirection, int inStepCount,
            int inAliveCount, int inAnimationFrame, InvaderDisplayState[] inInvaders)
        {
            X              = inX;
            Y              = inY;
            Direction      = inDirection;
            StepCount      = inStepCount;
            AliveCount     = inAliveCount;
            AnimationFrame = inAnimationFrame;
            Invaders       = System.Array.AsReadOnly((InvaderDisplayState[])inInvaders.Clone());
        }
    }

    public readonly struct GameplayVerificationState
    {
        public BodyState Player { get; }

        public ShotState Shot { get; }

        public FormationVerificationState Formation { get; }

        public int Score      { get; }
        public int WaveNumber { get; }

        public bool WaveClearRequired { get; }

        public GameplayVerificationState(PlayerState inPlayer, FormationVerificationState inFormation,
            int inScore, int inWaveNumber, bool inWaveClearRequired)
        {
            Player            = inPlayer.Player;
            Shot              = inPlayer.Shot;
            Formation         = inFormation;
            Score             = inScore;
            WaveNumber        = inWaveNumber;
            WaveClearRequired = inWaveClearRequired;
        }
    }

    public readonly struct DriverVerificationState
    {
        public GameState CurrentState { get; }

        public GameplayVerificationState Simulation { get; }

        public DriverVerificationState(GameState inCurrentState, GameplayVerificationState inSimulation)
        {
            CurrentState = inCurrentState;
            Simulation   = inSimulation;
        }
    }
}
#endif
