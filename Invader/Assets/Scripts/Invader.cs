using System;
using UnityEngine;

namespace UnityInvader
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class Invader : MonoBehaviour, IHitReceiver
    {
        [SerializeField]
        private SpriteRenderer _renderer;

        private Collider2D _collider;
        private Sprite     _frameA;
        private Sprite     _frameB;

        public event Action<Invader, Component> Defeated;

        public HitTargetType TargetType { get; private set; }

        public bool TryReceiveHit(Component inSource)
        {
            if (isActiveAndEnabled == false || _collider.enabled == false)
            {
                return false;
            }
            // Active state also rejects later queued contacts with this individual.
            gameObject.SetActive(false);
            Defeated?.Invoke(this, inSource);
            return true;
        }

        internal void Initialize(Sprite inFrameA, Sprite inFrameB, HitTargetType inTargetType)
        {
            _collider  = GetComponent<Collider2D>();
            _frameA    = inFrameA;
            _frameB    = inFrameB;
            TargetType = inTargetType;
        }

        internal void BeginWave()
        {
            gameObject.SetActive(true);
            ApplyAnimation(false);
        }

        internal void SetPlaying(bool inPlaying)
        {
            _renderer.enabled = inPlaying && isActiveAndEnabled;
            _collider.enabled = inPlaying && isActiveAndEnabled;
        }

        internal void ApplyAnimation(bool inFrameB)
        {
            _renderer.sprite = inFrameB ? _frameB : _frameA;
        }

        internal void ErodeShields(Vector2 inMovement)
        {
            if (isActiveAndEnabled == false || _collider == null || _collider.enabled == false)
            {
                return;
            }
            Shield.ErodeByInvader(_collider.bounds, inMovement);
        }
    }
}
