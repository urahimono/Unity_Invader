using System;
using UnityEngine;

namespace UnityInvader
{
    internal sealed class EnemyShotController
    {
        private bool _rollingAttemptReady;
        private int  _plungerColumnIndex;
        private int  _squigglyColumnIndex;

        internal bool IsSquigglyInFlight => _shots[Squiggly].InFlight;

        private readonly EnemyShot[]         _shots;
        private readonly Func<int, Vector2?> _getColumnMuzzle;
        private readonly Func<int>           _getScore;
        private readonly int                 _columns;
        private readonly float               _spacing;

        private const int Rolling  = 0;
        private const int Plunger  = 1;
        private const int Squiggly = 2;

        // ROM ColFireTable 0x1D00, indices 0x00..0x0F; one-based columns.
        private static readonly int[] _plungerColumns  = { 1, 7, 1, 1, 1, 4, 11, 1, 6, 3, 1, 1, 11, 9, 2, 8 };
        // ColFireTable indices 0x06..0x14, inclusive.
        private static readonly int[] _squigglyColumns = { 11, 1, 6, 3, 1, 1, 11, 9, 2, 8, 2, 11, 4, 7, 10 };
        private static readonly int[] _reloadScoreMsb  = { 0x02, 0x10, 0x20, 0x30 };
        private static readonly int[] _reloadRates     = { 0x30, 0x10, 0x0B, 0x08, 0x07 };

        internal EnemyShotController(EnemyShot inRolling, EnemyShot inPlunger, EnemyShot inSquiggly,
            Func<int, Vector2?> inGetColumnMuzzle, Func<int> inGetScore, int inColumns, float inSpacing)
        {
            if (inRolling == null || inPlunger == null || inSquiggly == null ||
                inRolling == inPlunger || inRolling == inSquiggly || inPlunger == inSquiggly)
            {
                throw new ArgumentException("Three independent enemy shots are required.");
            }
            _shots           = new[] { inRolling, inPlunger, inSquiggly };
            _getColumnMuzzle = inGetColumnMuzzle ?? throw new ArgumentNullException(nameof(inGetColumnMuzzle));
            _getScore        = inGetScore ?? throw new ArgumentNullException(nameof(inGetScore));
            _columns         = inColumns;
            _spacing         = inSpacing;
        }

        internal void PreparePhysicsStep()
        {
            foreach (var shot in _shots)
            {
                shot.PreparePhysicsStep();
            }
        }

        internal void SetPlaying(bool inPlaying)
        {
            foreach (var shot in _shots)
            {
                shot.SetPlaying(inPlaying);
            }
        }

        internal void SetFast(bool inFast)
        {
            foreach (var shot in _shots)
            {
                shot.SetFast(inFast);
            }
        }

        internal void BeginWave()
        {
            _plungerColumnIndex  = 0; // Provisional: restart each Wave; preserve on death.
            _squigglyColumnIndex = 0;
            ResetFlights();
        }

        internal void ResetFlights()
        {
            foreach (var shot in _shots)
            {
                shot.ResetFlight();
            }
            _rollingAttemptReady = false;
        }

        internal void ProcessRolling(Vector2 inPlayerPosition, Vector2 inFormationPosition)
        {
            ProcessShot(Rolling, 0, inPlayerPosition, inFormationPosition);
        }

        internal void ProcessPlunger(int inAliveCount)
        {
            ProcessShot(Plunger, inAliveCount, default, default);
        }

        internal void ProcessSquiggly()
        {
            ProcessShot(Squiggly, 0, default, default);
        }

        private void ProcessShot(int inType, int inAliveCount, Vector2 inPlayerPosition, Vector2 inFormationPosition)
        {
            var shot = _shots[inType];
            if (shot.InFlight)
            {
                shot.AdvanceStep();
                return;
            }
            if (inType == Rolling && _rollingAttemptReady == false)
            {
                // ROM 047D..0489: skip the first idle attempt after slot reinitialization.
                _rollingAttemptReady = true;
                return;
            }
            if (inType == Plunger && inAliveCount <= 1)
            {
                return;
            }
            int reload = GetReloadRate(_getScore());
            if (CanReload(inType, reload) == false)
            {
                return;
            }
            Vector2? position;
            switch (inType)
            {
                case Rolling:
                    position = GetRollingMuzzle(inPlayerPosition, inFormationPosition);
                    break;
                case Plunger:
                    position = GetTableMuzzle(_plungerColumns, ref _plungerColumnIndex);
                    break;
                default:
                    position = GetTableMuzzle(_squigglyColumns, ref _squigglyColumnIndex);
                    break;
            }
            if (position.HasValue && shot.TryLaunch(position.Value) && inType == Rolling)
            {
                _rollingAttemptReady = false;
            }
        }

        private bool CanReload(int inType, int inReload)
        {
            for (int index = 0; index < _shots.Length; index++)
            {
                if (index != inType && _shots[index].StepCount != 0 && _shots[index].StepCount <= inReload)
                {
                    return false;
                }
            }
            return true;
        }

        private Vector2? GetTableMuzzle(int[] inColumns, ref int inOutIndex)
        {
            int column = inColumns[inOutIndex] - 1;
            inOutIndex = (inOutIndex + 1) % inColumns.Length;
            return _getColumnMuzzle(column);
        }

        private Vector2? GetRollingMuzzle(Vector2 inPlayerPosition, Vector2 inFormationPosition)
        {
            int column = Mathf.FloorToInt((inPlayerPosition.x - inFormationPosition.x) / _spacing + 0.5f);
            if (column < 0 || column >= _columns)
            {
                return null; // Do not clamp to an edge column or choose a different one.
            }
            return _getColumnMuzzle(column);
        }

        private static int GetReloadRate(int inScore)
        {
            // 170E..1728 compares only the high BCD byte, not the full score.
            // Keep the existing unbounded Unity Score; beyond 9999 retain fastest reload.
            int hundreds = Mathf.Clamp(inScore / 100, 0, 99);
            int scoreMsb = ((hundreds / 10) << 4) | (hundreds % 10);
            for (int index = 0; index < _reloadScoreMsb.Length; index++)
            {
                if (scoreMsb <= _reloadScoreMsb[index])
                {
                    return _reloadRates[index];
                }
            }
            return _reloadRates[4];
        }
    }
}
