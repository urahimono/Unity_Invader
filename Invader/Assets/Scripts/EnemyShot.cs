using UnityEngine;

namespace UnityInvader
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public sealed class EnemyShot : MonoBehaviour, IHitReceiver
    {
        [SerializeField]
        private SpriteRenderer _renderer;

        private Rigidbody2D _body;
        private int         _stepCount;
        private bool        _playing;
        private bool        _fast;
        private bool        _resolvingHit;
        private int         _explosionSteps;
        private Color       _flightColor;
        private Vector3     _flightScale;

        public HitTargetType TargetType => HitTargetType.None;

        internal bool InFlight => gameObject.activeSelf;

        internal int StepCount => _stepCount;

        // Placeholder rectangle, not the original shot animations or collision shapes.
        public  const float Width          = 3f;
        public  const float Height         = 8f;
        private const int   ExplosionSteps = 4;

        private void Awake()
        {
            CacheBody();
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
            if (_body != null)
            {
                _body.simulated = false;
            }
        }

        private void OnTriggerEnter2D(Collider2D inOther)
        {
            if (inOther.TryGetComponent<IHitReceiver>(out var receiver))
            {
                ResolveHit(receiver);
            }
        }

        public bool TryReceiveHit(Component inSource)
        {
            if (_playing == false || isActiveAndEnabled == false || _resolvingHit || _explosionSteps > 0)
            {
                return false;
            }
            if (inSource is PlayerShot shot && shot.CanHit)
            {
                BeginExplosion();
                return true;
            }
            return false;
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
            _flightColor       = _renderer.color;
            _flightScale       = _renderer.transform.localScale;
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
            if (_explosionSteps > 0)
            {
                _renderer.color                = _flightColor;
                _renderer.transform.localScale = _flightScale;
            }
            _explosionSteps = 0;
            _resolvingHit   = false;
            _stepCount      = 0;
            gameObject.SetActive(false);
        }

        internal void PreparePhysicsStep()
        {
            if (_playing && isActiveAndEnabled && _explosionSteps == 0)
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
            if (_explosionSteps > 0)
            {
                if (_explosionSteps == 1)
                {
                    ResetFlight();
                }
                else
                {
                    _explosionSteps--;
                }
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
            _body.simulated      = playing && _explosionSteps == 0;
            _body.linearVelocity = Vector2.zero;
        }

        private void MoveShot()
        {
            var position = _body.position;
            position.y -= (_fast ? 5f : 4f) * ArcadeViewport.UnitsPerPixel;
            if (Shield.TryAbsorbShot(_body.position, position,
                new Vector2(Width, Height) * ArcadeViewport.UnitsPerPixel, false))
            {
                BeginExplosion();
                return;
            }
            if (position.y + Height / 2f * ArcadeViewport.UnitsPerPixel <= -ArcadeViewport.ScreenHeight / 2f * ArcadeViewport.UnitsPerPixel)
            {
                BeginExplosion();
                return;
            }
            _body.MovePosition(position);
        }

        private void ResolveHit(IHitReceiver inReceiver)
        {
            if (_playing == false || isActiveAndEnabled == false || _resolvingHit || _explosionSteps > 0)
            {
                return;
            }
            _resolvingHit = true;
            if (inReceiver.TryReceiveHit(this))
            {
                BeginExplosion();
            }
            _resolvingHit = false;
        }

        private void BeginExplosion()
        {
            // Count only subsequent updates of this type; keep StepCount for Reload.
            _explosionSteps                = ExplosionSteps;
            _renderer.color                = Color.yellow;
            _renderer.transform.localScale = Vector3.Scale(_flightScale, new Vector3(2f, 0.5f, 1f));
            ApplyPlayingState();
        }
    }
}
