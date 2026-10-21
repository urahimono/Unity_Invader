using UnityEngine;

namespace UnityInvader
{
    // Unity-side input and clock for this game's shared Playing simulation.
    [DisallowMultipleComponent]
    public sealed class PlayingController : MonoBehaviour
    {
        [SerializeField] private GameStateController gameState;
        [Tooltip("Temporary development start path; real game-start transitions are not implemented.")]
        [SerializeField] private bool developmentStartPlaying;
        private PlayerInputReader input;

        private void OnEnable()
        {
            input = new PlayerInputReader();
        }

        private void Start()
        {
            if (developmentStartPlaying) gameState.ChangeState(GameState.Playing);
        }

        private void Update()
        {
            bool playing = gameState.CurrentState == GameState.Playing;
            // The only runtime driver; both views synchronize afterward in LateUpdate.
            gameState.Playing.Advance(Time.deltaTime, playing, playing ? input.Read() : default);
        }

        private void OnDisable()
        {
            gameState.Playing.Advance(0, false, default);
            input?.Dispose();
            input = null;
        }
    }
}
