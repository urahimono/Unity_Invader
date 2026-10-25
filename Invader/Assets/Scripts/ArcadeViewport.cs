using UnityEngine;

namespace UnityInvader
{
    [RequireComponent(typeof(Camera))]
    public sealed class ArcadeViewport : MonoBehaviour
    {
        private Camera _arcadeCamera;

        public const int   ScreenWidth   = 224;
        public const int   ScreenHeight  = 256;
        public const float UnitsPerPixel = 0.01f;

        private void OnEnable()
        {
            _arcadeCamera = GetComponent<Camera>();
            Update();
        }

        private void Update()
        {
            UpdateCameraRect();
        }

        private void UpdateCameraRect()
        {
            if (Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }
            float targetAspect = (float)ScreenWidth / ScreenHeight;
            float screenAspect = (float)Screen.width / Screen.height;
            if (screenAspect > targetAspect)
            {
                float width        = targetAspect / screenAspect;
                _arcadeCamera.rect = new Rect((1f - width) / 2f, 0f, width, 1f);
            }
            else
            {
                float height       = screenAspect / targetAspect;
                _arcadeCamera.rect = new Rect(0f, (1f - height) / 2f, 1f, height);
            }
        }
    }
}
