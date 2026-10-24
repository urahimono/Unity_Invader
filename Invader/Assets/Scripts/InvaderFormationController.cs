using UnityEngine;
using UnityEngine.Serialization;

namespace UnityInvader
{
    public sealed class InvaderFormationController : MonoBehaviour
    {
        [FormerlySerializedAs("gameState")]
        [SerializeField]
        private GameStateController _gameState;
        // Top A/B, Middle A/B, Bottom A/B.
        [FormerlySerializedAs("frames")]
        [SerializeField]
        private Sprite[]            _frames           = new Sprite[6];
        [FormerlySerializedAs("spriteMaterial")]
        [SerializeField]
        private Material            _spriteMaterial;
        // Preserve the source aspect ratio with the same unit scale for every frame.
        [FormerlySerializedAs("cellDisplayScale")]
        [SerializeField]
        private Vector2             _cellDisplayScale = Vector2.one;
        private SpriteRenderer[]    _renderers;

        public InvaderFormation Formation => _gameState.Playing.Formation;

        private void Awake()
        {
            _renderers = new SpriteRenderer[Formation.Invaders.Count];
            foreach (var slot in Formation.Invaders)
            {
                var instance = new GameObject($"Invader {slot.Id} ({slot.Row},{slot.Column})");
                instance.transform.SetParent(transform, false);
                instance.transform.localScale = new Vector3(_cellDisplayScale.x, _cellDisplayScale.y, 1f);
                var renderer                  = instance.AddComponent<SpriteRenderer>();
                renderer.sharedMaterial       = _spriteMaterial;
                _renderers[slot.Id]           = renderer;
            }
            ApplyDisplay();
        }

        // GameplayDriver advances the shared ArcadeGameplaySimulation in Update.
        private void LateUpdate()
        {
            ApplyDisplay();
        }

        private void ApplyDisplay()
        {
            bool playing = _gameState.CurrentState == GameState.Playing;
            foreach (var slot in Formation.Invaders)
            {
                var renderer                = _renderers[slot.Id];
                renderer.enabled            = playing && slot.Alive;
                renderer.sprite             = _frames[(int)slot.Type * 2 + Formation.AnimationFrame];
                // Transform is output only. No game rule reads display positions.
                renderer.transform.position = new Vector3(
                    Formation.X + slot.Column * InvaderFormation.Spacing - PlayerBody.ScreenWidth / 2f,
                    Formation.Y - slot.Row * InvaderFormation.Spacing - PlayerBody.ScreenHeight / 2f, 0f);
            }
        }

        private void OnDisable()
        {
            if (_renderers == null)
            {
                return;
            }
            foreach (var renderer in _renderers)
            {
                renderer.enabled = false;
            }
        }
    }
}
