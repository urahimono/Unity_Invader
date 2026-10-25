using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine.InputSystem;
#endif

namespace UnityInvader
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(InvaderFormation))]
    [DefaultExecutionOrder(-100)]
    public sealed class DebugFormationSpeed : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private InvaderFormation _formation;
        private InputAction      _fast;

        private void Awake()
        {
            _formation = GetComponent<InvaderFormation>();
        }

        private void OnEnable()
        {
            _fast                        = new InputAction("DebugFormationSpeed", InputActionType.Button, "<Keyboard>/f");
            _fast.wantsInitialStateCheck = true;
            _fast.Enable();
        }

        private void FixedUpdate()
        {
            _formation.SetDebugFastMovement(_fast.IsPressed());
        }

        private void OnDisable()
        {
            _fast?.Dispose();
            _fast = null;
            if (_formation != null)
            {
                _formation.SetDebugFastMovement(false);
            }
        }
#endif
    }
}
