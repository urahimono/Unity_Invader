using UnityEngine;

namespace UnityInvader
{
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private GameStateController gameState;
        [SerializeField] private SpriteRenderer playerRenderer;
        private PlayingSimulation Simulation => gameState.Playing;
        private SpriteRenderer shotRenderer;
        private Texture2D shotTexture;
        private Sprite shotSprite;

        public PlayerModel Player => Simulation.Player;
        public PlayerShot Shot => Simulation.Shot;

        private void OnEnable()
        {
            if (shotRenderer == null)
            {
                var shotObject = new GameObject("PlayerShot");
                shotObject.transform.SetParent(transform, false);
                shotRenderer = shotObject.AddComponent<SpriteRenderer>();
                shotTexture = new Texture2D(PlayerShot.Width, PlayerShot.Height) { filterMode = FilterMode.Point };
                shotTexture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
                shotTexture.Apply();
                shotSprite = Sprite.Create(shotTexture, new Rect(0, 0, PlayerShot.Width, PlayerShot.Height), new Vector2(0.5f, 0f), 1);
                shotRenderer.sprite = shotSprite;
            }
            ApplyDisplay();
        }

        private void LateUpdate()
        {
            ApplyDisplay();
        }

        private void ApplyDisplay()
        {
            // Logical origin is bottom-left; Unity origin is the screen center.
            transform.position = new Vector3(Player.X - PlayerModel.ScreenWidth / 2f,
                Player.Y - PlayerModel.ScreenHeight / 2f, 0f);
            playerRenderer.enabled = gameState.CurrentState == GameState.Playing;
            shotRenderer.transform.position = new Vector3(Shot.X - PlayerModel.ScreenWidth / 2f,
                Shot.Y - PlayerModel.ScreenHeight / 2f, 0f);
            shotRenderer.enabled = gameState.CurrentState == GameState.Playing && Shot.Active;
        }

        private void OnDisable()
        {
            playerRenderer.enabled = false;
            if (shotRenderer != null) shotRenderer.enabled = false;
        }

        private void OnDestroy()
        {
            if (shotSprite != null) Destroy(shotSprite);
            if (shotTexture != null) Destroy(shotTexture);
        }
    }
}
