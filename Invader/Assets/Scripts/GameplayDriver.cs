using System;
using System.Threading;
using Cysharp.Threading.Tasks;
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
        [Tooltip("Includes the current Player. One extra life is awarded at 1500 points per game.")]
        [SerializeField]
        [Min(0)]
        private int              _lives        = 3;

        private PlayerInputReader _input;
        private bool              _initialized;
        private bool              _initializationAttempted;
        private bool              _extraLifeAwarded;

        private CancellationTokenSource _waveClearCancellation;
        private double                  _waveClearRemaining;
        private double                  _waveClearStartedAt;

        private CancellationTokenSource _playerDeadCancellation;
        private double                  _playerDeadRemaining;
        private double                  _playerDeadStartedAt;

        // Provisional death wait; exact animation/video timing is a later task.
        private const float PlayerDeadDelay = 0.5f;

        private const double WaveClearDelay = 1.0;

        private const int ExtraLifeScore = 1500;

        // ROM 1D54..1D62; the 16th stored value is skipped by the wrap at 044C.
        private static readonly int[] _ufoScores = { 100, 50, 50, 100, 150, 100, 100, 50, 300, 100, 100, 100, 50, 150, 100 };

        [Tooltip("Legacy saved setting. Startup always waits in Attract; use Enter to start.")]
        [HideInInspector]
        [FormerlySerializedAs("developmentStartPlaying")]
        [SerializeField]
        private bool _developmentStartPlaying;

        internal event System.Action GameStarting;
        internal event Action<int> UfoScoreAwarded;

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
                StartWaveClearWait();
                StartPlayerDeadWait();
            }
        }

        private void OnDisable()
        {
            SuspendWaveClearWait();
            SuspendPlayerDeadWait();
            _input?.Dispose();
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
            CancelWaveClearWait();
            CancelPlayerDeadWait();
            if (_initialized)
            {
                _player.HitAccepted -= NotifyPlayerHit;
                _player.ShotResolved -= NotifyShotResolved;
                _formation.Invaded -= NotifyInvaded;
            }
        }

        private void Start()
        {
            InitializeGameplay();
        }

        private void Update()
        {
            UpdateGameplay();
        }

        internal void NotifyPlayerHit()
        {
            if (_initialized == false || isActiveAndEnabled == false || _currentState != GameState.Playing || _lives <= 0)
            {
                return;
            }
            _lives--;
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
            int previousScore = _score;
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
            if (_extraLifeAwarded == false && previousScore < ExtraLifeScore && _score >= ExtraLifeScore)
            {
                _extraLifeAwarded = true;
                _lives++;
            }
            if (inTargetType == HitTargetType.Ufo)
            {
                UfoScoreAwarded?.Invoke(_score - previousScore);
            }
            TryEnterWaveClear();
        }

        private void UpdateGameplay()
        {
            if (_initialized == false)
            {
                return;
            }
            bool startPressed = _input.ReadStartPressed();
            if (startPressed && (_currentState == GameState.Attract || _currentState == GameState.GameOver))
            {
                BeginNewGame();
                return;
            }
            _player.SetInput(_input.Read());
            TryEnterWaveClear();
        }

        private void TryEnterWaveClear()
        {
            if (_initialized == false || isActiveAndEnabled == false || _currentState != GameState.Playing || _formation.IsCleared == false)
            {
                return;
            }
            ChangeState(GameState.WaveClear);
        }

        private void InitializeGameplay()
        {
            if (_initializationAttempted)
            {
                return;
            }
            _initializationAttempted = true;
            if (_player == null || _formation == null || _shields == null || _shields.Length != 4 ||
                System.Array.Exists(_shields, inShield => inShield == null))
            {
                Debug.LogError("Arcade requires Player, Formation and four Shield references.", this);
                StopInitialization();
                return;
            }
            if (_player.IsInitialized == false ||
                _formation.Initialize(_player, _player.GetNormalShotEndCount, () => Score) == false)
            {
                StopInitialization();
                return;
            }
            _formation.Invaded += NotifyInvaded;
            _player.HitAccepted += NotifyPlayerHit;
            _player.ShotResolved += NotifyShotResolved;
            _initialized = true;
            if (_developmentStartPlaying)
            {
                _developmentStartPlaying = false;
            }
            ChangeState(GameState.Attract);
        }

        private void BeginNewGame()
        {
            CancelWaveClearWait();
            CancelPlayerDeadWait();
            // Reuse initialized owners; do not recreate objects or subscribe a second time.
            _player.SetPlaying(false);
            _formation.SetPlaying(false);
            SetShieldsPlaying(false);
            GameStarting?.Invoke();
            _score              = 0;
            _waveNumber         = 1;
            _lives              = 3;
            _extraLifeAwarded   = false;
            _waveClearRemaining = 0.0;
            _playerDeadRemaining = 0.0;
            _player.Respawn();
            _player.ResetShotHistory();
            _formation.BeginWave(_waveNumber);
            foreach (var shield in _shields)
            {
                shield.BeginWave();
            }
            ChangeState(GameState.Playing);
        }

        private void StopInitialization()
        {
            if (_player != null)
            {
                _player.SetPlaying(false);
            }
            if (_formation != null)
            {
                _formation.SetPlaying(false);
            }
            SetShieldsPlaying(false);
            enabled = false;
        }

        private void StartWaveClearWait()
        {
            if (_initialized == false || isActiveAndEnabled == false ||
                _currentState != GameState.WaveClear || _waveClearCancellation != null)
            {
                return;
            }
            _waveClearCancellation = new CancellationTokenSource();
            _waveClearStartedAt = Time.timeAsDouble;
            WaitForNextWaveAsync(_waveClearCancellation).Forget(HandleStateWaitException);
        }

        private async UniTask WaitForNextWaveAsync(CancellationTokenSource inCancellation)
        {
            CancellationToken token = inCancellation.Token;
            try
            {
                // Scaled game time; resume after MonoBehaviour.Update, never inline on entry.
                await UniTask.Delay(TimeSpan.FromSeconds(_waveClearRemaining),
                    ignoreTimeScale: false, delayTiming: PlayerLoopTiming.LastUpdate,
                    cancellationToken: token, cancelImmediately: true);
                if (token.IsCancellationRequested || _waveClearCancellation != inCancellation ||
                    _initialized == false || isActiveAndEnabled == false || _currentState != GameState.WaveClear)
                {
                    return;
                }
                _waveClearCancellation = null;
                _waveClearRemaining = 0.0;
                _waveNumber++;
                _formation.BeginWave(_waveNumber);
                _player.ResetShotHistory(); // ROM 09FD copies the mirror, including 208D/208F.
                foreach (var shield in _shields)
                {
                    shield.BeginWave();
                }
                ChangeState(GameState.Playing);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                // State exit, Disable and destruction are expected cancellation paths.
            }
            finally
            {
                if (_waveClearCancellation == inCancellation)
                {
                    _waveClearCancellation = null;
                }
                inCancellation.Dispose();
            }
        }

        private void SuspendWaveClearWait()
        {
            if (_waveClearCancellation != null)
            {
                _waveClearRemaining = Math.Max(0.0,
                    _waveClearRemaining - Math.Max(0.0, Time.timeAsDouble - _waveClearStartedAt));
            }
            CancelWaveClearWait();
        }

        private void CancelWaveClearWait()
        {
            CancellationTokenSource cancellation = _waveClearCancellation;
            // Detach before Cancel: immediate cancellation runs the async finally inline.
            _waveClearCancellation = null;
            cancellation?.Cancel();
            // The async owner disposes its source in finally, including on cancellation.
        }

        private void HandleStateWaitException(Exception inException)
        {
            Debug.LogException(inException, this);
            enabled = false;
        }

        private void StartPlayerDeadWait()
        {
            if (_initialized == false || isActiveAndEnabled == false ||
                _currentState != GameState.PlayerDead || _playerDeadCancellation != null)
            {
                return;
            }
            _playerDeadCancellation = new CancellationTokenSource();
            _playerDeadStartedAt = Time.timeAsDouble;
            WaitForPlayerDeadAsync(_playerDeadCancellation).Forget(HandleStateWaitException);
        }

        private async UniTask WaitForPlayerDeadAsync(CancellationTokenSource inCancellation)
        {
            CancellationToken token = inCancellation.Token;
            try
            {
                // Match the WaveClear clock and continuation phase; keep the outcomes separate.
                await UniTask.Delay(TimeSpan.FromSeconds(_playerDeadRemaining),
                    ignoreTimeScale: false, delayTiming: PlayerLoopTiming.LastUpdate,
                    cancellationToken: token, cancelImmediately: true);
                if (token.IsCancellationRequested || _playerDeadCancellation != inCancellation ||
                    _initialized == false || isActiveAndEnabled == false || _currentState != GameState.PlayerDead)
                {
                    return;
                }
                _playerDeadCancellation = null;
                _playerDeadRemaining = 0.0;
                if (_lives == 0)
                {
                    ChangeState(GameState.GameOver);
                    return;
                }
                _formation.PreparePlayerRespawn();
                _player.Respawn();
                ChangeState(GameState.Playing);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                // State exit, Disable and destruction are expected cancellation paths.
            }
            finally
            {
                if (_playerDeadCancellation == inCancellation)
                {
                    _playerDeadCancellation = null;
                }
                inCancellation.Dispose();
            }
        }

        private void SuspendPlayerDeadWait()
        {
            if (_playerDeadCancellation != null)
            {
                _playerDeadRemaining = Math.Max(0.0,
                    _playerDeadRemaining - Math.Max(0.0, Time.timeAsDouble - _playerDeadStartedAt));
            }
            CancelPlayerDeadWait();
        }

        private void CancelPlayerDeadWait()
        {
            CancellationTokenSource cancellation = _playerDeadCancellation;
            // Cancel may synchronously execute the async finally; detach first.
            _playerDeadCancellation = null;
            cancellation?.Cancel();
            // WaitForPlayerDeadAsync owns Dispose, including the cancellation path.
        }

        private void ChangeState(GameState inNextState)
        {
            if (_currentState != inNextState)
            {
                CancelWaveClearWait();
                CancelPlayerDeadWait();
                _waveClearRemaining = inNextState == GameState.WaveClear ? WaveClearDelay : 0.0;
                _playerDeadRemaining = inNextState == GameState.PlayerDead ? PlayerDeadDelay : 0.0;
            }
            _currentState = inNextState;
            ApplyPlayingState();
            StartWaveClearWait();
            StartPlayerDeadWait();
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
            if (_shields == null)
            {
                return;
            }
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
