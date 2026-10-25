using System;
using UnityEngine;

namespace UnityInvader
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class Player : MonoBehaviour
    {
        [SerializeField]
        private PlayerShot     _shot;
        [SerializeField]
        private GameplayDriver _driver;

        private PlayerInputReader _input;
        private PlayerInputState  _inputState;
        private SpriteRenderer    _renderer;
        private BoxCollider2D     _hitCollider;
        private bool              _playing;
        private bool              _pendingFire;
        private bool              _fireReady = true;
        private bool              _hitAccepted;

        private const float Speed    = 60f * ArcadeViewport.UnitsPerPixel;
        private const float MaximumX = (ArcadeViewport.ScreenWidth / 2f - 8f) * ArcadeViewport.UnitsPerPixel;

        private void Awake()
        {
            _renderer            = GetComponent<SpriteRenderer>();
            _hitCollider         = GetComponent<BoxCollider2D>();
            _renderer.enabled    = false;
            if (_hitCollider != null)
            {
                _hitCollider.enabled = false;
            }
        }

        private void OnEnable()
        {
            _input = new PlayerInputReader();
            ApplyPlayingState();
        }

        private void Update()
        {
            ReadInput();
        }

        private void FixedUpdate()
        {
            UpdatePlayer();
        }

        private void OnDisable()
        {
            _input?.Dispose();
            _input       = null;
            _inputState  = default;
            _pendingFire = false;
            ApplyPlayingState();
        }

        internal void Initialize()
        {
            if (_shot == null || _driver == null || _hitCollider == null)
            {
                throw new InvalidOperationException("Player requires a hit collider, PlayerShot and GameplayDriver references. Configure Player Hit first.");
            }
        }

        internal void SetPlaying(bool inPlaying)
        {
            _playing = inPlaying && _hitAccepted == false;
            if (_playing == false)
            {
                _inputState  = default;
                _pendingFire = false;
            }
            ApplyPlayingState();
        }

        internal bool TryReceiveHit()
        {
            if (_playing == false || isActiveAndEnabled == false || _hitAccepted || _hitCollider.enabled == false)
            {
                return false;
            }
            _hitAccepted = true;
            SetPlaying(false);
            _driver.NotifyPlayerHit();
            return true;
        }

        private void ApplyPlayingState()
        {
            bool playing = _playing && isActiveAndEnabled;
            if (_renderer != null)
            {
                _renderer.enabled = playing;
            }
            if (_hitCollider != null)
            {
                _hitCollider.enabled = playing;
            }
            if (_shot != null)
            {
                _shot.SetPlaying(playing);
            }
        }

        private void ReadInput()
        {
            if (_playing == false)
            {
                return;
            }
            _inputState = _input.Read();
            if (_inputState.Fire == false)
            {
                _fireReady = true;
                return;
            }
            if (_fireReady == false)
            {
                return;
            }
            _fireReady = false;
            // Presses while the single shot is occupied are consumed, not queued for later.
            if (_shot.InFlight == false)
            {
                _pendingFire = true;
            }
        }

        private void UpdatePlayer()
        {
            if (_playing == false)
            {
                return;
            }
            int direction      = _inputState.Right ? 1 : _inputState.Left ? -1 : 0;
            var position       = transform.position;
            position.x         = Mathf.Clamp(position.x + direction * Speed * Time.fixedDeltaTime, -MaximumX, MaximumX);
            transform.position = position;
            if (_pendingFire)
            {
                _pendingFire = false;
                _shot.TryLaunch((Vector2)position + Vector2.up * (8f * ArcadeViewport.UnitsPerPixel));
            }
        }
    }
}
