using UnityEngine;
using UnityEngine.Serialization;

namespace UnityInvader
{
    [DisallowMultipleComponent]
    public sealed class GameplayDriver : MonoBehaviour
    {
        [FormerlySerializedAs("currentState")]
        [SerializeField]
        private GameState        _currentState = GameState.Attract;
        [SerializeField]
        private Player           _player;
        [SerializeField]
        private InvaderFormation _formation;
        [SerializeField]
        private int              _score;
        [SerializeField]
        private int              _waveNumber   = 1;
        [Tooltip("Includes the current Player. Extra lives are not implemented yet.")]
        [SerializeField]
        [Min(0)]
        private int              _lives        = 3;

        private float _waveClearElapsed;
        private float _playerDeadElapsed;
        private bool  _initialized;

        // Provisional death wait; exact animation/video timing is a later task.
        private const float PlayerDeadDelay = 0.5f;

        [Tooltip("Temporary development start path; real game-start transitions are not implemented.")]
        [FormerlySerializedAs("developmentStartPlaying")]
        [SerializeField]
        private bool _developmentStartPlaying;

        private void OnEnable()
        {
            if (_initialized)
            {
                ApplyPlayingState();
            }
        }

        private void Start()
        {
            InitializeGameplay();
        }

        private void Update()
        {
            UpdateWaveClear();
            UpdatePlayerDead();
        }

        private void OnDisable()
        {
            if (_initialized)
            {
                _player.SetPlaying(false);
                _formation.SetPlaying(false);
            }
        }

        internal void NotifyInvaderDefeated(int inScore, bool inWaveCleared)
        {
            if (isActiveAndEnabled == false || _currentState != GameState.Playing)
            {
                return;
            }
            _score += inScore;
            if (inWaveCleared)
            {
                _waveClearElapsed = 0f;
                ChangeState(GameState.WaveClear);
            }
        }

        internal void NotifyPlayerHit()
        {
            if (_initialized == false || isActiveAndEnabled == false || _currentState != GameState.Playing || _lives <= 0)
            {
                return;
            }
            _lives--;
            _playerDeadElapsed = 0f;
            ChangeState(GameState.PlayerDead);
        }

        private void InitializeGameplay()
        {
            if (_player == null || _formation == null)
            {
                Debug.LogError("Arcade requires migration: Unity Invader > Migrate Arcade to Components.", this);
                enabled = false;
                return;
            }
            _player.Initialize();
            _formation.Initialize(this);
            _initialized = true;
            ChangeState(_developmentStartPlaying ? GameState.Playing : _currentState);
        }

        private void UpdateWaveClear()
        {
            if (_initialized == false || _currentState != GameState.WaveClear)
            {
                return;
            }
            _waveClearElapsed += Time.deltaTime;
            if (_waveClearElapsed + 1e-7f >= 1f)
            {
                _formation.BeginWave();
                _waveNumber++;
                ChangeState(GameState.Playing);
            }
        }

        private void UpdatePlayerDead()
        {
            if (_initialized == false || _currentState != GameState.PlayerDead)
            {
                return;
            }
            _playerDeadElapsed += Time.deltaTime;
            if (_playerDeadElapsed + 1e-7f < PlayerDeadDelay)
            {
                return;
            }
            if (_lives == 0)
            {
                ChangeState(GameState.GameOver);
                return;
            }
            _formation.PreparePlayerRespawn();
            _player.Respawn();
            ChangeState(GameState.Playing);
        }

        private void ChangeState(GameState inNextState)
        {
            _currentState = inNextState;
            ApplyPlayingState();
        }

        private void ApplyPlayingState()
        {
            bool playing = isActiveAndEnabled && _currentState == GameState.Playing;
            _player.SetPlaying(playing);
            _formation.SetPlaying(playing);
        }

#if UNITY_EDITOR
        public void EditorSetState(GameState inNextState)
        {
            if (_initialized)
            {
                ChangeState(inNextState);
            }
        }
#endif
    }
}
