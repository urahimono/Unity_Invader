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

        private bool _playing = true;
        private bool _inFlight;
        private bool _resolvingHit;

        public event Action FlightEnded;
        public event Action UfoDefeated;

        public bool InFlight => _inFlight;

        public  const float Width  = 1f;
        public  const float Height = 4f;
        private const float Speed  = 240f * ArcadeViewport.UnitsPerPixel;

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
            ResolveHit(inOther);
        }

        private void OnDisable()
        {
            ApplyPlayingState();
        }

        public bool TryLaunch(Vector2 inPosition)
        {
            if (_playing == false || isActiveAndEnabled == false || InFlight)
            {
                return false;
            }
            _body.position     = inPosition;
            transform.position = inPosition;
            _resolvingHit      = false;
            _inFlight          = true;
            ApplyPlayingState();
            return true;
        }

        internal void SetPlaying(bool inPlaying)
        {
            _playing = inPlaying;
            ApplyPlayingState();
        }

        internal void ResetFlight()
        {
            _resolvingHit = false;
            _inFlight     = false;
            ApplyPlayingState();
        }

        private void ApplyPlayingState()
        {
            bool playing         = _playing && isActiveAndEnabled && InFlight;
            _renderer.enabled    = playing;
            _body.simulated      = playing;
            _body.linearVelocity = playing ? Vector2.up * Speed : Vector2.zero;
        }

        private void CheckScreenExit()
        {
            if (_playing && InFlight && _body.position.y >= ArcadeViewport.ScreenHeight / 2f * ArcadeViewport.UnitsPerPixel)
            {
                FinishFlight();
            }
        }

        private bool TryHitShield()
        {
            if (_playing == false || InFlight == false || isActiveAndEnabled == false || _resolvingHit)
            {
                return false;
            }
            var center = _body.position + Vector2.up * (Height * 0.5f * ArcadeViewport.UnitsPerPixel);
            var size   = new Vector2(Width, Height) * ArcadeViewport.UnitsPerPixel;
            if (Shield.TryAbsorbShot(center, center + _body.linearVelocity * Time.fixedDeltaTime, size, true))
            {
                FinishFlight();
                return true;
            }
            return false;
        }

        private void FinishFlight()
        {
            if (InFlight == false)
            {
                return;
            }
            ResetFlight();
            FlightEnded?.Invoke();
        }

        private void ResolveHit(Collider2D inOther)
        {
            if (_playing == false || InFlight == false || isActiveAndEnabled == false || _resolvingHit)
            {
                return;
            }
            _resolvingHit = true;
            if (inOther.TryGetComponent<Invader>(out var invader) && invader.TryReceiveHit())
            {
                FinishFlight();
            }
            else if (inOther.TryGetComponent<Ufo>(out var ufo) && ufo.TryReceiveHit())
            {
                FinishFlight();
                UfoDefeated?.Invoke();
            }
            _resolvingHit = false;
        }
    }
}
