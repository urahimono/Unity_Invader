using System;
using UnityEngine;

namespace UnityInvader
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D))]
    public sealed class Player : MonoBehaviour, IHitReceiver, IPositionProvider
    {
        [SerializeField]
        private PlayerShot     _shot;
        [SerializeField]
        private SpriteRenderer _renderer;
        [SerializeField]
        private BoxCollider2D  _hitCollider;

        private PlayerInputState _inputState;
        private bool             _playing;
        private bool             _pendingFire;
        private bool             _fireReady = true;
        private bool             _hitAccepted;

        public event Action HitAccepted;

        public Vector2 Position => transform.position;

        private const float Speed    = 60f * ArcadeViewport.UnitsPerPixel;
        private const float MaximumX = (ArcadeViewport.ScreenWidth / 2f - 8f) * ArcadeViewport.UnitsPerPixel;

        private void Awake()
        {
            if (_shot == null || _renderer == null || _hitCollider == null)
            {
                throw new InvalidOperationException("Player requires saved PlayerShot, renderer and hit collider references. Configure Player Hit first.");
            }
        }

        private void OnEnable()
        {
            ApplyPlayingState();
        }

        private void FixedUpdate()
        {
            UpdatePlayer();
        }

        private void OnDisable()
        {
            _inputState  = default;
            _pendingFire = false;
            if (_playing)
            {
                ApplyPlayingState();
            }
        }

        public bool TryReceiveHit()
        {
            if (_playing == false || isActiveAndEnabled == false || _hitAccepted || _hitCollider.enabled == false)
            {
                return false;
            }
            _hitAccepted = true;
            SetPlaying(false);
            HitAccepted?.Invoke();
            return true;
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

        internal void Respawn()
        {
            SetPlaying(false);
            _shot.ResetFlight();
            transform.position = new Vector3((112f - ArcadeViewport.ScreenWidth / 2f) * ArcadeViewport.UnitsPerPixel,
                (24f - ArcadeViewport.ScreenHeight / 2f) * ArcadeViewport.UnitsPerPixel, transform.position.z);
            _hitAccepted       = false;
            // A release during Playing is required before the next press may launch a shot.
            _fireReady = false;
        }

        internal void SetInput(PlayerInputState inInput)
        {
            if (_playing == false || isActiveAndEnabled == false)
            {
                return;
            }
            _inputState = inInput;
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

        private void ApplyPlayingState()
        {
            bool playing         = _playing && isActiveAndEnabled;
            _renderer.enabled    = playing;
            _hitCollider.enabled = playing;
            _shot.SetPlaying(playing);
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
