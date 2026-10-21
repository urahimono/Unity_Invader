using System;
using UnityEngine.InputSystem;

namespace UnityInvader
{
    // Unity input is converted here; the simulation receives only two booleans.
    public sealed class PlayerInputReader : IDisposable
    {
        private readonly InputAction left = new InputAction("Left", InputActionType.Button, "<Keyboard>/leftArrow");
        private readonly InputAction right = new InputAction("Right", InputActionType.Button, "<Keyboard>/rightArrow");

        public PlayerInputReader()
        {
            left.Enable();
            right.Enable();
        }

        public PlayerInputState Read()
        {
            return new PlayerInputState(left.IsPressed(), right.IsPressed());
        }

        public void Dispose()
        {
            left.Dispose();
            right.Dispose();
        }
    }
}
