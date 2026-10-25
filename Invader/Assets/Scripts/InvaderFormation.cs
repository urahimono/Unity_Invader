using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityInvader
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class InvaderFormation : MonoBehaviour
    {
        [SerializeField]
        private Invader  _invaderPrefab;
        [SerializeField]
        private Sprite[] _frames = new Sprite[6];
        [SerializeField]
        private int      _aliveCount;

        [SerializeField]
        private Transform _player;
        [SerializeField]
        private EnemyShot _rollingShot;
        [SerializeField]
        private EnemyShot _plungerShot;
        [SerializeField]
        private EnemyShot _squigglyShot;

        private GameplayDriver _driver;
        private Rigidbody2D    _body;
        private float          _elapsed;
        private int            _direction = 1;
        private bool           _frameB;
        private bool           _playing;
        private double         _shotTickElapsed;
        private int            _shotPhase;
        private bool           _rollingAttemptReady;
        private int            _plungerColumnIndex;
        private int            _squigglyColumnIndex;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private float _debugMovementMultiplier = 1f;
#endif

        public  const    int           Rows       = 5;
        public  const    int           Columns    = 11;
        public  const    int           Spacing    = 16;
        public  const    int           CellSize   = 16;
        private const    float         StepPeriod = 20f / 60f;
        private const    float         MinimumX   = (-ArcadeViewport.ScreenWidth / 2f + CellSize / 2f) * ArcadeViewport.UnitsPerPixel;
        private const    float         MaximumX   = (ArcadeViewport.ScreenWidth / 2f - CellSize / 2f - (Columns - 1) * Spacing) * ArcadeViewport.UnitsPerPixel;
        private readonly List<Invader> _invaders  = new List<Invader>();

        // ROM ColFireTable 0x1D00, indices 0x00..0x0F; one-based columns.
        private static readonly int[] _plungerColumns = { 1, 7, 1, 1, 1, 4, 11, 1, 6, 3, 1, 1, 11, 9, 2, 8 };

        // ColFireTable indices 0x06..0x14, inclusive. UFO sharing is not implemented.
        private static readonly int[] _squigglyColumns = { 11, 1, 6, 3, 1, 1, 11, 9, 2, 8, 2, 11, 4, 7, 10 };

        private const double ShotTickPeriod = 1d / 60d;

        private static readonly int[] _reloadScoreMsb = { 0x02, 0x10, 0x20, 0x30 };
        private static readonly int[] _reloadRates    = { 0x30, 0x10, 0x0B, 0x08, 0x07 };

        private void OnEnable()
        {
            if (_body != null)
            {
                ApplyPlayingState();
            }
        }

        private void FixedUpdate()
        {
            UpdateFormation();
            UpdateEnemyShots();
        }

        private void OnDisable()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _debugMovementMultiplier = 1f;
#endif
            if (_body != null)
            {
                ApplyPlayingState();
            }
        }

        internal void Initialize(GameplayDriver inDriver)
        {
            if (_invaderPrefab == null || _frames.Length != 6 || Array.Exists(_frames, inFrame => inFrame == null))
            {
                throw new InvalidOperationException("Formation requires its prefab and six frames. Migrate Arcade first.");
            }
            if (_player == null || _rollingShot == null || _plungerShot == null || _squigglyShot == null ||
                _rollingShot == _plungerShot || _rollingShot == _squigglyShot || _plungerShot == _squigglyShot)
            {
                throw new InvalidOperationException("Formation requires three independent enemy shots. Run Configure Squiggly EnemyShot.");
            }
            _driver = inDriver;
            _body   = GetComponent<Rigidbody2D>();
            CreateInvaders();
            BeginWave();
        }

        internal void BeginWave()
        {
            _elapsed             = 0f;
            _direction           = 1;
            _frameB              = false;
            _plungerColumnIndex  = 0; // Provisional: restart each Wave; preserve on death.
            _squigglyColumnIndex = 0;
            ResetEnemyShots();

            var initialPosition = new Vector2(32f - ArcadeViewport.ScreenWidth / 2f,
                208f - ArcadeViewport.ScreenHeight / 2f) * ArcadeViewport.UnitsPerPixel;
            // Keep both poses aligned while simulation is disabled, before enabling it again.
            transform.position = new Vector3(initialPosition.x, initialPosition.y, transform.position.z);
            _body.position     = initialPosition;

            _aliveCount = _invaders.Count;
            _rollingShot.SetFast(false);
            _plungerShot.SetFast(false);
            _squigglyShot.SetFast(false);
            foreach (var invader in _invaders)
            {
                invader.BeginWave();
            }
            ApplyPlayingState();
        }

        internal void SetPlaying(bool inPlaying)
        {
            _playing = inPlaying;
            if (inPlaying == false)
            {
                _elapsed = 0f;
                ResetShotClock();
            }
            ApplyPlayingState();
        }

        internal void NotifyDefeated(Invader inInvader, int inScore)
        {
            if (_playing == false || isActiveAndEnabled == false || _invaders.Contains(inInvader) == false)
            {
                return;
            }
            _aliveCount--;
            _rollingShot.SetFast(_aliveCount <= 8);
            _plungerShot.SetFast(_aliveCount <= 8);
            _squigglyShot.SetFast(_aliveCount <= 8);
            _driver.NotifyInvaderDefeated(inScore, _aliveCount == 0);
        }

        internal void PreparePlayerRespawn()
        {
            // Clear all slots without resetting the rack, column pointers or movement state.
            ResetEnemyShots();
        }

        private void CreateInvaders()
        {
            for (int row = 0; row < Rows; row++)
            {
                int type  = row == 0 ? 0 : row < 3 ? 1 : 2;
                int score = type == 0 ? 30 : type == 1 ? 20 : 10;
                for (int column = 0; column < Columns; column++)
                {
                    var invader                     = Instantiate(_invaderPrefab, transform);
                    invader.name                    = $"Invader ({row},{column})";
                    invader.transform.localPosition = new Vector3(column * Spacing, -row * Spacing, 0f) * ArcadeViewport.UnitsPerPixel;
                    invader.Initialize(this, _frames[type * 2], _frames[type * 2 + 1], score);
                    _invaders.Add(invader);
                }
            }
        }

        private void ApplyPlayingState()
        {
            bool playing    = _playing && isActiveAndEnabled;
            _body.simulated = playing;
            if (_rollingShot != null)
            {
                _rollingShot.SetPlaying(playing);
            }
            if (_plungerShot != null)
            {
                _plungerShot.SetPlaying(playing);
            }
            if (_squigglyShot != null)
            {
                _squigglyShot.SetPlaying(playing);
            }
            foreach (var invader in _invaders)
            {
                invader.SetPlaying(playing);
            }
        }

        private void UpdateFormation()
        {
            if (_playing == false)
            {
                return;
            }
            float elapsed = Time.fixedDeltaTime;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            elapsed *= _debugMovementMultiplier;
#endif
            _elapsed += elapsed;
            while (_elapsed + 1e-7f >= StepPeriod)
            {
                _elapsed -= StepPeriod;
                MoveFormation();
            }
        }

        private void MoveFormation()
        {
            var position = _body.position;
            position.x   = Mathf.Clamp(position.x + _direction * (2f * ArcadeViewport.UnitsPerPixel), MinimumX, MaximumX);
            if ((_direction > 0 && position.x >= MaximumX) || (_direction < 0 && position.x <= MinimumX))
            {
                position.y -= 8f * ArcadeViewport.UnitsPerPixel;
                _direction = -_direction;
            }
            var movement = position - _body.position;
            foreach (var invader in _invaders)
            {
                invader.ErodeShields(movement);
            }
            _body.position = position;
            _frameB        = _frameB == false;
            foreach (var invader in _invaders)
            {
                invader.ApplyAnimation(_frameB);
            }
        }

        private void ResetShotClock()
        {
            // Initial relative phase is a Unity approximation; do not catch up paused time.
            _shotTickElapsed = 0d;
            _shotPhase       = 0;
        }

        private void ResetEnemyShots()
        {
            _rollingShot.ResetFlight();
            _plungerShot.ResetFlight();
            _squigglyShot.ResetFlight();
            _rollingAttemptReady = false;
            ResetShotClock();
        }

        private void UpdateEnemyShots()
        {
            if (_playing == false)
            {
                return;
            }
            _rollingShot.PreparePhysicsStep();
            _plungerShot.PreparePhysicsStep();
            _squigglyShot.PreparePhysicsStep();
            _shotTickElapsed += Time.fixedDeltaTime;
            while (_shotTickElapsed + 1e-7d >= ShotTickPeriod)
            {
                // Do not accumulate a negative rounding residue from the float fixed delta.
                _shotTickElapsed = Math.Max(0d, _shotTickElapsed - ShotTickPeriod);
                switch (_shotPhase)
                {
                    case 0:
                        ProcessEnemyShot(_rollingShot, _plungerShot, _squigglyShot, 0);
                        break;
                    case 1:
                        ProcessEnemyShot(_plungerShot, _rollingShot, _squigglyShot, 1);
                        break;
                    case 2:
                        ProcessEnemyShot(_squigglyShot, _rollingShot, _plungerShot, 2);
                        break;
                }
                // ROM task timer counts down: 0 (Rolling), 2 (Squiggly), 1 (Plunger).
                _shotPhase = (_shotPhase + 2) % 3;
            }
        }

        private void ProcessEnemyShot(EnemyShot inShot, EnemyShot inOther1, EnemyShot inOther2, int inPhase)
        {
            if (inShot.InFlight)
            {
                inShot.AdvanceStep();
                return;
            }
            if (inPhase == 0 && _rollingAttemptReady == false)
            {
                // ROM 047D..0489: skip the first idle attempt after slot reinitialization.
                _rollingAttemptReady = true;
                return;
            }
            if (inPhase == 1 && _aliveCount <= 1)
            {
                return;
            }
            int reload = GetReloadRate(_driver.Score);
            if (CanReload(inOther1.StepCount, inOther2.StepCount, reload) == false)
            {
                return;
            }
            Vector2 position;
            bool found;
            switch (inPhase)
            {
                case 0:
                    found = TryGetRollingMuzzle(out position);
                    break;
                case 1:
                    found = TryGetTableMuzzle(_plungerColumns, ref _plungerColumnIndex, out position);
                    break;
                default:
                    found = TryGetTableMuzzle(_squigglyColumns, ref _squigglyColumnIndex, out position);
                    break;
            }
            if (found && inShot.TryLaunch(position) && inPhase == 0)
            {
                _rollingAttemptReady = false;
            }
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

        private static bool CanReload(int inOther1Steps, int inOther2Steps, int inReload)
        {
            // 057C..0595: ignore zero, otherwise require strictly greater than reload.
            return (inOther1Steps == 0 || inOther1Steps > inReload) &&
                (inOther2Steps == 0 || inOther2Steps > inReload);
        }

        private bool TryGetTableMuzzle(int[] inColumns, ref int inOutIndex, out Vector2 outPosition)
        {
            int column = inColumns[inOutIndex] - 1;
            inOutIndex = (inOutIndex + 1) % inColumns.Length;
            return TryGetColumnMuzzle(column, out outPosition);
        }

        private bool TryGetRollingMuzzle(out Vector2 outPosition)
        {
            outPosition   = default;
            float spacing = Spacing * ArcadeViewport.UnitsPerPixel;
            int column    = Mathf.FloorToInt((_player.position.x - _body.position.x) / spacing + 0.5f);
            if (column < 0 || column >= Columns)
            {
                return false; // Do not clamp to an edge column or choose a different one.
            }
            return TryGetColumnMuzzle(column, out outPosition);
        }

        private bool TryGetColumnMuzzle(int inColumn, out Vector2 outPosition)
        {
            outPosition = default;
            for (int row = Rows - 1; row >= 0; row--)
            {
                var invader = _invaders[row * Columns + inColumn];
                if (invader.gameObject.activeSelf)
                {
                    // Start below the existing 8px Invader hitbox; the shot position is its center.
                    outPosition = (Vector2)invader.transform.position - Vector2.up * ((4f + EnemyShot.Height / 2f) * ArcadeViewport.UnitsPerPixel);
                    return true;
                }
            }
            return false; // An empty target column does not fall back to an adjacent column.
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal void SetDebugFastMovement(bool inFast)
        {
            _debugMovementMultiplier = inFast && _playing && isActiveAndEnabled ? 10f : 1f;
        }
#endif
    }
}
