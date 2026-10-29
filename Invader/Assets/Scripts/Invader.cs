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
        private bool       _initializationAttempted;
        private bool       _initialized;

        public event Action<Invader, Component> Defeated;

        public HitTargetType TargetType { get; private set; }

        public bool TryReceiveHit(Component inSource)
        {
            if (_initialized == false || isActiveAndEnabled == false || _collider.enabled == false)
            {
                return false;
            }
            // Active state also rejects later queued contacts with this individual.
            gameObject.SetActive(false);
            Defeated?.Invoke(this, inSource);
            return true;
        }

        internal bool Initialize(InvaderType inType)
        {
            if (_initializationAttempted)
            {
                return _initialized;
            }
            _initializationAttempted = true;
            _collider = GetComponent<Collider2D>();
            if (_renderer == null || _collider == null)
            {
                Debug.LogError("Invader requires a SpriteRenderer reference and Collider2D.", this);
                enabled = false;
                SetPlaying(false);
                return false;
            }
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
                    Debug.LogError($"Unknown Invader type: {inType}.", this);
                    enabled = false;
                    SetPlaying(false);
                    return false;
            }
            if (_animationFrames == null || _animationFrames.Length == 0 ||
                Array.Exists(_animationFrames, inFrame => inFrame == null))
            {
                Debug.LogError($"Invader requires non-empty Sprite frames without null entries for {inType}. Configure Invader Animation first.", this);
                enabled = false;
                SetPlaying(false);
                return false;
            }
            _initialized = true;
            return true;
        }

        internal void BeginWave()
        {
            if (_initialized == false)
            {
                return;
            }
            gameObject.SetActive(true);
            _frameIndex      = 0;
            _renderer.sprite = _animationFrames[_frameIndex];
        }

        internal void SetPlaying(bool inPlaying)
        {
            bool playing = _initialized && inPlaying && isActiveAndEnabled;
            if (_renderer != null)
            {
                _renderer.enabled = playing;
            }
            if (_collider != null)
            {
                _collider.enabled = playing;
            }
        }

        internal void AdvanceAnimation()
        {
            if (_initialized == false)
            {
                return;
            }
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
