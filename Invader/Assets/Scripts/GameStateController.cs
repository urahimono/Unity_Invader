using UnityEngine;

namespace UnityInvader
{
    public sealed class GameStateController : MonoBehaviour
    {
        // Initial placeholder only; state rules will be implemented in later issues.
        [SerializeField] private GameState currentState = GameState.Attract;

        public GameState CurrentState => currentState;

        public void ChangeState(GameState nextState)
        {
            currentState = nextState;
        }
    }
}
