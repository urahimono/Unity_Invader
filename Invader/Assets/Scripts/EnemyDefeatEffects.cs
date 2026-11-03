using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace UnityInvader
{
    // Presentation only: slots outlive defeated objects and WaveClear.
    [DisallowMultipleComponent]
    public sealed class EnemyDefeatEffects : MonoBehaviour
    {
        [SerializeField]
        private GameplayDriver   _driver;
        [SerializeField]
        private Ufo              _ufo;
        [SerializeField]
        private SpriteRenderer[] _slots;
        [SerializeField]
        private Sprite           _invaderSprite;
        [SerializeField]
        private Sprite           _ufoSprite;
        [SerializeField, Min(0.01f)]
        private float            _duration = 0.25f;
        [SerializeField]
        private Color            _invaderColor = Color.white;
        [SerializeField]
        private Color            _ufoColor = Color.red;

        private readonly List<Invader> _invaders = new List<Invader>();
        private CancellationTokenSource _lifetime;

        private void OnEnable()
        {
            if (_driver == null || _ufo == null || _invaderSprite == null || _ufoSprite == null ||
                _slots == null || _slots.Length != InvaderFormation.Rows * InvaderFormation.Columns + 1 ||
                Array.Exists(_slots, inSlot => inSlot == null) || float.IsFinite(_duration) == false || _duration <= 0f)
            {
                Debug.LogError("Enemy defeat effects require Driver, UFO, sprites, 56 renderers and a positive duration.", this);
                enabled = false;
                return;
            }
            ResetEffects();
            _driver.GameStarting += ResetEffects;
            _ufo.Defeated += ShowUfo;
            foreach (var invader in _invaders)
            {
                if (invader != null)
                {
                    invader.Defeated += ShowInvader;
                }
            }
        }

        private void OnDisable()
        {
            if (_driver != null)
            {
                _driver.GameStarting -= ResetEffects;
            }
            if (_ufo != null)
            {
                _ufo.Defeated -= ShowUfo;
            }
            foreach (var invader in _invaders)
            {
                if (invader != null)
                {
                    invader.Defeated -= ShowInvader;
                }
            }
            ClearEffects();
        }

        internal void Track(Invader inInvader)
        {
            _invaders.Add(inInvader);
            if (_lifetime != null && isActiveAndEnabled)
            {
                inInvader.Defeated += ShowInvader;
            }
        }

        private void ShowInvader(Invader inInvader, Component inSource)
        {
            Show(inInvader.transform.position, _invaderSprite, _invaderColor);
        }

        private void ShowUfo(Vector3 inPosition)
        {
            Show(inPosition, _ufoSprite, _ufoColor);
        }

        private void Show(Vector3 inPosition, Sprite inSprite, Color inColor)
        {
            if (_lifetime == null || isActiveAndEnabled == false)
            {
                return;
            }
            foreach (var slot in _slots)
            {
                if (slot.enabled)
                {
                    continue;
                }
                slot.transform.position = new Vector3(inPosition.x, inPosition.y, -0.1f);
                slot.sprite             = inSprite;
                slot.color              = inColor;
                slot.enabled            = true;
                HideAfterDelay(slot, _lifetime.Token).Forget(Debug.LogException);
                return;
            }
            Debug.LogWarning("Enemy defeat effect slots are occupied; skip this visual only.", this);
        }

        private async UniTask HideAfterDelay(SpriteRenderer inSlot, CancellationToken inToken)
        {
            // Scaled game time, Update PlayerLoop. TimeScale=0 and Editor Pause freeze the effect.
            bool cancelled = await UniTask.Delay(TimeSpan.FromSeconds(_duration), DelayType.DeltaTime,
                PlayerLoopTiming.Update, inToken).SuppressCancellationThrow();
            if (cancelled == false)
            {
                inSlot.enabled = false;
            }
        }

        private void ResetEffects()
        {
            ClearEffects();
            _lifetime = new CancellationTokenSource();
        }

        private void ClearEffects()
        {
            _lifetime?.Cancel();
            _lifetime?.Dispose();
            _lifetime = null;
            if (_slots == null)
            {
                return;
            }
            foreach (var slot in _slots)
            {
                if (slot != null)
                {
                    slot.enabled = false;
                }
            }
        }
    }
}
