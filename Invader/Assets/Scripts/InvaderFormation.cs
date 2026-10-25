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
        [Min(0.05f)]
        [Tooltip("Provisional: attempt a Rolling shot every 2 seconds of Playing; not the original score-dependent reload rate.")]
        private float     _rollingFireInterval = 2f;

        private GameplayDriver _driver;
        private Rigidbody2D    _body;
        private float          _elapsed;
        private int            _direction = 1;
        private bool           _frameB;
        private bool           _playing;
        private float          _rollingFireElapsed;

        public  const    int           Rows       = 5;
        public  const    int           Columns    = 11;
        public  const    int           Spacing    = 16;
        public  const    int           CellSize   = 16;
        private const    float         StepPeriod = 20f / 60f;
        private const    float         MinimumX   = (-ArcadeViewport.ScreenWidth / 2f + CellSize / 2f) * ArcadeViewport.UnitsPerPixel;
        private const    float         MaximumX   = (ArcadeViewport.ScreenWidth / 2f - CellSize / 2f - (Columns - 1) * Spacing) * ArcadeViewport.UnitsPerPixel;
        private readonly List<Invader> _invaders  = new List<Invader>();

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
            UpdateRollingShot();
        }

        private void OnDisable()
        {
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
            if (_player == null || _rollingShot == null)
            {
                throw new InvalidOperationException("Formation requires Rolling shot references. Run Configure Rolling EnemyShot.");
            }
            _driver = inDriver;
            _body   = GetComponent<Rigidbody2D>();
            CreateInvaders();
            BeginWave();
        }

        internal void BeginWave()
        {
            _elapsed            = 0f;
            _direction          = 1;
            _frameB             = false;
            _rollingFireElapsed = 0f;
            _rollingShot.ResetFlight();

            var initialPosition = new Vector2(32f - ArcadeViewport.ScreenWidth / 2f,
                208f - ArcadeViewport.ScreenHeight / 2f) * ArcadeViewport.UnitsPerPixel;
            // Keep both poses aligned while simulation is disabled, before enabling it again.
            transform.position = new Vector3(initialPosition.x, initialPosition.y, transform.position.z);
            _body.position     = initialPosition;

            _aliveCount = _invaders.Count;
            _rollingShot.SetFast(false);
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
                _elapsed            = 0f;
                _rollingFireElapsed = 0f;
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
            _driver.NotifyInvaderDefeated(inScore, _aliveCount == 0);
        }

        internal void PreparePlayerRespawn()
        {
            // Clear the single Rolling slot without resetting the current rack or movement state.
            _rollingShot.ResetFlight();
            _rollingFireElapsed = 0f;
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
            _elapsed += Time.fixedDeltaTime;
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

        private void UpdateRollingShot()
        {
            if (_playing == false)
            {
                return;
            }
            _rollingShot.SetFast(_aliveCount <= 8);
            _rollingFireElapsed += Time.fixedDeltaTime;
            if (_rollingFireElapsed + 1e-7f < _rollingFireInterval)
            {
                return;
            }
            // No catch-up burst and no queued launch while the single Rolling shot is occupied.
            _rollingFireElapsed = 0f;
            if (_rollingShot.InFlight == false && TryGetRollingMuzzle(out var position))
            {
                _rollingShot.TryLaunch(position);
            }
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
            for (int row = Rows - 1; row >= 0; row--)
            {
                var invader = _invaders[row * Columns + column];
                if (invader.gameObject.activeSelf)
                {
                    // Start below the existing 8px Invader hitbox; the shot position is its center.
                    outPosition = (Vector2)invader.transform.position - Vector2.up * ((4f + EnemyShot.Height / 2f) * ArcadeViewport.UnitsPerPixel);
                    return true;
                }
            }
            return false; // An empty target column does not fall back to an adjacent column.
        }
    }
}
