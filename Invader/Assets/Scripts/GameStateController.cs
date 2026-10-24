using UnityEngine;
using UnityEngine.Serialization;

namespace UnityInvader
{
    public sealed class GameStateController : MonoBehaviour
    {
        // Initial placeholder only; state rules will be implemented in later issues.
        [FormerlySerializedAs("currentState")]
        [SerializeField]
        private GameState _currentState = GameState.Attract;

        public GameState CurrentState => _currentState;

        public PlayingSimulation Playing { get; } = new PlayingSimulation();

        public void ChangeState(GameState nextState)
        {
            _currentState = nextState;
        }
    }
}
