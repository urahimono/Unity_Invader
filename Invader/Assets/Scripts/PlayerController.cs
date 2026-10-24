using UnityEngine;
using UnityEngine.Serialization;

namespace UnityInvader
{
    public sealed class PlayerController : MonoBehaviour
    {
        [FormerlySerializedAs("playerRenderer")]
        [SerializeField]
        private SpriteRenderer _playerRenderer;

        private SpriteRenderer     _shotRenderer;
        private Texture2D          _shotTexture;
        private Sprite             _shotSprite;
        [System.NonSerialized]
        private PlayerDisplayState _display;
        [System.NonSerialized]
        private bool               _hasDisplay;

        private void OnEnable()
        {
            ApplyCachedDisplay();
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

        public void ApplyDisplay(PlayerDisplayState inDisplay)
        {
            _display    = inDisplay;
            _hasDisplay = true;
            if (_shotRenderer == null)
            {
                var shotObject = new GameObject("PlayerShot");
                shotObject.transform.SetParent(transform, false);
                _shotRenderer = shotObject.AddComponent<SpriteRenderer>();
                _shotTexture  = new Texture2D(inDisplay.ShotWidth, inDisplay.ShotHeight) { filterMode = FilterMode.Point };
                _shotTexture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
                _shotTexture.Apply();
                _shotSprite          = Sprite.Create(_shotTexture, new Rect(0, 0, inDisplay.ShotWidth, inDisplay.ShotHeight), new Vector2(0.5f, 0f), 1);
                _shotRenderer.sprite = _shotSprite;
            }
            ApplyCachedDisplay();
        }

        private void ApplyCachedDisplay()
        {
            if (_hasDisplay == false)
            {
                return;
            }
            transform.position               = new Vector3(_display.X, _display.Y, 0f);
            _playerRenderer.enabled          = isActiveAndEnabled && _display.Visible;
            _shotRenderer.transform.position = new Vector3(_display.ShotX, _display.ShotY, 0f);
            _shotRenderer.enabled            = isActiveAndEnabled && _display.ShotVisible;
        }
    }
}
