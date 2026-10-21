using UnityEngine;

namespace UnityInvader
{
    public sealed class InvaderFormationController : MonoBehaviour
    {
        [SerializeField] private GameStateController gameState;
        // Top A/B, Middle A/B, Bottom A/B.
        [SerializeField] private Sprite[] frames = new Sprite[6];
        [SerializeField] private Material spriteMaterial;
        // Preserve the source aspect ratio with the same unit scale for every frame.
        [SerializeField] private Vector2 cellDisplayScale = Vector2.one;
        private SpriteRenderer[] renderers;

        public InvaderFormation Formation => gameState.Playing.Formation;

        private void Awake()
        {
            renderers = new SpriteRenderer[Formation.Invaders.Count];
            foreach (var slot in Formation.Invaders)
            {
                var instance = new GameObject($"Invader {slot.Id} ({slot.Row},{slot.Column})");
                instance.transform.SetParent(transform, false);
                instance.transform.localScale = new Vector3(cellDisplayScale.x, cellDisplayScale.y, 1f);
                var renderer = instance.AddComponent<SpriteRenderer>();
                renderer.sharedMaterial = spriteMaterial;
                renderers[slot.Id] = renderer;
            }
            ApplyDisplay();
        }

        // PlayerController advances the shared PlayingSimulation in Update.
        private void LateUpdate()
        {
            ApplyDisplay();
        }

        private void ApplyDisplay()
        {
            bool playing = gameState.CurrentState == GameState.Playing;
            foreach (var slot in Formation.Invaders)
            {
                var renderer = renderers[slot.Id];
                renderer.enabled = playing && slot.Alive;
                renderer.sprite = frames[(int)slot.Type * 2 + Formation.AnimationFrame];
                // Transform is output only. No game rule reads display positions.
                renderer.transform.position = new Vector3(
                    Formation.X + slot.Column * InvaderFormation.Spacing - PlayerModel.ScreenWidth / 2f,
                    Formation.Y - slot.Row * InvaderFormation.Spacing - PlayerModel.ScreenHeight / 2f, 0f);
            }
        }

        private void OnDisable()
        {
            if (renderers == null) return;
            foreach (var renderer in renderers) renderer.enabled = false;
        }
    }
}
