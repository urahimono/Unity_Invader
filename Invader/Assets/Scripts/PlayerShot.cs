using UnityEngine;

namespace UnityInvader
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public sealed class PlayerShot : MonoBehaviour
    {
        [SerializeField]
        private SpriteRenderer _renderer;

        private Rigidbody2D    _body;
        private bool           _playing;
        private bool           _resolvingHit;

        public bool InFlight => gameObject.activeSelf;

        public  const float Width  = 1f;
        public  const float Height = 4f;
        private const float Speed  = 240f * ArcadeViewport.UnitsPerPixel;

        private void Awake()
        {
            CacheComponents();
        }

        private void OnEnable()
        {
            ApplyPlayingState();
        }

        private void FixedUpdate()
        {
            if (TryHitShield())
            {
                return;
            }
            CheckScreenExit();
        }

        private void OnTriggerEnter2D(Collider2D inOther)
        {
            if (inOther.TryGetComponent<Invader>(out var invader))
            {
                ResolveHit(invader);
            }
        }

        private void OnDisable()
        {
            if (_body != null)
            {
                _body.simulated = false;
            }
        }

        public bool TryLaunch(Vector2 inPosition)
        {
            if (_playing == false || InFlight)
            {
                return false;
            }
            CacheComponents();
            _body.position     = inPosition;
            transform.position = inPosition;
            _resolvingHit      = false;
            gameObject.SetActive(true);
            return true;
        }

        internal void SetPlaying(bool inPlaying)
        {
            _playing = inPlaying;
            CacheComponents();
            ApplyPlayingState();
        }

        internal void ResetFlight()
        {
            _resolvingHit = false;
            gameObject.SetActive(false);
        }

        private void CacheComponents()
        {
            if (_body == null)
            {
                _body = GetComponent<Rigidbody2D>();
            }
        }

        private void ApplyPlayingState()
        {
            CacheComponents();
            bool playing         = _playing && isActiveAndEnabled;
            _renderer.enabled    = playing;
            _body.simulated      = playing;
            _body.linearVelocity = playing ? Vector2.up * Speed : Vector2.zero;
        }

        private void CheckScreenExit()
        {
            if (_playing && _body.position.y >= ArcadeViewport.ScreenHeight / 2f * ArcadeViewport.UnitsPerPixel)
            {
                gameObject.SetActive(false);
            }
        }

        private bool TryHitShield()
        {
            if (_playing == false || isActiveAndEnabled == false || _resolvingHit)
            {
                return false;
            }
            var center = _body.position + Vector2.up * (Height * 0.5f * ArcadeViewport.UnitsPerPixel);
            var size   = new Vector2(Width, Height) * ArcadeViewport.UnitsPerPixel;
            if (Shield.TryAbsorbShot(center, center + _body.linearVelocity * Time.fixedDeltaTime, size, true))
            {
                ResetFlight();
                return true;
            }
            return false;
        }

        private void ResolveHit(Invader inInvader)
        {
            if (_playing == false || isActiveAndEnabled == false || _resolvingHit)
            {
                return;
            }
            _resolvingHit = true;
            if (inInvader.TryReceiveHit())
            {
                // Inactive state consumes this reusable shot immediately after an accepted hit.
                gameObject.SetActive(false);
            }
            _resolvingHit = false;
        }
    }
}
