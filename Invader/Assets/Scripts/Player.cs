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

        [SerializeField]
        private long _normalShotEndCount;

        private PlayerInputState _inputState;
        private bool             _playing;
        private bool             _pendingFire;
        private bool             _fireReady = true;
        private bool             _hitAccepted;
        private bool             _shotOccupied;

        public event Action HitAccepted;
        public event Action<HitTargetType> ShotResolved;

        public HitTargetType TargetType => HitTargetType.Player;

        public long NormalShotEndCount => _normalShotEndCount;

        public bool IsInitialized { get; private set; }

        public Vector2 Position => transform.position;

        private const float Speed    = 60f * ArcadeViewport.UnitsPerPixel;
        private const float MaximumX = (ArcadeViewport.ScreenWidth / 2f - 8f) * ArcadeViewport.UnitsPerPixel;

        private void Awake()
        {
            if (_shot == null || _renderer == null || _hitCollider == null)
            {
                Debug.LogError("Player requires saved PlayerShot, renderer and hit collider references. Configure Player Hit first.", this);
                // Missing saved references must not leave the attached collider active.
                GetComponent<SpriteRenderer>().enabled = false;
                GetComponent<BoxCollider2D>().enabled  = false;
                enabled = false;
                ApplyPlayingState();
                return;
            }
            _shot.FlightEnded += ResolveShot;
            IsInitialized = true;
        }

        private void OnEnable()
        {
            ApplyPlayingState();
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

        private void OnDestroy()
        {
            if (IsInitialized && _shot != null)
            {
                _shot.FlightEnded -= ResolveShot;
            }
        }

        private void FixedUpdate()
        {
            UpdatePlayer();
        }

        public bool TryReceiveHit(Component inSource)
        {
            if (IsInitialized == false || _playing == false || isActiveAndEnabled == false || _hitAccepted || _hitCollider.enabled == false)
            {
                return false;
            }
            _hitAccepted = true;
            SetPlaying(false);
            HitAccepted?.Invoke();
            return true;
        }

        internal long GetNormalShotEndCount()
        {
            return NormalShotEndCount;
        }

        internal void ResetShotHistory()
        {
            _normalShotEndCount = 0;
        }

        internal void SetPlaying(bool inPlaying)
        {
            _playing = IsInitialized && inPlaying && _hitAccepted == false;
            if (_playing == false)
            {
                _inputState  = default;
                _pendingFire = false;
            }
            ApplyPlayingState();
        }

        internal void Respawn()
        {
            if (IsInitialized == false)
            {
                return;
            }
            SetPlaying(false);
            _shot.ResetFlight();
            ReleaseShot();
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
            if (_shotOccupied == false)
            {
                _pendingFire = true;
            }
        }

        private void ResolveShot(HitTargetType inTargetType)
        {
            ReleaseShot();
            _normalShotEndCount++;
            ShotResolved?.Invoke(inTargetType);
        }

        private void ReleaseShot()
        {
            _shotOccupied = false;
        }

        private void ApplyPlayingState()
        {
            bool playing = IsInitialized && _playing && isActiveAndEnabled;
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
                _pendingFire  = false;
                _shotOccupied = _shot.TryLaunch((Vector2)position + Vector2.up * (8f * ArcadeViewport.UnitsPerPixel));
            }
        }
    }
}
