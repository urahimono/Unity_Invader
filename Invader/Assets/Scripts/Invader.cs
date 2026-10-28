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
        [SerializeField]
        private Sprite[]       _topFrames;
        [SerializeField]
        private Sprite[]       _middleFrames;
        [SerializeField]
        private Sprite[]       _bottomFrames;

        private Collider2D _collider;
        private Sprite[]   _animationFrames;
        private int        _frameIndex;

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

        internal void Initialize(InvaderType inType)
        {
            _collider = GetComponent<Collider2D>();
            switch (inType)
            {
                case InvaderType.Top:
                    _animationFrames = _topFrames;
                    TargetType       = HitTargetType.InvaderTop;
                    break;
                case InvaderType.Middle:
                    _animationFrames = _middleFrames;
                    TargetType       = HitTargetType.InvaderMiddle;
                    break;
                case InvaderType.Bottom:
                    _animationFrames = _bottomFrames;
                    TargetType       = HitTargetType.InvaderBottom;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(inType), inType, "Unknown Invader type.");
            }
            if (_animationFrames == null || _animationFrames.Length == 0 ||
                Array.Exists(_animationFrames, inFrame => inFrame == null))
            {
                throw new InvalidOperationException("Invader requires non-empty Sprite frames for its type. Configure Invader Animation first.");
            }
        }

        internal void BeginWave()
        {
            gameObject.SetActive(true);
            _frameIndex      = 0;
            _renderer.sprite = _animationFrames[_frameIndex];
        }

        internal void SetPlaying(bool inPlaying)
        {
            _renderer.enabled = inPlaying && isActiveAndEnabled;
            _collider.enabled = inPlaying && isActiveAndEnabled;
        }

        internal void AdvanceAnimation()
        {
            _frameIndex      = (_frameIndex + 1) % _animationFrames.Length;
            _renderer.sprite = _animationFrames[_frameIndex];
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
