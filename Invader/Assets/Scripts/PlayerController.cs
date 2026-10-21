using UnityEngine;

namespace UnityInvader
{
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private GameStateController gameState;
        [SerializeField] private SpriteRenderer playerRenderer;
        [Tooltip("Temporary path for Issue #4; real game-start transitions are not implemented.")]
        [SerializeField] private bool developmentStartPlaying;

        private PlayerInputReader input;
        private PlayerSimulation simulation;
        private SpriteRenderer shotRenderer;
        private Texture2D shotTexture;
        private Sprite shotSprite;

        public PlayerModel Player => simulation.Player;
        public PlayerShot Shot => simulation.Shot;

        private void OnEnable()
        {
            if (simulation == null) simulation = new PlayerSimulation();
            input = new PlayerInputReader();
            if (shotRenderer == null)
            {
                var shotObject = new GameObject("PlayerShot");
                shotObject.transform.SetParent(transform, false);
                shotRenderer = shotObject.AddComponent<SpriteRenderer>();
                shotTexture = new Texture2D(1, 4) { filterMode = FilterMode.Point };
                shotTexture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
                shotTexture.Apply();
                shotSprite = Sprite.Create(shotTexture, new Rect(0, 0, 1, 4), new Vector2(0.5f, 0f), 1);
                shotRenderer.sprite = shotSprite;
            }
            ApplyDisplay();
        }

        private void Start()
        {
            if (developmentStartPlaying)
            {
                gameState.ChangeState(GameState.Playing);
            }
        }

        private void Update()
        {
            bool playing = gameState.CurrentState == GameState.Playing;
            simulation.Advance(Time.deltaTime, playing, input.Read());
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
            input?.Dispose();
            input = null;
            if (shotRenderer != null) shotRenderer.enabled = false;
        }

        private void OnDestroy()
        {
            if (shotSprite != null) Destroy(shotSprite);
            if (shotTexture != null) Destroy(shotTexture);
        }
    }
}
