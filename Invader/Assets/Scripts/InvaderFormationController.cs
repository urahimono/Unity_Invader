using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace UnityInvader
{
    public sealed class InvaderFormationController : MonoBehaviour
    {
        // Top A/B, Middle A/B, Bottom A/B.
        [FormerlySerializedAs("frames")]
        [SerializeField]
        private Sprite[] _frames           = new Sprite[6];
        [FormerlySerializedAs("spriteMaterial")]
        [SerializeField]
        private Material _spriteMaterial;
        // Preserve the source aspect ratio with the same unit scale for every frame.
        [FormerlySerializedAs("cellDisplayScale")]
        [SerializeField]
        private Vector2  _cellDisplayScale = Vector2.one;

        private SpriteRenderer[]      _renderers;
        [System.NonSerialized]
        private InvaderDisplayState[] _display;

        private void OnEnable()
        {
            ApplyCachedDisplay();
        }

        private void OnDisable()
        {
            HideRenderers();
        }

        public void ApplyDisplay(ReadOnlySpan<InvaderDisplayState> inDisplay)
        {
            if (_renderers == null || _renderers.Length == 0)
            {
                _renderers  = new SpriteRenderer[inDisplay.Length];
                for (int id = 0; id < inDisplay.Length; id++)
                {
                    var state    = inDisplay[id];
                    var instance = new GameObject($"Invader {state.Id} ({state.Row},{state.Column})");
                    instance.transform.SetParent(transform, false);
                    instance.transform.localScale = new Vector3(_cellDisplayScale.x, _cellDisplayScale.y, 1f);
                    var renderer                  = instance.AddComponent<SpriteRenderer>();
                    renderer.sharedMaterial       = _spriteMaterial;
                    _renderers[id]                = renderer;
                }
            }
            if (_display == null || _display.Length != inDisplay.Length)
            {
                _display = new InvaderDisplayState[inDisplay.Length];
            }
            inDisplay.CopyTo(_display);
            ApplyCachedDisplay();
        }

        private void HideRenderers()
        {
            if (_renderers == null)
            {
                return;
            }
            foreach (var renderer in _renderers)
            {
                renderer.enabled = false;
            }
        }

        private void ApplyCachedDisplay()
        {
            if (_renderers == null || _display == null || _display.Length != _renderers.Length)
            {
                return;
            }
            for (int id = 0; id < _renderers.Length; id++)
            {
                var state                   = _display[id];
                var renderer                = _renderers[id];
                renderer.enabled            = isActiveAndEnabled && state.Visible;
                renderer.sprite             = _frames[(int)state.Type * 2 + state.AnimationFrame];
                renderer.transform.position = new Vector3(state.DisplayX, state.DisplayY, 0f);
            }
        }
    }
}
