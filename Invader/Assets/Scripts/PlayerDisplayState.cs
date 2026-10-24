namespace UnityInvader
{
    public readonly struct BodyState
    {
        public int X { get; }
        public int Y { get; }

        public BodyState(int inX, int inY)
        {
            X = inX;
            Y = inY;
        }
    }

    public readonly struct ShotState
    {
        public int X { get; }
        public int Y { get; }

        public bool Active    { get; }
        public bool FireReady { get; }

        public ShotState(int inX, int inY, bool inActive, bool inFireReady)
        {
            X         = inX;
            Y         = inY;
            Active    = inActive;
            FireReady = inFireReady;
        }
    }

    // A detached value snapshot, never a reference to the owned body or shot.
    public readonly struct PlayerState
    {
        public BodyState Player { get; }

        public ShotState Shot { get; }

        public PlayerState(BodyState inPlayer, ShotState inShot)
        {
            Player = inPlayer;
            Shot   = inShot;
        }
    }

    public readonly struct PlayerDisplayState
    {
        public int X          { get; }
        public int Y          { get; }
        public int ShotX      { get; }
        public int ShotY      { get; }
        public int ShotWidth  { get; }
        public int ShotHeight { get; }

        public bool Visible     { get; }
        public bool ShotVisible { get; }

        public PlayerDisplayState(PlayerState inPlayer, bool inVisible)
        {
            X           = inPlayer.Player.X - PlayerBody.ScreenWidth / 2;
            Y           = inPlayer.Player.Y - PlayerBody.ScreenHeight / 2;
            ShotX       = inPlayer.Shot.X - PlayerBody.ScreenWidth / 2;
            ShotY       = inPlayer.Shot.Y - PlayerBody.ScreenHeight / 2;
            ShotWidth   = PlayerShot.Width;
            ShotHeight  = PlayerShot.Height;
            Visible     = inVisible;
            ShotVisible = inVisible && inPlayer.Shot.Active;
        }
    }
}
