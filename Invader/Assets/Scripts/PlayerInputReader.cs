using System;
using UnityEngine.InputSystem;

namespace UnityInvader
{
    // Unity input is converted here; the simulation receives only booleans.
    public sealed class PlayerInputReader : IDisposable
    {
        private readonly InputAction left = new InputAction("Left", InputActionType.Button, "<Keyboard>/leftArrow");
        private readonly InputAction right = new InputAction("Right", InputActionType.Button, "<Keyboard>/rightArrow");
        private readonly InputAction fire = new InputAction("Fire", InputActionType.Button, "<Keyboard>/space");

        public PlayerInputReader()
        {
            left.Enable();
            right.Enable();
            fire.Enable();
        }

        public PlayerInputState Read()
        {
            return new PlayerInputState(left.IsPressed(), right.IsPressed(), fire.IsPressed());
        }

        public void Dispose()
        {
            left.Dispose();
            right.Dispose();
            fire.Dispose();
        }
    }
}
