using UnityEngine;

namespace UnityInvader
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public sealed class Ufo : MonoBehaviour, IHitReceiver
    {
        [SerializeField]
        private SpriteRenderer _renderer;

        private Rigidbody2D   _body;
        private BoxCollider2D _hitCollider;
        private bool          _playing;
        private int           _direction;
        private int           _ticksRemaining;
        private bool          _pending;

        public HitTargetType TargetType => HitTargetType.Ufo;

        internal bool InFlight => gameObject.activeSelf;

        // Provisional centered rectangle and lane, not ROM sprite/coordinate geometry.
        public  const float Width   = 24f;
        public  const float Height  = 8f;
        private const float LaneY   = (232f - ArcadeViewport.ScreenHeight / 2f) * ArcadeViewport.UnitsPerPixel;
        private const float Outside = (ArcadeViewport.ScreenWidth / 2f + Width / 2f) * ArcadeViewport.UnitsPerPixel;

        private const int ReloadTicks = 0x0600;

        private void Awake()
        {
            CacheComponents();
        }

        private void OnEnable()
        {
            ApplyPlayingState();
        }

        private void OnDisable()
        {
            if (_renderer != null)
            {
                _renderer.enabled = false;
            }
            if (_hitCollider != null)
            {
                _hitCollider.enabled = false;
            }
            if (_body != null)
            {
                _body.simulated = false;
            }
        }

        public bool TryReceiveHit(Component inSource)
        {
            if (_playing == false || isActiveAndEnabled == false || _hitCollider.enabled == false)
            {
                return false;
            }
            // Consume before returning; later Trigger callbacks cannot accept this UFO again.
            ResetFlight();
            return true;
        }

        internal bool TryLaunch(bool inMovingRight)
        {
            if (_playing == false || InFlight)
            {
                return false;
            }
            CacheComponents();
            _direction         = inMovingRight ? 1 : -1;
            var position       = new Vector2(inMovingRight ? -Outside : Outside, LaneY);
            transform.position = position;
            _body.position     = position;
            gameObject.SetActive(true);
            return true;
        }

        internal void SetPlaying(bool inPlaying)
        {
            _playing = inPlaying;
            ApplyPlayingState();
        }

        internal void ResetFlight()
        {
            gameObject.SetActive(false);
        }

        internal void ResetCycle()
        {
            ResetFlight();
            _ticksRemaining = ReloadTicks;
            _pending        = false;
        }

        internal void TickCountdown(bool inHasDescended)
        {
            if (inHasDescended == false)
            {
                return;
            }
            // 0913..092D checks zero before decrementing; reload also consumes one tick.
            // Called while inactive too: waiting UFOs still count each shared clock tick.
            if (_ticksRemaining == 0)
            {
                _ticksRemaining = ReloadTicks;
                _pending        = true;
            }
            _ticksRemaining--;
        }

        internal bool ProcessPhase(bool inCanLaunch, bool inMovingRight)
        {
            if (InFlight)
            {
                AdvanceStep();
                return true;
            }
            if (_pending && inCanLaunch)
            {
                if (TryLaunch(inMovingRight))
                {
                    _pending = false;
                }
                // An eligible attempt consumes the shared phase even if launch fails.
                return true;
            }
            return false;
        }

        internal void PreparePhysicsStep()
        {
            if (_playing && isActiveAndEnabled)
            {
                _body.MovePosition(_body.position);
            }
        }

        internal void AdvanceStep()
        {
            if (_playing == false || isActiveAndEnabled == false)
            {
                return;
            }
            var position = _body.position;
            position.x += _direction * 2f * ArcadeViewport.UnitsPerPixel;
            if ((_direction > 0 && position.x >= Outside) || (_direction < 0 && position.x <= -Outside))
            {
                ResetFlight();
                return;
            }
            _body.MovePosition(position);
        }

        private void CacheComponents()
        {
            if (_body == null)
            {
                _body        = GetComponent<Rigidbody2D>();
                _hitCollider = GetComponent<BoxCollider2D>();
            }
        }

        private void ApplyPlayingState()
        {
            CacheComponents();
            bool playing         = _playing && isActiveAndEnabled;
            _renderer.enabled    = playing;
            _hitCollider.enabled = playing;
            _body.simulated      = playing;
            _body.linearVelocity = Vector2.zero;
        }
    }
}
