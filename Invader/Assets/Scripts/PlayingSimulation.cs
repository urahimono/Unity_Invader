namespace UnityInvader
{
    // Space Invaders' Playing-only clock and interaction rules, independent of Unity.
    public sealed class PlayingSimulation
    {
        public const double TickDuration = PlayerSimulation.TickDuration;
        public const int InvaderHitBoxWidth = 16;
        public const int InvaderHitBoxHeight = 8;
        private double _accumulatedTime;
        private readonly PlayerSimulation _player = new PlayerSimulation();

        public PlayerModel Player => _player.Player;
        public PlayerShot Shot => _player.Shot;
        public InvaderFormation Formation { get; private set; } = new InvaderFormation();
        public int Score { get; private set; }
        public int WaveNumber { get; private set; } = 1;
        public bool WaveClearRequired { get; private set; }

        public void StartNextWave()
        {
            if (!WaveClearRequired) return;
            Formation = new InvaderFormation();
            WaveNumber++;
            WaveClearRequired = false;
            _accumulatedTime = 0;
            // Keep Player, Score and the single Shot (including its release gate).
        }

        public void Advance(double elapsedSeconds, bool playing, PlayerInputState input)
        {
            if (!playing || WaveClearRequired)
            {
                _accumulatedTime = 0;
                return;
            }
            // Retain release/press sampled between ticks, as before.
            Shot.ReadFire(input.Fire);
            _accumulatedTime += elapsedSeconds;
            while (_accumulatedTime + 1e-12 >= TickDuration)
            {
                // Deterministic order: Player -> Shot -> Formation -> hit/score.
                _player.Tick(input);
                Formation.Tick();
                ResolvePlayerShotHit();
                _accumulatedTime -= TickDuration;
                if (Formation.AliveCount == 0)
                {
                    WaveClearRequired = true;
                    _accumulatedTime = 0;
                    break; // Do not run extra ticks after the final hit, even at 30fps.
                }
            }
        }

        private void ResolvePlayerShotHit()
        {
            if (!Shot.Active) return;
            // Upward shot: bottom row first, then columns from left to right.
            for (int row = InvaderFormation.Rows - 1; row >= 0; row--)
            {
                for (int column = 0; column < InvaderFormation.Columns; column++)
                {
                    var slot = Formation.Invaders[row * InvaderFormation.Columns + column];
                    if (!slot.Alive) continue;
                    int x = Formation.X + column * InvaderFormation.Spacing;
                    int y = Formation.Y - row * InvaderFormation.Spacing;
                    // Positive-area AABB overlap. Shot X is center, Y is its bottom edge.
                    if (Shot.X + PlayerShot.Width / 2.0 <= x - InvaderHitBoxWidth / 2 ||
                        Shot.X - PlayerShot.Width / 2.0 >= x + InvaderHitBoxWidth / 2 ||
                        Shot.Y + PlayerShot.Height <= y - InvaderHitBoxHeight / 2 ||
                        Shot.Y >= y + InvaderHitBoxHeight / 2) continue;
                    Formation.TryKill(slot.Id);
                    Shot.Deactivate();
                    Score += slot.Type == InvaderType.Top ? 30 : slot.Type == InvaderType.Middle ? 20 : 10;
                    return; // One shot may kill only one Invader, even at a shared horizontal edge.
                }
            }
        }
    }
}
