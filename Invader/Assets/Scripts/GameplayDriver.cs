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
        private GameState         _currentState = GameState.Attract;
        [Tooltip("Temporary development start path; real game-start transitions are not implemented.")]
        [FormerlySerializedAs("developmentStartPlaying")]
        [SerializeField]
        private bool              _developmentStartPlaying;
        private PlayerInputReader _input;
        private double            _waveClearElapsed;
        private bool              _waitingForNextWave;

        [SerializeField]
        private          PlayerController           _playerController;
        [SerializeField]
        private          InvaderFormationController _formationController;
        private readonly GameplaySimulation         _simulation           = new GameplaySimulation();
        private readonly InvaderDisplayState[]      _invaderDisplayStates = new InvaderDisplayState[InvaderFormation.Rows * InvaderFormation.Columns];

        private void ChangeState(GameState inNextState)
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
            PublishDisplay();
        }

        private void Update()
        {
            if (_waitingForNextWave && _currentState == GameState.WaveClear)
            {
                _waveClearElapsed += Time.deltaTime;
                if (_waveClearElapsed + 1e-7 >= 1.0)
                {
                    _simulation.StartNextWave();
                    _waitingForNextWave = false;
                    ChangeState(GameState.Playing);
                }
                return; // New Formation remains at its initial state for this frame.
            }
            _waitingForNextWave = false;
            bool playing        = _currentState == GameState.Playing;
            // The only runtime driver; both views synchronize afterward in LateUpdate.
            _simulation.Advance(Time.deltaTime, playing, playing ? _input.Read() : default);
            if (playing && _simulation.WaveClearRequired)
            {
                _waveClearElapsed   = 0;
                _waitingForNextWave = true;
                ChangeState(GameState.WaveClear);
            }
        }

        private void LateUpdate()
        {
            PublishDisplay();
        }

        private void PublishDisplay()
        {
            _simulation.CopyDisplayState(_currentState == GameState.Playing, out var player, _invaderDisplayStates);
            if (_playerController != null)
            {
                _playerController.ApplyDisplay(player);
            }
            if (_formationController != null)
            {
                _formationController.ApplyDisplay(_invaderDisplayStates);
            }
        }

#if UNITY_EDITOR
        public DriverVerificationState EditorCapture()
        {
            return new DriverVerificationState(_currentState, _simulation.EditorCapture());
        }

        public void EditorSetState(GameState inNextState)
        {
            ChangeState(inNextState);
        }

        public void EditorPrepareShot(int inX, int inY)
        {
            _simulation.EditorPrepareShot(inX, inY);
        }

        public void EditorDeactivateShot()
        {
            _simulation.EditorDeactivateShot();
        }

        public void EditorAdvanceFixture(double inElapsed, bool inPlaying, PlayerInputState inInput)
        {
            _simulation.Advance(inElapsed, inPlaying, inInput);
        }
#endif
        private void OnDisable()
        {
            _simulation.Advance(0, false, default);
            _input?.Dispose();
            _input = null;
            PublishDisplay();
        }
    }
}
