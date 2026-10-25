using UnityEngine;

namespace UnityInvader
{
    // Development-only status display; never controls gameplay or keeps a state copy.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GameplayDriver))]
    public sealed class DebugStatusOverlay : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private GameplayDriver _driver;
        private GUIStyle       _style;

        private void Awake()
        {
            _driver = GetComponent<GameplayDriver>();
        }

        private void OnGUI()
        {
            if (_driver == null || Event.current.type != EventType.Repaint)
            {
                return;
            }
            DrawStatus();
        }

        private void DrawStatus()
        {
            if (_style == null)
            {
                _style                  = new GUIStyle(GUI.skin.label);
                _style.fontSize         = 16;
                _style.normal.textColor = Color.white;
            }
            var previousMatrix = GUI.matrix;
            var previousColor  = GUI.color;
            float scale        = Mathf.Min(1f, Screen.width / 208f, Screen.height / 108f);
            try
            {
                GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
                GUI.color  = Color.black;
                GUI.DrawTexture(new Rect(8f, 8f, 192f, 92f), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(new Rect(16f, 10f, 180f, 20f), "DEBUG", _style);
                GUI.Label(new Rect(16f, 30f, 180f, 66f), GetStatusText(), _style);
            }
            finally
            {
                GUI.matrix = previousMatrix;
                GUI.color  = previousColor;
            }
        }

        private string GetStatusText()
        {
            return $"State : {_driver.CurrentState}\nLives : {_driver.Lives}\nWave : {_driver.WaveNumber}";
        }
#endif
    }
}
