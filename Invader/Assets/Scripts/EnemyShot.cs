using UnityEngine;

namespace UnityInvader
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public sealed class EnemyShot : MonoBehaviour
    {
        [SerializeField]
        private SpriteRenderer _renderer;

        private Rigidbody2D _body;
        private int         _stepCount;
        private bool        _playing;
        private bool        _fast;
        private bool        _resolvingHit;

        internal bool InFlight => gameObject.activeSelf;

        internal int StepCount => _stepCount;

        // Placeholder rectangle, not the original shot animations or collision shapes.
        public const float Width  = 3f;
        public const float Height = 8f;

        private void Awake()
        {
            CacheBody();
        }

        private void OnEnable()
        {
            ApplyPlayingState();
        }

        private void OnTriggerEnter2D(Collider2D inOther)
        {
            ResolveHit(inOther);
        }

        private void OnDisable()
        {
            if (_renderer != null)
            {
                _renderer.enabled = false;
            }
            if (_body != null)
            {
                _body.simulated = false;
            }
        }

        internal bool TryLaunch(Vector2 inPosition)
        {
            if (_playing == false || InFlight)
            {
                return false;
            }
            CacheBody();
            transform.position = new Vector3(inPosition.x, inPosition.y, transform.position.z);
            _body.position     = inPosition;
            _stepCount         = 1; // ROM 05BF: starting a shot counts as its first step.
            _resolvingHit      = false;
            gameObject.SetActive(true);
            return true;
        }

        internal void SetPlaying(bool inPlaying)
        {
            _playing = inPlaying;
            ApplyPlayingState();
        }

        internal void SetFast(bool inFast)
        {
            _fast = inFast;
        }

        internal void ResetFlight()
        {
            _stepCount = 0;
            gameObject.SetActive(false);
        }

        internal void PreparePhysicsStep()
        {
            if (_playing && isActiveAndEnabled)
            {
                // MovePosition's velocity must not persist on another type's phase.
                _body.MovePosition(_body.position);
            }
        }

        internal void AdvanceStep()
        {
            if (_playing == false || isActiveAndEnabled == false)
            {
                return;
            }
            _stepCount = (_stepCount + 1) & 0xFF; // ROM INC byte at 05D0.
            MoveShot();
        }

        private void CacheBody()
        {
            if (_body == null)
            {
                _body = GetComponent<Rigidbody2D>();
            }
        }

        private void ApplyPlayingState()
        {
            CacheBody();
            bool playing         = _playing && isActiveAndEnabled;
            _renderer.enabled    = playing;
            _body.simulated      = playing;
            _body.linearVelocity = Vector2.zero;
        }

        private void MoveShot()
        {
            var position = _body.position;
            position.y -= (_fast ? 5f : 4f) * ArcadeViewport.UnitsPerPixel;
            var hit = new HitInfo(this, _body.position, position,
                new Vector2(Width, Height) * ArcadeViewport.UnitsPerPixel, Vector2.down);
            if (ProjectileHitDetector.TryResolveSwept(in hit))
            {
                ResetFlight();
                return;
            }
            if (position.y + Height / 2f * ArcadeViewport.UnitsPerPixel <= -ArcadeViewport.ScreenHeight / 2f * ArcadeViewport.UnitsPerPixel)
            {
                ResetFlight();
                return;
            }
            _body.MovePosition(position);
        }

        private void ResolveHit(Collider2D inOther)
        {
            if (_playing == false || isActiveAndEnabled == false || _resolvingHit)
            {
                return;
            }
            _resolvingHit = true;
            var position = _body.position;
            var hit      = new HitInfo(this, position, position,
                new Vector2(Width, Height) * ArcadeViewport.UnitsPerPixel, Vector2.down);
            if (ProjectileHitDetector.TryResolveTrigger(inOther, in hit))
            {
                ResetFlight();
            }
            _resolvingHit = false;
        }
    }
}
