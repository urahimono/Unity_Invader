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

        public event Action FlightEnded;

        public  const float Width  = 1f;
        public  const float Height = 4f;
        private const float Speed  = 240f * ArcadeViewport.UnitsPerPixel;

        private void OnEnable()
        {
            ApplyPlayingState();
        }

        private void FixedUpdate()
        {
            if (TryHitSwept())
            {
                return;
            }
            CheckScreenExit();
        }

        private void OnTriggerEnter2D(Collider2D inOther)
        {
            ResolveHit(inOther);
        }

        private void OnDisable()
        {
            ApplyPlayingState();
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
            gameObject.SetActive(true);
            return true;
        }

        internal void SetPlaying(bool inPlaying)
        {
            enabled = inPlaying;
        }

        internal void ResetFlight()
        {
            gameObject.SetActive(false);
        }

        private void ApplyPlayingState()
        {
            bool playing         = isActiveAndEnabled;
            _renderer.enabled    = playing;
            _body.simulated      = playing;
            _body.linearVelocity = playing ? Vector2.up * Speed : Vector2.zero;
        }

        private void CheckScreenExit()
        {
            if (_body.position.y >= ArcadeViewport.ScreenHeight / 2f * ArcadeViewport.UnitsPerPixel)
            {
                FinishFlight();
            }
        }

        private bool TryHitSwept()
        {
            var center = _body.position + Vector2.up * (Height * 0.5f * ArcadeViewport.UnitsPerPixel);
            var size   = new Vector2(Width, Height) * ArcadeViewport.UnitsPerPixel;
            var hit    = new HitInfo(this, center, center + _body.linearVelocity * Time.fixedDeltaTime, size, Vector2.up);
            if (ProjectileHitDetector.TryResolveSwept(in hit))
            {
                FinishFlight();
                return true;
            }
            return false;
        }

        private void FinishFlight()
        {
            if (gameObject.activeSelf == false)
            {
                return;
            }
            ResetFlight();
            FlightEnded?.Invoke();
        }

        private void ResolveHit(Collider2D inOther)
        {
            if (isActiveAndEnabled == false)
            {
                return;
            }
            var center = _body.position + Vector2.up * (Height * 0.5f * ArcadeViewport.UnitsPerPixel);
            var size   = new Vector2(Width, Height) * ArcadeViewport.UnitsPerPixel;
            var hit    = new HitInfo(this, center, center, size, Vector2.up);
            if (ProjectileHitDetector.TryResolveTrigger(inOther, in hit))
            {
                FinishFlight();
            }
        }
    }
}
