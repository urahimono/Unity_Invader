using System;
using UnityEngine.InputSystem;

namespace UnityInvader
{
    // Input System bindings for the Player component.
    public sealed class PlayerInputReader : IDisposable
    {
        private readonly InputAction _left  = new InputAction("Left", InputActionType.Button, "<Keyboard>/leftArrow");
        private readonly InputAction _right = new InputAction("Right", InputActionType.Button, "<Keyboard>/rightArrow");
        private readonly InputAction _fire  = new InputAction("Fire", InputActionType.Button, "<Keyboard>/space");

        public PlayerInputReader()
        {
            _left.Enable();
            _right.Enable();
            _fire.Enable();
        }

        public PlayerInputState Read()
        {
            return new PlayerInputState(_left.IsPressed(), _right.IsPressed(), _fire.IsPressed());
        }

        public void Dispose()
        {
            _left.Dispose();
            _right.Dispose();
            _fire.Dispose();
        }
    }
}
