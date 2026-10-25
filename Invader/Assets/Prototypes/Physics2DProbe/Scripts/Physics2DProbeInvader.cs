using UnityEngine;

namespace UnityInvader.Prototypes.Physics2DProbe
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
    public sealed class Physics2DProbeInvader : MonoBehaviour
    {
        private Physics2DProbeFormation _formation;
        private bool                    _defeated;

        private void OnTriggerEnter2D(Collider2D inOther)
        {
            if (inOther.TryGetComponent<Physics2DProbeShot>(out var shot))
            {
                ReceiveShot(shot);
            }
        }

        internal void JoinFormation(Physics2DProbeFormation inFormation)
        {
            _formation = inFormation;
        }

        private void ReceiveShot(Physics2DProbeShot inShot)
        {
            if (_defeated || isActiveAndEnabled == false || _formation == null)
            {
                return;
            }
            // Commit consumption before any defeat side effects or subsequent callbacks.
            if (inShot.TryConsume() == false)
            {
                return;
            }
            _defeated = true;
            gameObject.SetActive(false);
            _formation.NotifyDefeated(this);
        }
    }
}
