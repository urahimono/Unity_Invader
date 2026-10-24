namespace UnityInvader
{
    public readonly struct InvaderDisplayState
    {
        public int Id             { get; }
        public int Row            { get; }
        public int Column         { get; }
        public int X              { get; }
        public int Y              { get; }
        public int AnimationFrame { get; }

        public int DisplayX => X - PlayerBody.ScreenWidth / 2;
        public int DisplayY => Y - PlayerBody.ScreenHeight / 2;

        public InvaderType Type { get; }

        public bool Alive   { get; }
        public bool Visible { get; }

        public InvaderDisplayState(int inId, int inRow, int inColumn, int inX, int inY,
            InvaderType inType, int inAnimationFrame, bool inAlive, bool inVisible)
        {
            Id             = inId;
            Row            = inRow;
            Column         = inColumn;
            X              = inX;
            Y              = inY;
            Type           = inType;
            AnimationFrame = inAnimationFrame;
            Alive          = inAlive;
            Visible        = inVisible && inAlive;
        }
    }
}
