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

        public ArcadeGameplaySimulation Playing { get; } = new ArcadeGameplaySimulation();

        public void ChangeState(GameState inNextState)
        {
            _currentState = inNextState;
        }
    }
}
