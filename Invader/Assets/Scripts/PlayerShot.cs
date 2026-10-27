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

        public event Action<HitTargetType> FlightEnded;

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
            if (gameObject.activeSelf == false)
            {
                return;
            }
            ResetFlight();
            FlightEnded?.Invoke(inTargetType);
        }

        private void ResolveHit(Collider2D inOther)
        {
            if (isActiveAndEnabled == false)
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
