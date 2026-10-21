using UnityEngine;

namespace UnityInvader
{
    [RequireComponent(typeof(Camera))]
    public sealed class ArcadeViewport : MonoBehaviour
    {
        private Camera arcadeCamera;

        private void OnEnable()
        {
            arcadeCamera = GetComponent<Camera>();
            Update();
        }

        private void Update()
        {
            if (Screen.width <= 0 || Screen.height <= 0) return;
            float targetAspect = (float)PlayerModel.ScreenWidth / PlayerModel.ScreenHeight;
            float screenAspect = (float)Screen.width / Screen.height;
            if (screenAspect > targetAspect)
            {
                float width = targetAspect / screenAspect;
                arcadeCamera.rect = new Rect((1f - width) / 2f, 0f, width, 1f);
            }
            else
            {
                float height = screenAspect / targetAspect;
                arcadeCamera.rect = new Rect(0f, (1f - height) / 2f, 1f, height);
            }
        }
    }
}
