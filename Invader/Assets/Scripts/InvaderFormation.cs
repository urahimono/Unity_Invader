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
        private Invader _invaderPrefab;
        [SerializeField]
        private int     _aliveCount;

        [SerializeField]
        private EnemyShot _rollingShot;
        [SerializeField]
        private EnemyShot _plungerShot;
        [SerializeField]
        private EnemyShot _squigglyShot;
        [SerializeField]
        private Ufo       _ufo;

        private IPositionProvider   _player;
        private Func<long>          _getNormalShotEndCount;
        private EnemyShotController _enemyShots;
        private Rigidbody2D         _body;
        private float               _elapsed;
        private int                 _direction = 1;
        private bool                _playing;
        private double              _shotTickElapsed;
        private int                 _shotPhase;
        private int                 _ufoTicksRemaining;
        private bool                _ufoPending;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private float _debugMovementMultiplier = 1f;
#endif

        public event Action Invaded;

        internal bool IsCleared => _aliveCount == 0;

        public  const    int           Rows       = 5;
        public  const    int           Columns    = 11;
        public  const    int           Spacing    = 16;
        public  const    int           CellSize   = 16;
        private const    float         StepPeriod = 20f / 60f;
        private const    float         MinimumX   = (-ArcadeViewport.ScreenWidth / 2f + CellSize / 2f) * ArcadeViewport.UnitsPerPixel;
        private const    float         MaximumX   = (ArcadeViewport.ScreenWidth / 2f - CellSize / 2f - (Columns - 1) * Spacing) * ArcadeViewport.UnitsPerPixel;
        private readonly List<Invader> _invaders  = new List<Invader>();

        private const double ShotTickPeriod = 1d / 60d;

        private const int   UfoReloadTicks = 0x0600;
        private const float FirstWaveY    = (208f - ArcadeViewport.ScreenHeight / 2f) * ArcadeViewport.UnitsPerPixel;
        // Provisional sprite-center anchor, not the ROM's rotated drawing coordinate.
        private const float InvasionY = (40f - ArcadeViewport.ScreenHeight / 2f) * ArcadeViewport.UnitsPerPixel;

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

        private void OnDestroy()
        {
            foreach (var invader in _invaders)
            {
                if (invader != null)
                {
                    invader.Defeated -= NotifyDefeated;
                }
            }
        }

        internal void Initialize(IPositionProvider inPlayer, Func<long> inGetNormalShotEndCount, Func<int> inGetScore)
        {
            if (inGetScore == null)
            {
                throw new ArgumentNullException(nameof(inGetScore));
            }
            if (inGetNormalShotEndCount == null)
            {
                throw new ArgumentNullException(nameof(inGetNormalShotEndCount));
            }
            if (_invaderPrefab == null)
            {
                throw new InvalidOperationException("Formation requires its Invader prefab.");
            }
            if (inPlayer == null || _rollingShot == null || _plungerShot == null || _squigglyShot == null || _ufo == null ||
                _rollingShot == _plungerShot || _rollingShot == _squigglyShot || _plungerShot == _squigglyShot)
            {
                throw new InvalidOperationException("Formation requires three independent enemy shots and UFO. Run Configure UFO.");
            }
            _player               = inPlayer;
            _getNormalShotEndCount = inGetNormalShotEndCount;
            _body                 = GetComponent<Rigidbody2D>();
            _enemyShots            = new EnemyShotController(_rollingShot, _plungerShot, _squigglyShot,
                GetColumnMuzzle, inGetScore, Columns, Spacing * ArcadeViewport.UnitsPerPixel);
            CreateInvaders();
            BeginWave();
        }

        internal void BeginWave()
        {
            _elapsed             = 0f;
            _direction           = 1;
            _enemyShots.BeginWave();
            ResetShotClock();
            ResetUfo();

            var initialPosition = new Vector2(32f - ArcadeViewport.ScreenWidth / 2f,
                208f - ArcadeViewport.ScreenHeight / 2f) * ArcadeViewport.UnitsPerPixel;
            // Keep both poses aligned while simulation is disabled, before enabling it again.
            transform.position = new Vector3(initialPosition.x, initialPosition.y, transform.position.z);
            _body.position     = initialPosition;

            _aliveCount = _invaders.Count;
            _enemyShots.SetFast(false);
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

        internal void NotifyDefeated(Invader inInvader, Component inSource)
        {
            if (_playing == false || isActiveAndEnabled == false || _invaders.Contains(inInvader) == false)
            {
                return;
            }
            _aliveCount--;
            _enemyShots.SetFast(_aliveCount <= 8);
        }

        internal void PreparePlayerRespawn()
        {
            // Clear all slots without resetting the rack, column pointers or movement state.
            _enemyShots.ResetFlights();
            ResetShotClock();
            ResetUfo();
        }

        private void CreateInvaders()
        {
            for (int row = 0; row < Rows; row++)
            {
                var type = row == 0 ? InvaderType.Top : row < 3 ? InvaderType.Middle : InvaderType.Bottom;
                for (int column = 0; column < Columns; column++)
                {
                    var invader                     = Instantiate(_invaderPrefab, transform);
                    invader.name                    = $"Invader ({row},{column})";
                    invader.transform.localPosition = new Vector3(column * Spacing, -row * Spacing, 0f) * ArcadeViewport.UnitsPerPixel;
                    invader.Initialize(type);
                    invader.Defeated += NotifyDefeated;
                    _invaders.Add(invader);
                }
            }
        }

        private void ApplyPlayingState()
        {
            bool playing    = _playing && isActiveAndEnabled;
            _body.simulated = playing;
            _enemyShots.SetPlaying(playing);
            if (_ufo != null)
            {
                _ufo.SetPlaying(playing);
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
            while (_playing && _elapsed + 1e-7f >= StepPeriod)
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
            foreach (var invader in _invaders)
            {
                invader.AdvanceAnimation();
            }
            CheckInvasion(position);
        }

        private void CheckInvasion(Vector2 inPosition)
        {
            foreach (var invader in _invaders)
            {
                // Use the destination body pose: child world Transforms may still be one step behind.
                float y = inPosition.y + invader.transform.localPosition.y;
                // Ignore subpixel float residue from repeated 8px descents at exactly 40px.
                if (invader.gameObject.activeSelf && y < InvasionY - 1e-6f)
                {
                    Invaded?.Invoke();
                    return;
                }
            }
        }

        private void ResetShotClock()
        {
            // Initial relative phase is a Unity approximation; do not catch up paused time.
            _shotTickElapsed = 0d;
            _shotPhase       = 0;
        }

        private void UpdateEnemyShots()
        {
            if (_playing == false)
            {
                return;
            }
            _enemyShots.PreparePhysicsStep();
            _ufo.PreparePhysicsStep();
            _shotTickElapsed += Time.fixedDeltaTime;
            while (_shotTickElapsed + 1e-7d >= ShotTickPeriod)
            {
                // Do not accumulate a negative rounding residue from the float fixed delta.
                _shotTickElapsed = Math.Max(0d, _shotTickElapsed - ShotTickPeriod);
                switch (_shotPhase)
                {
                    case 0:
                        _enemyShots.ProcessRolling(_player.Position, _body.position);
                        break;
                    case 1:
                        _enemyShots.ProcessPlunger(_aliveCount);
                        break;
                    case 2:
                        ProcessUfoOrSquiggly();
                        break;
                }
                TickUfoCountdown(); // ROM 007E: TimeToSaucer follows the object work.
                // ROM task timer counts down: 0 (Rolling), 2 (Squiggly), 1 (Plunger).
                _shotPhase = (_shotPhase + 2) % 3;
            }
        }

        private void ResetUfo()
        {
            _ufo.ResetFlight();
            _ufoTicksRemaining = UfoReloadTicks;
            _ufoPending        = false;
        }

        private void TickUfoCountdown()
        {
            if (_body.position.y >= FirstWaveY - 1e-6f)
            {
                return;
            }
            // 0913..092D checks zero before decrementing; reload also consumes one tick.
            if (_ufoTicksRemaining == 0)
            {
                _ufoTicksRemaining = UfoReloadTicks;
                _ufoPending        = true;
            }
            _ufoTicksRemaining--;
        }

        private void ProcessUfoOrSquiggly()
        {
            if (_ufo.InFlight)
            {
                _ufo.AdvanceStep();
                return;
            }
            if (_ufoPending && _aliveCount >= 8 && _enemyShots.IsSquigglyInFlight == false)
            {
                bool movingRight = (_getNormalShotEndCount() & 1L) != 0;
                if (_ufo.TryLaunch(movingRight))
                {
                    _ufoPending = false;
                }
                return;
            }
            _enemyShots.ProcessSquiggly();
        }

        private Vector2? GetColumnMuzzle(int inColumn)
        {
            for (int row = Rows - 1; row >= 0; row--)
            {
                var invader = _invaders[row * Columns + inColumn];
                if (invader.gameObject.activeSelf)
                {
                    // Start below the existing 8px Invader hitbox; the shot position is its center.
                    return (Vector2)invader.transform.position - Vector2.up * ((4f + EnemyShot.Height / 2f) * ArcadeViewport.UnitsPerPixel);
                }
            }
            return null; // An empty target column does not fall back to an adjacent column.
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal void SetDebugFastMovement(bool inFast)
        {
            _debugMovementMultiplier = inFast && _playing && isActiveAndEnabled ? 10f : 1f;
        }
#endif
    }
}
