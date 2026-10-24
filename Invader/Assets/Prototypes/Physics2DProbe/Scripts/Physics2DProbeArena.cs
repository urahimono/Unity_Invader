using System.Collections.Generic;
using UnityEngine;

namespace UnityInvader.Prototypes.Physics2DProbe
{
    public sealed class Physics2DProbeArena : MonoBehaviour
    {
        [SerializeField]
        private Physics2DProbeFormation _singleFormation;
        [SerializeField]
        private Physics2DProbeFormation _sharedFormation;
        [SerializeField]
        private Physics2DProbeShot      _shotPrefab;
        [SerializeField]
        private Transform              _singleMuzzle;
        [SerializeField]
        private Transform              _sharedMuzzle;

        private readonly List<Physics2DProbeShot> _shots = new List<Physics2DProbeShot>();

        private void Start()
        {
            ResetArena();
        }

        private void OnGUI()
        {
            DrawProbeControls();
        }

        private void Fire(Transform inMuzzle)
        {
            _shots.RemoveAll(inShot => inShot == null);
            var shot = Instantiate(_shotPrefab, inMuzzle.position, Quaternion.identity);
            _shots.Add(shot);
        }

        private void ResetArena()
        {
            foreach (var shot in _shots)
            {
                if (shot != null)
                {
                    shot.TryConsume();
                }
            }
            _shots.Clear();
            _singleFormation.ResetProbe();
            _sharedFormation.ResetProbe();
        }

        private void DrawProbeControls()
        {
            GUILayout.BeginArea(new Rect(12f, 12f, 510f, 210f), GUI.skin.box);
            GUILayout.Label("Physics2D probe (independent of Arcade)");
            GUILayout.Label($"LEFT single: alive {_singleFormation.AliveCount}/1, notifications {_singleFormation.DefeatNotifications}");
            GUILayout.Label($"RIGHT shared edge: alive {_sharedFormation.AliveCount}/2, notifications {_sharedFormation.DefeatNotifications}");
            if (GUILayout.Button("Fire LEFT: one target"))
            {
                Fire(_singleMuzzle);
            }
            if (GUILayout.Button("Fire RIGHT: touch two targets with one shot"))
            {
                Fire(_sharedMuzzle);
            }
            if (GUILayout.Button("Reset both cases and remove shots"))
            {
                ResetArena();
            }
            GUILayout.Label("One shot: at most one defeat. Target order is unspecified.");
            GUILayout.EndArea();
        }
    }
}
