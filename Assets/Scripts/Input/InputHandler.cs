using UnityEngine;
using UnityEngine.InputSystem;

namespace RacingGame
{
    /// <summary>
    /// Unity InputSystem adapter implementing IInputProvider.
    /// Buffers discrete key presses between Update and FixedUpdate so taps are never dropped.
    /// </summary>
    public class InputHandler : MonoBehaviour, IInputProvider
    {
        [Header("Steering Smoothing")]
        [SerializeField] private float steerResponseSpeed = 13f;

        private float _smoothedSteer;
        private bool _kickQueued;
        private bool _pauseQueued;
        private bool _restartQueued;
        private bool _muteQueued;

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            if (kb.spaceKey.wasPressedThisFrame) _kickQueued = true;
            if (kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame) _pauseQueued = true;
            if (kb.rKey.wasPressedThisFrame) _restartQueued = true;
            if (kb.mKey.wasPressedThisFrame) _muteQueued = true;
        }

        public VehicleInput ReadInput()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null)
            {
                _kickQueued = false;
                return default;
            }

            float throttle = (kb.wKey.isPressed || kb.upArrowKey.isPressed) ? 1f : 0f;
            float brake = (kb.sKey.isPressed || kb.downArrowKey.isPressed) ? 1f : 0f;

            float rawSteer = 0f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) rawSteer -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) rawSteer += 1f;

            float dt = Time.deltaTime > 0f ? Time.deltaTime : Time.fixedDeltaTime;
            _smoothedSteer = Mathf.MoveTowards(_smoothedSteer, rawSteer, steerResponseSpeed * dt);

            bool kick = _kickQueued || kb.spaceKey.wasPressedThisFrame;
            _kickQueued = false;

            return new VehicleInput(throttle, brake, _smoothedSteer, kick);
        }

        public bool WasPausePressed()
        {
            Keyboard kb = Keyboard.current;
            bool pressed = _pauseQueued || (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame));
            _pauseQueued = false;
            return pressed;
        }

        public bool WasRestartPressed()
        {
            Keyboard kb = Keyboard.current;
            bool pressed = _restartQueued || (kb != null && kb.rKey.wasPressedThisFrame);
            _restartQueued = false;
            return pressed;
        }

        public bool WasRecoverPressed()
        {
            return false;
        }

        public bool WasMutePressed()
        {
            Keyboard kb = Keyboard.current;
            bool pressed = _muteQueued || (kb != null && kb.mKey.wasPressedThisFrame);
            _muteQueued = false;
            return pressed;
        }

        public void ResetState()
        {
            _smoothedSteer = 0f;
            _kickQueued = false;
            _pauseQueued = false;
            _restartQueued = false;
            _muteQueued = false;
        }
    }
}
