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
        private float       _stepElapsed;
        private bool        _playing;
        private bool        _fast;
        private bool        _resolvingHit;

        internal bool InFlight => gameObject.activeSelf;

        // Placeholder rectangle, not the original Rolling animation or collision shape.
        public const float Width  = 3f;
        public const float Height = 8f;

        private const float StepPeriod = 3f / 60f;

        private void Awake()
        {
            CacheBody();
        }

        private void OnEnable()
        {
            ApplyPlayingState();
        }

        private void FixedUpdate()
        {
            UpdateShot();
        }

        private void OnTriggerEnter2D(Collider2D inOther)
        {
            if (inOther.TryGetComponent<Player>(out var player))
            {
                ResolveHit(player);
            }
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
            _stepElapsed       = 0f;
            _resolvingHit      = false;
            gameObject.SetActive(true);
            return true;
        }

        internal void SetPlaying(bool inPlaying)
        {
            _playing = inPlaying;
            if (inPlaying == false)
            {
                _stepElapsed = 0f;
            }
            ApplyPlayingState();
        }

        internal void SetFast(bool inFast)
        {
            _fast = inFast;
        }

        internal void ResetFlight()
        {
            _stepElapsed = 0f;
            gameObject.SetActive(false);
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

        private void UpdateShot()
        {
            if (_playing == false)
            {
                return;
            }
            _stepElapsed += Time.fixedDeltaTime;
            var position = _body.position;
            while (_stepElapsed + 1e-7f >= StepPeriod)
            {
                _stepElapsed -= StepPeriod;
                position.y -= (_fast ? 5f : 4f) * ArcadeViewport.UnitsPerPixel;
            }
            if (position.y + Height / 2f * ArcadeViewport.UnitsPerPixel <= -ArcadeViewport.ScreenHeight / 2f * ArcadeViewport.UnitsPerPixel)
            {
                gameObject.SetActive(false);
                return;
            }
            // Also hold the current position on non-step ticks, so no movement velocity persists.
            _body.MovePosition(position);
        }

        private void ResolveHit(Player inPlayer)
        {
            if (_playing == false || isActiveAndEnabled == false || _resolvingHit)
            {
                return;
            }
            _resolvingHit = true;
            if (inPlayer.TryReceiveHit())
            {
                gameObject.SetActive(false);
            }
            _resolvingHit = false;
        }
    }
}
