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
        private Shield[]         _shields      = new Shield[4];
        [SerializeField]
        private int              _score;
        [SerializeField]
        private int              _waveNumber   = 1;
        [Tooltip("Includes the current Player. Extra lives are not implemented yet.")]
        [SerializeField]
        [Min(0)]
        private int              _lives        = 3;

        private PlayerInputReader _input;
        private float             _waveClearElapsed;
        private float             _playerDeadElapsed;
        private bool              _initialized;

        // Provisional death wait; exact animation/video timing is a later task.
        private const float PlayerDeadDelay = 0.5f;

        // ROM 1D54..1D62; the 16th stored value is skipped by the wrap at 044C.
        private static readonly int[] _ufoScores = { 100, 50, 50, 100, 150, 100, 100, 50, 300, 100, 100, 100, 50, 150, 100 };

        [Tooltip("Temporary development start path; real game-start transitions are not implemented.")]
        [FormerlySerializedAs("developmentStartPlaying")]
        [SerializeField]
        private bool _developmentStartPlaying;

        public GameState CurrentState => _currentState;

        public int Lives      => _lives;
        public int Score      => _score;
        public int WaveNumber => _waveNumber;

        private void OnEnable()
        {
            _input = new PlayerInputReader();
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
            if (_initialized)
            {
                _player.SetInput(_input.Read());
            }
            TryEnterWaveClear();
            UpdateWaveClear();
            UpdatePlayerDead();
        }

        private void OnDisable()
        {
            _input.Dispose();
            _input = null;
            if (_initialized)
            {
                _player.SetPlaying(false);
                _formation.SetPlaying(false);
                SetShieldsPlaying(false);
            }
        }

        private void OnDestroy()
        {
            if (_initialized)
            {
                _player.HitAccepted -= NotifyPlayerHit;
                _player.ShotResolved -= NotifyShotResolved;
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

        internal void NotifyInvaded()
        {
            if (_initialized == false || isActiveAndEnabled == false || _currentState != GameState.Playing)
            {
                return;
            }
            _lives = 0;
            ChangeState(GameState.GameOver);
        }

        internal void NotifyShotResolved(HitTargetType inTargetType)
        {
            if (_initialized == false || isActiveAndEnabled == false || _currentState != GameState.Playing)
            {
                return;
            }
            switch (inTargetType)
            {
                case HitTargetType.InvaderTop:
                    _score += 30;
                    break;
                case HitTargetType.InvaderMiddle:
                    _score += 20;
                    break;
                case HitTargetType.InvaderBottom:
                    _score += 10;
                    break;
                case HitTargetType.Ufo:
                    _score += _ufoScores[(int)(_player.NormalShotEndCount % _ufoScores.Length)];
                    break;
            }
            TryEnterWaveClear();
        }

        private void TryEnterWaveClear()
        {
            if (_initialized == false || isActiveAndEnabled == false || _currentState != GameState.Playing || _formation.IsCleared == false)
            {
                return;
            }
            _waveClearElapsed = 0f;
            ChangeState(GameState.WaveClear);
        }

        private void InitializeGameplay()
        {
            if (_player == null || _formation == null || _shields == null || _shields.Length != 4 ||
                System.Array.Exists(_shields, inShield => inShield == null))
            {
                Debug.LogError("Arcade requires Player, Formation and four Shield references.", this);
                enabled = false;
                return;
            }
            _formation.Initialize(this, _player, _player.GetNormalShotEndCount);
            _player.HitAccepted += NotifyPlayerHit;
            _player.ShotResolved += NotifyShotResolved;
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
                _player.ResetShotHistory(); // ROM 09FD copies the mirror, including 208D/208F.
                foreach (var shield in _shields)
                {
                    shield.BeginWave();
                }
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
            SetShieldsPlaying(playing);
        }

        private void SetShieldsPlaying(bool inPlaying)
        {
            foreach (var shield in _shields)
            {
                if (shield != null)
                {
                    shield.SetPlaying(inPlaying);
                }
            }
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
