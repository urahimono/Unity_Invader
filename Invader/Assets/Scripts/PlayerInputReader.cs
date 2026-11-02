using System;
using UnityEngine.InputSystem;

namespace UnityInvader
{
    // Input System bindings for Player input and Driver-owned game start.
    public sealed class PlayerInputReader : IDisposable
    {
        private bool _startReady;

        private readonly InputAction _left  = new InputAction("Left", InputActionType.Button, "<Keyboard>/leftArrow");
        private readonly InputAction _right = new InputAction("Right", InputActionType.Button, "<Keyboard>/rightArrow");
        private readonly InputAction _fire  = new InputAction("Fire", InputActionType.Button, "<Keyboard>/space");
        private readonly InputAction _start = new InputAction("Start", InputActionType.Button, "<Keyboard>/enter");

        public PlayerInputReader()
        {
            _left.Enable();
            _right.Enable();
            _fire.Enable();
            _start.Enable();
        }

        public PlayerInputState Read()
        {
            return new PlayerInputState(_left.IsPressed(), _right.IsPressed(), _fire.IsPressed());
        }

        public bool ReadStartPressed()
        {
            if (_start.IsPressed() == false)
            {
                _startReady = true;
                return false;
            }
            if (_startReady == false)
            {
                return false;
            }
            _startReady = false;
            return true;
        }

        public void Dispose()
        {
            _left.Dispose();
            _right.Dispose();
            _fire.Dispose();
            _start.Dispose();
        }
    }
}
