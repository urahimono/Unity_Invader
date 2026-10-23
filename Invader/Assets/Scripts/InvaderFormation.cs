using System;
using System.Collections.Generic;

namespace UnityInvader
{
    public enum InvaderType { Top, Middle, Bottom }

    public sealed class InvaderSlot
    {
        public int Row { get; }
        public int Column { get; }
        public int Id => Row * InvaderFormation.Columns + Column;
        public InvaderType Type => Row == 0 ? InvaderType.Top :
            Row < 3 ? InvaderType.Middle : InvaderType.Bottom;
        public bool Alive { get; private set; } = true;

        public InvaderSlot(int row, int column)
        {
            Row = row;
            Column = column;
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
        public const int Rows = 5;
        public const int Columns = 11;
        public const int Spacing = 16;
        public const int CellSize = 16;
        public const int HorizontalStep = 2;
        public const int Descent = 8;
        public const int TicksPerStep = 20;
        public const int MinimumX = CellSize / 2;
        public const int MaximumX = PlayerModel.ScreenWidth - CellSize / 2 - (Columns - 1) * Spacing;
        private int _ticksUntilStep = TicksPerStep;

        // X/Y are the center of the top-left slot, not a Unity Transform.
        public int X { get; private set; } = 32;
        public int Y { get; private set; } = 208;
        public int Direction { get; private set; } = 1;
        public int StepCount { get; private set; }
        public int AnimationFrame => StepCount % 2;
        public IReadOnlyList<InvaderSlot> Invaders { get; }
        public int AliveCount { get; private set; } = Rows * Columns;

        public InvaderFormation()
        {
            var slots = new InvaderSlot[Rows * Columns];
            for (int row = 0; row < Rows; row++)
            {
                for (int column = 0; column < Columns; column++)
                {
                    slots[row * Columns + column] = new InvaderSlot(row, column);
                }
            }
            Invaders = Array.AsReadOnly(slots);
        }

        public bool TryKill(int id)
        {
            if (Invaders[id].TryKill() == false)
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
            X = Math.Max(MinimumX, Math.Min(MaximumX, X + Direction * HorizontalStep));
            if ((Direction > 0 && X == MaximumX) || (Direction < 0 && X == MinimumX))
            {
                Y -= Descent;
                Direction = -Direction;
            }
            StepCount++;
        }
    }
}
