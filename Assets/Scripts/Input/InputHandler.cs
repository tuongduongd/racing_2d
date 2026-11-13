using UnityEngine;
using UnityEngine.InputSystem;

namespace RacingGame
{
    /// <summary>
    /// Reads keyboard input via Unity's InputSystem and converts it into VehicleInput.
    /// Used by PlayerDriver (composition).
    /// </summary>
    public class InputHandler : MonoBehaviour
    {
        [Header("Steering Smoothing")]
        [SerializeField] private float steerResponseSpeed = 9f;

        private float _smoothedSteer;

        public VehicleInput ReadInput()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null)
            {
                return default;
            }

            float throttle = (kb.wKey.isPressed || kb.upArrowKey.isPressed) ? 1f : 0f;
            float brake = (kb.sKey.isPressed || kb.downArrowKey.isPressed || kb.spaceKey.isPressed) ? 1f : 0f;

            float rawSteer = 0f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) rawSteer -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) rawSteer += 1f;

            float dt = Time.deltaTime > 0f ? Time.deltaTime : Time.fixedDeltaTime;
            _smoothedSteer = Mathf.MoveTowards(_smoothedSteer, rawSteer, steerResponseSpeed * dt);

            return new VehicleInput(throttle, brake, _smoothedSteer);
        }

        public void ResetState()
        {
            _smoothedSteer = 0f;
        }
    }
}
