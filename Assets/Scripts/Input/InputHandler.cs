using UnityEngine;
using UnityEngine.InputSystem;

namespace RacingGame
{
    /// <summary>
    /// Reads keyboard input via Unity's InputSystem and translates it into VehicleInput
    /// for the player-controlled Car.
    /// </summary>
    public class InputHandler : MonoBehaviour, IVehicleDriver
    {
        [Header("Steering Smoothing")]
        public float steerResponseSpeed = 9f;

        private float _smoothedSteer;

        public VehicleInput DecideInput(Car car, WaypointTrack track)
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
    }
}
