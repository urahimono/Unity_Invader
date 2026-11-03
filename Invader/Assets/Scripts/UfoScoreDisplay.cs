using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace UnityInvader
{
    // Presentation only: pairs the synchronous hit position with the Driver's actual award.
    [DisallowMultipleComponent]
    public sealed class UfoScoreDisplay : MonoBehaviour
    {
        [SerializeField]
        private GameplayDriver _driver;
        [SerializeField]
        private Ufo            _ufo;
        [SerializeField]
        private Text           _text;
        [SerializeField, Min(0.01f)]
        private float          _duration = 0.8f;

        private Vector3                 _pendingPosition;
        private int                     _pendingFrame = -1;
        private CancellationTokenSource _displayCancellation;

        private void OnEnable()
        {
            ClearDisplay();
            if (_driver == null || _ufo == null || _text == null ||
                float.IsFinite(_duration) == false || _duration <= 0f)
            {
                Debug.LogError("UFO score display requires Driver, UFO, Text and a positive duration.", this);
                enabled = false;
                return;
            }
            _ufo.Defeated += RecordPosition;
            _driver.UfoScoreAwarded += ShowAward;
            _driver.GameStarting += ClearDisplay;
        }

        private void OnDisable()
        {
            if (_ufo != null)
            {
                _ufo.Defeated -= RecordPosition;
            }
            if (_driver != null)
            {
                _driver.UfoScoreAwarded -= ShowAward;
                _driver.GameStarting -= ClearDisplay;
            }
            ClearDisplay();
        }

        private void LateUpdate()
        {
            // Both normal and B shots resolve synchronously before LateUpdate.
            // An unawarded defeat must never supply the position for a later award.
            _pendingFrame = -1;
        }

        private void RecordPosition(Vector3 inPosition)
        {
            _pendingPosition = inPosition;
            _pendingFrame    = Time.frameCount;
        }

        private void ShowAward(int inPoints)
        {
            bool hasPosition = _pendingFrame == Time.frameCount;
            _pendingFrame = -1;
            if (hasPosition == false || inPoints <= 0)
            {
                return;
            }
            ClearDisplay();
            Vector3 position = _pendingPosition;
            position.z = _text.rectTransform.position.z;
            _text.rectTransform.position = position;
            _text.text                   = inPoints.ToString();
            _text.enabled                = true;
            _displayCancellation         = new CancellationTokenSource();
            HideAfterDelay(_displayCancellation.Token).Forget(Debug.LogException);
        }

        private async UniTask HideAfterDelay(CancellationToken inToken)
        {
            float remaining = _duration;
            while (remaining > 0f)
            {
                bool cancelled = await UniTask.NextFrame(PlayerLoopTiming.Update, inToken).SuppressCancellationThrow();
                if (cancelled)
                {
                    return;
                }
                // Scaled game time; Driver Disable is the existing gameplay pause operation.
                // Editor Pause stops the PlayerLoop itself.
                if (_driver != null && _driver.isActiveAndEnabled)
                {
                    remaining -= Time.deltaTime;
                }
            }
            _text.enabled = false;
        }

        private void ClearDisplay()
        {
            _pendingFrame = -1;
            _displayCancellation?.Cancel();
            _displayCancellation?.Dispose();
            _displayCancellation = null;
            if (_text != null)
            {
                _text.enabled = false;
                _text.text    = string.Empty;
            }
        }
    }
}
