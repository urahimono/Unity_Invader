using UnityEngine;
using UnityEngine.Serialization;

namespace UnityInvader
{
    // Unity-side state, input and clock for this game's shared simulation.
    [DisallowMultipleComponent]
    public sealed class GameplayDriver : MonoBehaviour
    {
        [FormerlySerializedAs("currentState")]
        [SerializeField]
        private GameState           _currentState = GameState.Attract;
        [Tooltip("Temporary development start path; real game-start transitions are not implemented.")]
        [FormerlySerializedAs("developmentStartPlaying")]
        [SerializeField]
        private bool                _developmentStartPlaying;
        private PlayerInputReader   _input;
        private double              _waveClearElapsed;
        private bool                _waitingForNextWave;

        public GameState CurrentState => _currentState;

        public GameplaySimulation Simulation { get; } = new GameplaySimulation();

        public void ChangeState(GameState inNextState)
        {
            _currentState = inNextState;
        }

        private void OnEnable()
        {
            _input = new PlayerInputReader();
        }

        private void Start()
        {
            if (_developmentStartPlaying)
            {
                ChangeState(GameState.Playing);
            }
        }

        private void Update()
        {
            if (_waitingForNextWave && CurrentState == GameState.WaveClear)
            {
                _waveClearElapsed += Time.deltaTime;
                if (_waveClearElapsed + 1e-7 >= 1.0)
                {
                    Simulation.StartNextWave();
                    _waitingForNextWave = false;
                    ChangeState(GameState.Playing);
                }
                return; // New Formation remains at its initial state for this frame.
            }
            _waitingForNextWave = false;
            bool playing        = CurrentState == GameState.Playing;
            // The only runtime driver; both views synchronize afterward in LateUpdate.
            Simulation.Advance(Time.deltaTime, playing, playing ? _input.Read() : default);
            if (playing && Simulation.WaveClearRequired)
            {
                _waveClearElapsed   = 0;
                _waitingForNextWave = true;
                ChangeState(GameState.WaveClear);
            }
        }

        private void OnDisable()
        {
            Simulation.Advance(0, false, default);
            _input?.Dispose();
            _input = null;
        }
    }
}
