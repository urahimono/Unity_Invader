using System.Collections.Generic;
using UnityEngine;

namespace UnityInvader.Prototypes.Physics2DProbe
{
    [DisallowMultipleComponent]
    public sealed class Physics2DProbeFormation : MonoBehaviour
    {
        [SerializeField]
        private Physics2DProbeInvader _invaderPrefab;
        [SerializeField]
        private Vector2[]            _positions = { Vector2.zero };

        public int AliveCount => _aliveInvaders.Count;

        public int DefeatNotifications { get; private set; }

        private readonly List<Physics2DProbeInvader>    _invaders      = new List<Physics2DProbeInvader>();
        private readonly HashSet<Physics2DProbeInvader> _aliveInvaders = new HashSet<Physics2DProbeInvader>();

        public void ResetProbe()
        {
            ClearInvaders();
            DefeatNotifications = 0;
            foreach (var position in _positions)
            {
                var invader                     = Instantiate(_invaderPrefab, transform);
                invader.transform.localPosition = position;
                invader.JoinFormation(this);
                _invaders.Add(invader);
                _aliveInvaders.Add(invader);
            }
        }

        internal void NotifyDefeated(Physics2DProbeInvader inInvader)
        {
            if (_aliveInvaders.Remove(inInvader) == false)
            {
                return;
            }
            DefeatNotifications++;
            Debug.Log($"PHYSICS2D_PROBE_DEFEAT: {name}, alive={AliveCount}, notifications={DefeatNotifications}", this);
        }

        private void ClearInvaders()
        {
            foreach (var invader in _invaders)
            {
                if (invader != null)
                {
                    invader.gameObject.SetActive(false);
                    Destroy(invader.gameObject);
                }
            }
            _invaders.Clear();
            _aliveInvaders.Clear();
        }
    }
}
