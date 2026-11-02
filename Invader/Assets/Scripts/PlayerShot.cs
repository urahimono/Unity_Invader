using System;
using UnityEngine;

namespace UnityInvader
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public sealed class PlayerShot : MonoBehaviour
    {
        [SerializeField]
        private SpriteRenderer _renderer;
        [SerializeField]
        private Rigidbody2D    _body;

        private float   _explosionRemaining;
        private Color   _flightColor;
        private Vector3 _flightScale;

        // Flight result is immediate; the slot becomes reusable only after the explosion.
        public event Action<HitTargetType> FlightEnded;
        internal event Action BecameReusable;

        internal bool CanHit => isActiveAndEnabled && _explosionRemaining <= 0f;

        public  const float Width             = 1f;
        public  const float Height            = 4f;
        private const float Speed             = 240f * ArcadeViewport.UnitsPerPixel;
        private const float ExplosionDuration = 16f / 60f;

        private void OnEnable()
        {
            ApplyPlayingState();
        }

        private void OnDisable()
        {
            ApplyPlayingState();
        }

        private void FixedUpdate()
        {
            if (_explosionRemaining > 0f)
            {
                AdvanceExplosion();
                return;
            }
            if (TryHitShield())
            {
                return;
            }
            CheckScreenExit();
        }

        private void OnTriggerEnter2D(Collider2D inOther)
        {
            ResolveHit(inOther);
        }

        public bool TryLaunch(Vector2 inPosition)
        {
            if (enabled == false || gameObject.activeSelf ||
                (transform.parent != null && transform.parent.gameObject.activeInHierarchy == false))
            {
                return false;
            }
            _body.position     = inPosition;
            transform.position = inPosition;
            _flightColor       = _renderer.color;
            _flightScale       = _renderer.transform.localScale;
            gameObject.SetActive(true);
            return true;
        }

        internal void SetPlaying(bool inPlaying)
        {
            enabled = inPlaying;
        }

        internal void ResetFlight()
        {
            if (_explosionRemaining > 0f)
            {
                _renderer.color                = _flightColor;
                _renderer.transform.localScale = _flightScale;
            }
            _explosionRemaining = 0f;
            gameObject.SetActive(false);
        }

        private void ApplyPlayingState()
        {
            bool playing         = isActiveAndEnabled;
            bool flying          = playing && _explosionRemaining <= 0f;
            _renderer.enabled    = playing;
            _body.simulated      = flying;
            _body.linearVelocity = flying ? Vector2.up * Speed : Vector2.zero;
        }

        private void CheckScreenExit()
        {
            if (_body.position.y >= ArcadeViewport.ScreenHeight / 2f * ArcadeViewport.UnitsPerPixel)
            {
                FinishFlight();
            }
        }

        private bool TryHitShield()
        {
            var center = _body.position + Vector2.up * (Height * 0.5f * ArcadeViewport.UnitsPerPixel);
            var size   = new Vector2(Width, Height) * ArcadeViewport.UnitsPerPixel;
            if (Shield.TryAbsorbShot(center, center + _body.linearVelocity * Time.fixedDeltaTime, size, true))
            {
                FinishFlight();
                return true;
            }
            return false;
        }

        private void FinishFlight(HitTargetType inTargetType = HitTargetType.None)
        {
            if (gameObject.activeSelf == false || _explosionRemaining > 0f)
            {
                return;
            }
            // ROM state 3 and the separate Invader state 5 both wait 16 counts.
            // Begin now; only subsequent FixedUpdates consume the wait.
            _explosionRemaining            = ExplosionDuration;
            _renderer.color                = Color.yellow;
            _renderer.transform.localScale = Vector3.Scale(_flightScale, new Vector3(4f, 0.75f, 1f));
            ApplyPlayingState();
            FlightEnded?.Invoke(inTargetType);
        }

        private void AdvanceExplosion()
        {
            if (_explosionRemaining > Time.fixedDeltaTime)
            {
                _explosionRemaining -= Time.fixedDeltaTime;
                return;
            }
            ResetFlight();
            BecameReusable?.Invoke();
        }

        private void ResolveHit(Collider2D inOther)
        {
            if (CanHit == false)
            {
                return;
            }
            if (inOther.TryGetComponent<IHitReceiver>(out var receiver) && receiver.TryReceiveHit(this))
            {
                FinishFlight(receiver.TargetType);
            }
        }
    }
}
