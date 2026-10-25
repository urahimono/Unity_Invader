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

        private GameplayDriver _driver;
        private Rigidbody2D    _body;
        private float          _elapsed;
        private int            _direction = 1;
        private bool           _frameB;
        private bool           _playing;

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
            _driver = inDriver;
            _body   = GetComponent<Rigidbody2D>();
            CreateInvaders();
            BeginWave();
        }

        internal void BeginWave()
        {
            _elapsed       = 0f;
            _direction     = 1;
            _frameB        = false;
            _body.position = new Vector2(32f - ArcadeViewport.ScreenWidth / 2f,
                208f - ArcadeViewport.ScreenHeight / 2f) * ArcadeViewport.UnitsPerPixel;
            _aliveCount    = _invaders.Count;
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
            _driver.NotifyInvaderDefeated(inScore, _aliveCount == 0);
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
            _body.position = position;
            _frameB        = _frameB == false;
            foreach (var invader in _invaders)
            {
                invader.ApplyAnimation(_frameB);
            }
        }
    }
}
