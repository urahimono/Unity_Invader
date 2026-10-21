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

        public PlayerModel Player => simulation.Player;

        private void OnEnable()
        {
            if (simulation == null) simulation = new PlayerSimulation();
            input = new PlayerInputReader();
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
        }

        private void OnDisable()
        {
            input?.Dispose();
            input = null;
        }
    }
}
