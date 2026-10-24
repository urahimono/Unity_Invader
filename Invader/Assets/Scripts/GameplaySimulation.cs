namespace UnityInvader
{
    // Space Invaders' Playing-only clock and interaction rules, independent of Unity.
    public sealed class GameplaySimulation
    {
        private double           _accumulatedTime;
        private InvaderFormation _formation       = new InvaderFormation();

        public int Score      { get; private set; }
        public int WaveNumber { get; private set; } = 1;

        public bool WaveClearRequired { get; private set; }

        public  const    double     TickDuration        = PlayerUnit.TickDuration;
        public  const    int        InvaderHitBoxWidth  = 16;
        public  const    int        InvaderHitBoxHeight = 8;
        private readonly PlayerUnit _player             = new PlayerUnit();

        public void StartNextWave()
        {
            if (WaveClearRequired == false)
            {
                return;
            }
            _formation = new InvaderFormation();
            WaveNumber++;
            WaveClearRequired = false;
            _accumulatedTime  = 0;
            // Keep Player, Score and the single Shot (including its release gate).
        }

        public void Advance(double inElapsedSeconds, bool inPlaying, PlayerInputState inInput)
        {
            if (inPlaying == false || WaveClearRequired)
            {
                _accumulatedTime = 0;
                return;
            }
            // Retain release/press sampled between ticks, as before.
            _player.ReadFire(inInput.Fire);
            _accumulatedTime += inElapsedSeconds;
            while (_accumulatedTime + 1e-12 >= TickDuration)
            {
                // Deterministic order: Player -> Shot -> Formation -> hit/score.
                _player.Tick(inInput);
                _formation.Tick();
                ResolvePlayerShotHit();
                _accumulatedTime -= TickDuration;
                if (_formation.AliveCount == 0)
                {
                    WaveClearRequired = true;
                    _accumulatedTime  = 0;
                    break; // Do not run extra ticks after the final hit, even at 30fps.
                }
            }
        }

        public void CopyDisplayState(bool inVisible, out PlayerDisplayState outPlayer, System.Span<InvaderDisplayState> inDisplayBuffer)
        {
            if (inDisplayBuffer.Length != InvaderFormation.Rows * InvaderFormation.Columns)
            {
                throw new System.ArgumentException("Expected 55 display slots.", nameof(inDisplayBuffer));
            }
            outPlayer   = new PlayerDisplayState(_player.CaptureState(), inVisible);
            for (int id = 0; id < inDisplayBuffer.Length; id++)
            {
                inDisplayBuffer[id] = _formation.CaptureInvader(id, inVisible);
            }
        }

        private void ResolvePlayerShotHit()
        {
            var shot = _player.CaptureState().Shot;
            if (shot.Active == false)
            {
                return;
            }
            // Upward shot: bottom row first, then columns from left to right.
            for (int row = InvaderFormation.Rows - 1; row >= 0; row--)
            {
                for (int column = 0; column < InvaderFormation.Columns; column++)
                {
                    var slot = _formation.CaptureInvader(row * InvaderFormation.Columns + column);
                    if (slot.Alive == false)
                    {
                        continue;
                    }
                    int x = _formation.X + column * InvaderFormation.Spacing;
                    int y = _formation.Y - row * InvaderFormation.Spacing;
                    // Positive-area AABB overlap. Shot X is center, Y is its bottom edge.
                    if (shot.X + PlayerShot.Width / 2.0 <= x - InvaderHitBoxWidth / 2 ||
                        shot.X - PlayerShot.Width / 2.0 >= x + InvaderHitBoxWidth / 2 ||
                        shot.Y + PlayerShot.Height <= y - InvaderHitBoxHeight / 2 ||
                        shot.Y >= y + InvaderHitBoxHeight / 2)
                    {
                        continue;
                    }
                    _formation.TryKill(slot.Id);
                    _player.ConsumeShotHit();
                    Score += slot.Type == InvaderType.Top ? 30 : slot.Type == InvaderType.Middle ? 20 : 10;
                    return; // One shot may kill only one Invader, even at a shared horizontal edge.
                }
            }
        }

#if UNITY_EDITOR
        public GameplayVerificationState EditorCapture()
        {
            return new GameplayVerificationState(_player.CaptureState(), _formation.EditorCapture(), Score, WaveNumber, WaveClearRequired);
        }

        public void EditorPrepareShot(int inX, int inY)
        {
            _player.EditorPrepareShot(inX, inY);
        }

        public void EditorDeactivateShot()
        {
            _player.ConsumeShotHit();
        }

        public bool EditorKillInvader(int inId)
        {
            return _formation.TryKill(inId);
        }
#endif
    }
}
