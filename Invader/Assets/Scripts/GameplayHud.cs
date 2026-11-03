using UnityEngine;
using UnityEngine.UI;

namespace UnityInvader
{
    // Reads presentation values only; GameplayDriver remains the owner of game state.
    [DisallowMultipleComponent]
    public sealed class GameplayHud : MonoBehaviour
    {
        [SerializeField]
        private GameplayDriver _driver;
        [SerializeField]
        private Text           _score;
        [SerializeField]
        private Text           _lives;
        [SerializeField]
        private Text           _gameOver;

        private void OnEnable()
        {
            if (_driver == null || _score == null || _lives == null || _gameOver == null)
            {
                Debug.LogError("Gameplay HUD requires Driver, Score, Lives and Game Over text references.", this);
                enabled = false;
                return;
            }
            RefreshDisplay();
        }

        private void LateUpdate()
        {
            RefreshDisplay();
        }

        private void RefreshDisplay()
        {
            _score.text       = $"SCORE {_driver.Score}";
            _lives.text       = $"LIVES {_driver.Lives}";
            _gameOver.enabled = _driver.CurrentState == GameState.GameOver;
        }
    }
}
