using UnityEngine;

namespace UnityInvader
{
    // Presentation only. PlayerDead lifetime is still owned by GameplayDriver.
    [DisallowMultipleComponent]
    public sealed class PlayerDeathEffect : MonoBehaviour
    {
        [SerializeField]
        private Player         _player;
        [SerializeField]
        private GameplayDriver _driver;
        [SerializeField]
        private SpriteRenderer _renderer;
        [SerializeField]
        private Sprite[]       _frames;

        private bool   _hasHit;
        private double _startedAt;

        // Provisional visual cadence, not a gameplay/death timer or a measured original value.
        private const double FrameInterval = 0.1;

        private void OnEnable()
        {
            ResetEffect();
            if (_player == null || _driver == null || _renderer == null || _frames == null ||
                _frames.Length != 2 || _frames[0] == null || _frames[1] == null)
            {
                Debug.LogError("Player Death Effect requires Player, Driver, renderer and two frames.", this);
                enabled = false;
                return;
            }
            _player.HitAccepted += RecordHit;
            _driver.GameStarting += ResetEffect;
        }

        private void OnDisable()
        {
            if (_player != null)
            {
                _player.HitAccepted -= RecordHit;
            }
            if (_driver != null)
            {
                _driver.GameStarting -= ResetEffect;
            }
            ResetEffect();
        }

        private void LateUpdate()
        {
            if (_hasHit == false)
            {
                return;
            }
            if (_player == null || _driver == null || _player.isActiveAndEnabled == false ||
                _driver.isActiveAndEnabled == false || _driver.CurrentState != GameState.PlayerDead)
            {
                ResetEffect();
                return;
            }
            int frame = (int)(System.Math.Max(0.0, Time.timeAsDouble - _startedAt) / FrameInterval) % _frames.Length;
            _renderer.sprite = _frames[frame];
            _renderer.enabled = true;
        }

        private void RecordHit()
        {
            if (_driver.isActiveAndEnabled == false ||
                (_driver.CurrentState != GameState.Playing && _driver.CurrentState != GameState.PlayerDead))
            {
                return;
            }
            // HitAccepted subscriber order is not assumed: Driver may enter PlayerDead after this call.
            Vector2 position = _player.Position;
            _renderer.transform.position = new Vector3(position.x, position.y, -0.1f);
            _startedAt = Time.timeAsDouble;
            _hasHit = true;
        }

        private void ResetEffect()
        {
            _hasHit = false;
            if (_renderer != null)
            {
                _renderer.enabled = false;
            }
        }
    }
}
