using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace UnityInvader
{
    // Audio presentation only. No scoring, movement or hit decisions.
    [DisallowMultipleComponent]
    public sealed class GameplayAudio : MonoBehaviour
    {
        [SerializeField]
        private GameplayDriver _driver;
        [SerializeField]
        private Player         _player;
        [SerializeField]
        private Ufo            _ufo;
        [SerializeField]
        private AudioSource    _movement;
        [SerializeField]
        private AudioSource    _shot;
        [SerializeField]
        private AudioSource    _invaderDefeat;
        [SerializeField]
        private AudioSource    _playerDeath;
        [SerializeField]
        private AudioSource    _ufoFlight;
        [SerializeField]
        private AudioSource    _ufoDefeat;
        [SerializeField]
        private AudioClip[]    _movementPattern;
        [Tooltip("Provisional independent cadence, not a measured survivor-count rule.")]
        [SerializeField, Min(0.01f)]
        private float          _movementInterval = 0.35f;

        private AudioSource[]           _sources;
        private CancellationTokenSource _movementCancellation;
        private int                     _movementIndex;
        private bool                    _paused;
        private bool                    _stopped = true;

        private bool CanPlayCue => _driver != null && _driver.isActiveAndEnabled && Time.timeScale > 0f &&
            _driver.CurrentState != GameState.Attract && _driver.CurrentState != GameState.GameOver;

        private bool CanPlayContinuous => CanPlayCue && _driver.CurrentState == GameState.Playing;

        private readonly List<Invader> _invaders = new List<Invader>();

        private void OnEnable()
        {
            _sources = new[] { _movement, _shot, _invaderDefeat, _playerDeath, _ufoFlight, _ufoDefeat };
            if (_driver == null || _player == null || _ufo == null ||
                Array.Exists(_sources, inSource => inSource == null || inSource.clip == null) ||
                _movementPattern == null || _movementPattern.Length == 0 ||
                Array.Exists(_movementPattern, inClip => inClip == null) ||
                float.IsFinite(_movementInterval) == false || _movementInterval <= 0f)
            {
                Debug.LogError("Gameplay audio requires owners, six configured sources, a movement pattern and a positive interval.", this);
                enabled = false;
                return;
            }
            ResetAudio();
            _player.ShotLaunched += PlayShot;
            _player.HitAccepted += PlayPlayerDeath;
            _ufo.Defeated += PlayUfoDefeat;
            _driver.GameStarting += ResetAudio;
            foreach (var invader in _invaders)
            {
                if (invader != null)
                {
                    invader.Defeated += PlayInvaderDefeat;
                }
            }
        }

        private void OnDisable()
        {
            if (_player != null)
            {
                _player.ShotLaunched -= PlayShot;
                _player.HitAccepted -= PlayPlayerDeath;
            }
            if (_ufo != null)
            {
                _ufo.Defeated -= PlayUfoDefeat;
            }
            if (_driver != null)
            {
                _driver.GameStarting -= ResetAudio;
            }
            foreach (var invader in _invaders)
            {
                if (invader != null)
                {
                    invader.Defeated -= PlayInvaderDefeat;
                }
            }
            ResetAudio();
        }

        private void Update()
        {
            RefreshPlayback();
        }

        internal void Track(Invader inInvader)
        {
            _invaders.Add(inInvader);
            if (isActiveAndEnabled)
            {
                inInvader.Defeated += PlayInvaderDefeat;
            }
        }

        private void RefreshPlayback()
        {
            if (_driver.CurrentState == GameState.GameOver || _driver.CurrentState == GameState.Attract)
            {
                if (_stopped == false)
                {
                    ResetAudio();
                }
                return;
            }
            SetPaused(_driver.isActiveAndEnabled == false || Time.timeScale <= 0f);
            if (CanPlayContinuous == false)
            {
                StopContinuous();
                return;
            }
            if (_movementCancellation == null)
            {
                _stopped              = false;
                _movementCancellation = new CancellationTokenSource();
                PlayMovement(_movementCancellation.Token).Forget(HandleAudioException);
            }
            if (_ufo.InFlight && _ufo.isActiveAndEnabled)
            {
                if (_ufoFlight.isPlaying == false)
                {
                    _ufoFlight.Play();
                }
            }
            else
            {
                _ufoFlight.Stop();
            }
        }

        private async UniTask PlayMovement(CancellationToken inToken)
        {
            while (CanPlayContinuous && inToken.IsCancellationRequested == false)
            {
                _movement.PlayOneShot(_movementPattern[_movementIndex]);
                _movementIndex = (_movementIndex + 1) % _movementPattern.Length;
                // Independent scaled game time, Update. No Formation movement-step dependency.
                bool cancelled = await UniTask.Delay(TimeSpan.FromSeconds(_movementInterval), DelayType.DeltaTime,
                    PlayerLoopTiming.Update, inToken).SuppressCancellationThrow();
                if (cancelled)
                {
                    return;
                }
            }
        }

        private void PlayShot()
        {
            PlayCue(_shot);
        }

        private void PlayPlayerDeath()
        {
            StopContinuous();
            PlayCue(_playerDeath);
        }

        private void PlayInvaderDefeat(Invader inInvader, Component inSource)
        {
            PlayCue(_invaderDefeat);
        }

        private void PlayUfoDefeat(Vector3 inPosition)
        {
            _ufoFlight.Stop();
            PlayCue(_ufoDefeat);
        }

        private void PlayCue(AudioSource inSource)
        {
            if (CanPlayCue)
            {
                // PlayOneShot preserves overlapping instances instead of replacing a short sound.
                _stopped = false;
                inSource.PlayOneShot(inSource.clip);
            }
        }

        private void SetPaused(bool inPaused)
        {
            if (_paused == inPaused)
            {
                return;
            }
            _paused = inPaused;
            foreach (var source in _sources)
            {
                if (inPaused)
                {
                    source.Pause();
                }
                else
                {
                    source.UnPause();
                }
            }
        }

        private void StopContinuous()
        {
            _movementCancellation?.Cancel();
            _movementCancellation?.Dispose();
            _movementCancellation = null;
            if (_movement != null)
            {
                _movement.Stop();
            }
            if (_ufoFlight != null)
            {
                _ufoFlight.Stop();
            }
        }

        private void ResetAudio()
        {
            StopContinuous();
            _movementIndex = 0;
            _paused        = false;
            _stopped       = true;
            if (_sources == null)
            {
                return;
            }
            foreach (var source in _sources)
            {
                if (source != null)
                {
                    source.Stop();
                }
            }
        }

        private void HandleAudioException(Exception inException)
        {
            Debug.LogException(inException, this);
            enabled = false;
        }
    }
}
