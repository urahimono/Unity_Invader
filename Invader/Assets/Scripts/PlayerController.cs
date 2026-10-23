using UnityEngine;
using UnityEngine.Serialization;

namespace UnityInvader
{
    public sealed class PlayerController : MonoBehaviour
    {
        [FormerlySerializedAs("gameState")]
        [SerializeField]
        private GameStateController _gameState;
        [FormerlySerializedAs("playerRenderer")]
        [SerializeField]
        private SpriteRenderer      _playerRenderer;
        private PlayingSimulation Simulation => _gameState.Playing;
        private SpriteRenderer _shotRenderer;
        private Texture2D      _shotTexture;
        private Sprite         _shotSprite;

        public PlayerModel Player => Simulation.Player;
        public PlayerShot Shot => Simulation.Shot;

        private void OnEnable()
        {
            if (_shotRenderer == null)
            {
                var shotObject = new GameObject("PlayerShot");
                shotObject.transform.SetParent(transform, false);
                _shotRenderer = shotObject.AddComponent<SpriteRenderer>();
                _shotTexture = new Texture2D(PlayerShot.Width, PlayerShot.Height) { filterMode = FilterMode.Point };
                _shotTexture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
                _shotTexture.Apply();
                _shotSprite = Sprite.Create(_shotTexture, new Rect(0, 0, PlayerShot.Width, PlayerShot.Height), new Vector2(0.5f, 0f), 1);
                _shotRenderer.sprite = _shotSprite;
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
            _playerRenderer.enabled = _gameState.CurrentState == GameState.Playing;
            _shotRenderer.transform.position = new Vector3(Shot.X - PlayerModel.ScreenWidth / 2f,
                Shot.Y - PlayerModel.ScreenHeight / 2f, 0f);
            _shotRenderer.enabled = _gameState.CurrentState == GameState.Playing && Shot.Active;
        }

        private void OnDisable()
        {
            _playerRenderer.enabled = false;
            if (_shotRenderer != null)
            {
                _shotRenderer.enabled = false;
            }
        }

        private void OnDestroy()
        {
            if (_shotSprite != null)
            {
                Destroy(_shotSprite);
            }
            if (_shotTexture != null)
            {
                Destroy(_shotTexture);
            }
        }
    }
}
