using UnityEngine;

namespace UnityInvader.Prototypes.Physics2DProbe
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public sealed class Physics2DProbeShot : MonoBehaviour
    {
        [SerializeField]
        [Min(0.1f)]
        private float _speed    = 4f;
        [SerializeField]
        [Min(0.1f)]
        private float _lifetime = 3f;

        private Rigidbody2D _body;
        private bool        _consumed;
        private float       _remainingLifetime;

        private void Awake()
        {
            _body                = GetComponent<Rigidbody2D>();
            _body.linearVelocity = Vector2.up * _speed;
            _remainingLifetime   = _lifetime;
        }

        private void FixedUpdate()
        {
            UpdateLifetime();
        }

        public bool TryConsume()
        {
            if (_consumed || isActiveAndEnabled == false)
            {
                return false;
            }
            // Destroy is deferred: the flag, simulation and active state change immediately.
            _consumed       = true;
            _body.simulated = false;
            gameObject.SetActive(false);
            Destroy(gameObject);
            return true;
        }

        private void UpdateLifetime()
        {
            _remainingLifetime -= Time.fixedDeltaTime;
            if (_remainingLifetime <= 0f)
            {
                TryConsume();
            }
        }
    }
}
