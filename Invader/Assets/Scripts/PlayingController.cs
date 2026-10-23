using UnityEngine;
using UnityEngine.Serialization;

namespace UnityInvader
{
    // Unity-side input and clock for this game's shared Playing simulation.
    [DisallowMultipleComponent]
    public sealed class PlayingController : MonoBehaviour
    {
        [FormerlySerializedAs("gameState")]
        [SerializeField] private GameStateController _gameState;
        [Tooltip("Temporary development start path; real game-start transitions are not implemented.")]
        [FormerlySerializedAs("developmentStartPlaying")]
        [SerializeField] private bool _developmentStartPlaying;
        private PlayerInputReader _input;
        private double _waveClearElapsed;
        private bool _waitingForNextWave;

        private void OnEnable()
        {
            _input = new PlayerInputReader();
        }

        private void Start()
        {
            if (_developmentStartPlaying)
            {
                _gameState.ChangeState(GameState.Playing);
            }
        }

        private void Update()
        {
            if (_waitingForNextWave && _gameState.CurrentState == GameState.WaveClear)
            {
                _waveClearElapsed += Time.deltaTime;
                if (_waveClearElapsed + 1e-7 >= 1.0)
                {
                    _gameState.Playing.StartNextWave();
                    _waitingForNextWave = false;
                    _gameState.ChangeState(GameState.Playing);
                }
                return; // New Formation remains at its initial state for this frame.
            }
            _waitingForNextWave = false;
            bool playing = _gameState.CurrentState == GameState.Playing;
            // The only runtime driver; both views synchronize afterward in LateUpdate.
            _gameState.Playing.Advance(Time.deltaTime, playing, playing ? _input.Read() : default);
            if (playing && _gameState.Playing.WaveClearRequired)
            {
                _waveClearElapsed = 0;
                _waitingForNextWave = true;
                _gameState.ChangeState(GameState.WaveClear);
            }
        }

        private void OnDisable()
        {
            _gameState.Playing.Advance(0, false, default);
            _input?.Dispose();
            _input = null;
        }
    }
}
