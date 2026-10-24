using System;

namespace UnityInvader
{
    public enum InvaderType { Top, Middle, Bottom }

    public sealed class InvaderSlot
    {
        public int Row    { get; }
        public int Column { get; }

        public int Id => Row * InvaderFormation.Columns + Column;

        public InvaderType Type => Row == 0 ? InvaderType.Top :
            Row < 3 ? InvaderType.Middle : InvaderType.Bottom;

        public bool Alive { get; private set; } = true;

        public InvaderSlot(int inRow, int inColumn)
        {
            Row    = inRow;
            Column = inColumn;
        }

        internal bool TryKill()
        {
            if (Alive == false)
            {
                return false;
            }
            Alive = false;
            return true;
        }
    }

    // Logical pixels with a bottom-left origin. Placement and timing are provisional.
    public sealed class InvaderFormation
    {
        private int _ticksUntilStep = TicksPerStep;

        // X/Y are the center of the top-left slot, not a Unity Transform.
        public int X          { get; private set; } = 32;
        public int Y          { get; private set; } = 208;
        public int Direction  { get; private set; } = 1;
        public int StepCount  { get; private set; }
        public int AliveCount { get; private set; } = Rows * Columns;

        public int AnimationFrame => StepCount % 2;

        public  const    int           Rows           = 5;
        public  const    int           Columns        = 11;
        public  const    int           Spacing        = 16;
        public  const    int           CellSize       = 16;
        public  const    int           HorizontalStep = 2;
        public  const    int           Descent        = 8;
        public  const    int           TicksPerStep   = 20;
        public  const    int           MinimumX       = CellSize / 2;
        public  const    int           MaximumX       = PlayerBody.ScreenWidth - CellSize / 2 - (Columns - 1) * Spacing;
        private readonly InvaderSlot[] _invaders      = new InvaderSlot[Rows * Columns];

        public InvaderFormation()
        {
            for (int row = 0; row < Rows; row++)
            {
                for (int column = 0; column < Columns; column++)
                {
                    _invaders[row * Columns + column] = new InvaderSlot(row, column);
                }
            }
        }

        public InvaderDisplayState CaptureInvader(int inId, bool inVisible = true)
        {
            var slot = _invaders[inId];
            return new InvaderDisplayState(slot.Id, slot.Row, slot.Column,
                X + slot.Column * Spacing, Y - slot.Row * Spacing,
                slot.Type, AnimationFrame, slot.Alive, inVisible);
        }

        public bool TryKill(int inId)
        {
            if (_invaders[inId].TryKill() == false)
            {
                return false;
            }
            AliveCount--;
            return true;
        }

        public void Tick()
        {
            if (--_ticksUntilStep > 0)
            {
                return;
            }
            _ticksUntilStep = TicksPerStep;
            X               = Math.Max(MinimumX, Math.Min(MaximumX, X + Direction * HorizontalStep));
            if ((Direction > 0 && X == MaximumX) || (Direction < 0 && X == MinimumX))
            {
                Y -= Descent;
                Direction = -Direction;
            }
            StepCount++;
        }

#if UNITY_EDITOR
        public FormationVerificationState EditorCapture()
        {
            var invaders = new InvaderDisplayState[_invaders.Length];
            for (int id = 0; id < invaders.Length; id++)
            {
                invaders[id] = CaptureInvader(id);
            }
            return new FormationVerificationState(X, Y, Direction, StepCount, AliveCount, AnimationFrame, invaders);
        }
#endif
    }
}
