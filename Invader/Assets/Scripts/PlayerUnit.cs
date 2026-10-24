namespace UnityInvader
{
    public sealed class PlayerUnit
    {
        public  const    double     TickDuration = 1.0 / 60.0;
        private          double     _accumulatedTime;
        private readonly PlayerBody _player = new PlayerBody();
        private readonly PlayerShot _shot   = new PlayerShot();

        public PlayerState CaptureState()
        {
            return new PlayerState(new BodyState(_player.X, _player.Y),
                new ShotState(_shot.X, _shot.Y, _shot.Active, _shot.FireReady));
        }

        internal void ReadFire(bool inPressed)
        {
            _shot.ReadFire(inPressed);
        }

        internal void ConsumeShotHit()
        {
            _shot.Deactivate();
        }

        public void Advance(double inElapsedSeconds, bool inPlaying, PlayerInputState inInput)
        {
            if (inPlaying == false)
            {
                _accumulatedTime = 0;
                return;
            }

            // Sample every render frame so release/press between logical ticks is retained.
            _shot.ReadFire(inInput.Fire);
            _accumulatedTime += inElapsedSeconds;
            while (_accumulatedTime + 1e-12 >= TickDuration)
            {
                Tick(inInput);
                _accumulatedTime -= TickDuration;
            }
        }

        // GameplaySimulation owns the live clock; Advance remains available for isolated verification.
        internal void Tick(PlayerInputState inInput)
        {
            _player.Tick(inInput);
            _shot.Tick(_player);
        }

#if UNITY_EDITOR
        public void EditorPrepareShot(int inX, int inY)
        {
            if (_shot.Active || inX < PlayerBody.MinimumX || inX > PlayerBody.MaximumX ||
                inY < _player.Y + 8 || inY >= PlayerBody.ScreenHeight || (inY - _player.Y - 8) % PlayerShot.SpeedPerTick != 0)
            {
                throw new System.InvalidOperationException("Fixture requires an empty shot and a reachable position.");
            }
            while (_player.X != inX)
            {
                _player.Tick(new PlayerInputState(_player.X > inX, _player.X < inX));
            }
            _shot.ReadFire(false);
            _shot.ReadFire(true);
            _shot.Tick(_player);
            while (_shot.Y < inY)
            {
                _shot.Tick(_player);
            }
        }
#endif
    }
}
