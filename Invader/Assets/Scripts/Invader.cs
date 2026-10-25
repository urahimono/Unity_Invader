using UnityEngine;

namespace UnityInvader
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class Invader : MonoBehaviour
    {
        [SerializeField]
        private SpriteRenderer _renderer;

        private InvaderFormation _formation;
        private Collider2D       _collider;
        private Sprite           _frameA;
        private Sprite           _frameB;
        private int              _score;

        internal void Initialize(InvaderFormation inFormation, Sprite inFrameA, Sprite inFrameB, int inScore)
        {
            _formation = inFormation;
            _collider  = GetComponent<Collider2D>();
            _frameA    = inFrameA;
            _frameB    = inFrameB;
            _score     = inScore;
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

        internal bool TryReceiveHit()
        {
            if (isActiveAndEnabled == false || _collider.enabled == false)
            {
                return false;
            }
            // Active state is the single source of individual life, including queued trigger callbacks.
            gameObject.SetActive(false);
            _formation.NotifyDefeated(this, _score);
            return true;
        }
    }
}
